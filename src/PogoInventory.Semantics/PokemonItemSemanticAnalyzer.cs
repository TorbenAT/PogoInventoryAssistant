namespace PogoInventory.Semantics;

public enum SemanticFieldStatus { Known, Unknown, Conflicting }

public sealed record SemanticFieldResult<T>(
    T? Value,
    SemanticFieldStatus Status,
    double Confidence,
    IReadOnlyList<long> FrameIds,
    IReadOnlyList<string> EvidenceHashes,
    IReadOnlyList<string> Reasons);

public sealed record PokemonEvidenceFrame(
    long FrameId,
    DateTimeOffset CapturedAtUtc,
    string EvidenceHash,
    string ScreenState,
    string Source);

public sealed record PokemonItemEvidenceSet(
    string ItemId,
    IReadOnlyList<PokemonEvidenceFrame> DetailsFrames,
    IReadOnlyList<PokemonEvidenceFrame> AppraisalFrames);

public sealed record PokemonItemSemanticResult(
    string ItemId,
    SemanticFieldResult<string> Species,
    SemanticFieldResult<int> Cp,
    SemanticFieldResult<int> AttackIv,
    SemanticFieldResult<int> DefenseIv,
    SemanticFieldResult<int> HpIv,
    bool IsComplete,
    IReadOnlyDictionary<string, double> AnalyzerTimingsMilliseconds);

public sealed record SemanticObservation<T>(
    T? Value,
    double Confidence,
    long FrameId,
    string EvidenceHash);

public sealed class PokemonItemSemanticAnalyzer
{
    public PokemonItemSemanticResult Analyze(
        PokemonItemEvidenceSet evidence,
        IReadOnlyList<SemanticObservation<string>> species,
        IReadOnlyList<SemanticObservation<int>> cp,
        IReadOnlyList<SemanticObservation<(int Attack, int Defense, int Hp)>> iv)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var speciesResult = Resolve(evidence.DetailsFrames, species, "SPECIES");
        var cpResult = Resolve(evidence.DetailsFrames, cp, "CP");
        var attackResult = ResolveIv(evidence.AppraisalFrames, iv, "AttackIV");
        var defenseResult = ResolveIv(evidence.AppraisalFrames, iv, "DefenseIV");
        var hpResult = ResolveIv(evidence.AppraisalFrames, iv, "HPIV");
        return new(
            evidence.ItemId,
            speciesResult, cpResult, attackResult, defenseResult, hpResult,
            speciesResult.Status == SemanticFieldStatus.Known && cpResult.Status == SemanticFieldStatus.Known &&
            attackResult.Status == SemanticFieldStatus.Known && defenseResult.Status == SemanticFieldStatus.Known &&
            hpResult.Status == SemanticFieldStatus.Known,
            new Dictionary<string, double>(StringComparer.Ordinal));
    }

    private static SemanticFieldResult<T> Resolve<T>(IReadOnlyList<PokemonEvidenceFrame> frames, IReadOnlyList<SemanticObservation<T>> observations, string reason)
    {
        var valid = observations.Where(x => x.Value is not null && frames.Any(f => f.FrameId == x.FrameId && f.EvidenceHash == x.EvidenceHash)).ToArray();
        if (!SemanticConsensus.TryResolve(valid.Select(x => x.Value!), out var resolved))
            return new(default, valid.GroupBy(x => x.Value).Count() > 1 ? SemanticFieldStatus.Conflicting : SemanticFieldStatus.Unknown, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_CONSENSUS_NOT_REACHED"]);
        var group = valid.Where(x => EqualityComparer<T>.Default.Equals(x.Value, resolved)).ToArray();
        return new(resolved, SemanticFieldStatus.Known, group.Average(x => x.Confidence), group.Select(x => x.FrameId).ToArray(), group.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_TWO_FRAME_AGREEMENT"]);
    }

    private static SemanticFieldResult<int> ResolveIv(IReadOnlyList<PokemonEvidenceFrame> frames, IReadOnlyList<SemanticObservation<(int Attack, int Defense, int Hp)>> observations, string field)
    {
        var valid = observations.Where(x => frames.Any(f => f.FrameId == x.FrameId && f.EvidenceHash == x.EvidenceHash) && x.Confidence >= .5).ToArray();
        if (!SemanticConsensus.TryResolve(valid.Select(x => x.Value), out var resolved)) return new(default, valid.Length > 1 ? SemanticFieldStatus.Conflicting : SemanticFieldStatus.Unknown, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{field.ToUpperInvariant()}_CONSENSUS_NOT_REACHED"]);
        var group = valid.Where(x => x.Value.Equals(resolved)).ToArray();
        var value = field switch { "AttackIV" => resolved.Attack, "DefenseIV" => resolved.Defense, _ => resolved.Hp };
        return new(value, SemanticFieldStatus.Known, group.Average(x => x.Confidence), group.Select(x => x.FrameId).ToArray(), group.Select(x => x.EvidenceHash).ToArray(), [$"{field.ToUpperInvariant()}_TWO_FRAME_AGREEMENT"]);
    }
}
