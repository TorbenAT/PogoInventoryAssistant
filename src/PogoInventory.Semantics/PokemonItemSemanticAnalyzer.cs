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
    IReadOnlyList<PokemonEvidenceFrame> HeaderFrames,
    IReadOnlyList<PokemonEvidenceFrame> AppraisalFrames);

public sealed record PokemonItemSemanticResult(
    string ItemId,
    SemanticFieldResult<string> Species,
    SemanticFieldResult<int?> Cp,
    SemanticFieldResult<int?> AttackIv,
    SemanticFieldResult<int?> DefenseIv,
    SemanticFieldResult<int?> HpIv,
    bool IsComplete,
    IReadOnlyDictionary<string, double> AnalyzerTimingsMilliseconds);

public sealed record SemanticObservation<T>(
    T? Value,
    double Confidence,
    long FrameId,
    string EvidenceHash);

public static class PokemonItemProgressionEvidence
{
    public static IReadOnlyList<string> ProveDifferent(
        PokemonItemSemanticResult previous,
        PokemonItemSemanticResult candidate)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);

        var reasons = new List<string>();
        Compare(previous.Species, candidate.Species, "SPECIES_CHANGED", reasons);
        Compare(previous.Cp, candidate.Cp, "CP_CHANGED", reasons);
        Compare(previous.AttackIv, candidate.AttackIv, "ATTACK_IV_CHANGED", reasons);
        Compare(previous.DefenseIv, candidate.DefenseIv, "DEFENSE_IV_CHANGED", reasons);
        Compare(previous.HpIv, candidate.HpIv, "HP_IV_CHANGED", reasons);
        return reasons;
    }

    private static void Compare<T>(
        SemanticFieldResult<T> previous,
        SemanticFieldResult<T> candidate,
        string reason,
        ICollection<string> reasons)
    {
        if (previous.Status == SemanticFieldStatus.Known &&
            candidate.Status == SemanticFieldStatus.Known &&
            !EqualityComparer<T?>.Default.Equals(previous.Value, candidate.Value))
        {
            reasons.Add(reason);
        }
    }
}

public sealed class PokemonItemSemanticAnalyzer
{
    public static SemanticFieldResult<int?> ResolveCpPreprocessingVariants(
        SemanticFieldResult<int?> first,
        SemanticFieldResult<int?> second,
        bool preferSecondWhenFullySupported = false,
        int fullSupportFrameCount = 5)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Status == SemanticFieldStatus.Known &&
            second.Status == SemanticFieldStatus.Known)
        {
            if (first.Value != second.Value)
            {
                var firstSupport = first.FrameIds.Distinct().Count();
                var secondSupport = second.FrameIds.Distinct().Count();
                if (firstSupport >= fullSupportFrameCount &&
                    secondSupport < fullSupportFrameCount)
                {
                    return first with
                    {
                        Reasons = first.Reasons
                            .Append(
                                "CP_PREPROCESSING_FULL_WINDOW_SUPPORT_SELECTED")
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    };
                }
                if (secondSupport >= fullSupportFrameCount &&
                    (firstSupport < fullSupportFrameCount ||
                     preferSecondWhenFullySupported))
                {
                    return second with
                    {
                        Reasons = second.Reasons
                            .Append(
                                preferSecondWhenFullySupported &&
                                firstSupport >= fullSupportFrameCount
                                    ? "CP_CALIBRATED_PREPROCESSING_SELECTED"
                                    : "CP_PREPROCESSING_FULL_WINDOW_SUPPORT_SELECTED")
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    };
                }

                var firstText = first.Value!.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                var secondText = second.Value!.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                if (first.FrameIds.Distinct().Count() >= 3 &&
                    second.FrameIds.Distinct().Count() >= 3 &&
                    (firstText.StartsWith(
                         secondText, StringComparison.Ordinal) ||
                     secondText.StartsWith(
                         firstText, StringComparison.Ordinal)))
                {
                    return new(
                        default,
                        SemanticFieldStatus.Unknown,
                        0,
                        first.FrameIds.Concat(second.FrameIds).Distinct()
                            .Order().ToArray(),
                        first.EvidenceHashes.Concat(second.EvidenceHashes)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal).ToArray(),
                        ["CP_PREPROCESSING_PREFIX_AMBIGUOUS"]);
                }

                return new(
                    default,
                    SemanticFieldStatus.Conflicting,
                    0,
                    first.FrameIds.Concat(second.FrameIds).Distinct()
                        .Order().ToArray(),
                    first.EvidenceHashes.Concat(second.EvidenceHashes)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal).ToArray(),
                    ["CP_PREPROCESSING_VARIANTS_CONFLICT"]);
            }

            return first.FrameIds.Count >= second.FrameIds.Count
                ? first
                : second;
        }

        var known = first.Status == SemanticFieldStatus.Known
            ? first
            : second.Status == SemanticFieldStatus.Known
                ? second
                : null;
        var alternate = ReferenceEquals(known, first) ? second : first;
        var knownSupport = known?.FrameIds.Distinct().Count() ?? 0;
        if (known is not null &&
            (alternate.Status == SemanticFieldStatus.Conflicting
                ? knownSupport >= 4
                : knownSupport >= 3))
        {
            return known with
            {
                Reasons = known.Reasons
                    .Append("CP_PREPROCESSING_VARIANT_SELECTED")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }

        return new(
            default,
            SemanticFieldStatus.Unknown,
            0,
            first.FrameIds.Concat(second.FrameIds).Distinct()
                .Order().ToArray(),
            first.EvidenceHashes.Concat(second.EvidenceHashes)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            [
                first.Status == SemanticFieldStatus.Conflicting ||
                second.Status == SemanticFieldStatus.Conflicting
                    ? "CP_PREPROCESSING_NO_TRUSTED_CANDIDATE"
                    : "CP_PREPROCESSING_VARIANTS_UNKNOWN"
            ]);
    }

    public PokemonItemSemanticResult Analyze(
        PokemonItemEvidenceSet evidence,
        IReadOnlyList<SemanticObservation<string>> species,
        IReadOnlyList<SemanticObservation<int?>> cp,
        IReadOnlyList<SemanticObservation<(int Attack, int Defense, int Hp)>> iv)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var speciesResult = Resolve(evidence.HeaderFrames, species, "SPECIES");
        var cpResult = ResolveCp(evidence.HeaderFrames, cp);
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

    private static SemanticFieldResult<T> Resolve<T>(
        IReadOnlyList<PokemonEvidenceFrame> frames,
        IReadOnlyList<SemanticObservation<T>> observations,
        string reason,
        double minimumAnchorConfidence = 0)
    {
        var valid = observations
            .Where(x => x.Value is not null && frames.Any(f => f.FrameId == x.FrameId && f.EvidenceHash == x.EvidenceHash))
            .GroupBy(x => (x.FrameId, x.EvidenceHash))
            .Select(x => x.OrderByDescending(y => y.Confidence).First())
            .ToArray();
        if (valid.Select(x => x.Value).Distinct().Skip(1).Any())
            return new(default, SemanticFieldStatus.Conflicting, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_CONFLICTING_EVIDENCE"]);
        if (minimumAnchorConfidence > 0 &&
            !valid.Any(x => x.Confidence >= minimumAnchorConfidence))
            return new(default, SemanticFieldStatus.Unknown, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_ANCHOR_NOT_OBSERVED"]);
        if (!SemanticConsensus.TryResolve(valid.Select(x => x.Value!), out var resolved))
            return new(default, SemanticFieldStatus.Unknown, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_CONSENSUS_NOT_REACHED"]);
        var group = valid.Where(x => EqualityComparer<T>.Default.Equals(x.Value, resolved)).ToArray();
        return new(resolved, SemanticFieldStatus.Known, group.Average(x => x.Confidence), group.Select(x => x.FrameId).ToArray(), group.Select(x => x.EvidenceHash).ToArray(), [$"{reason}_TWO_FRAME_AGREEMENT"]);
    }

    private static SemanticFieldResult<int?> ResolveCp(
        IReadOnlyList<PokemonEvidenceFrame> frames,
        IReadOnlyList<SemanticObservation<int?>> observations)
    {
        var valid = observations
            .Where(x => x.Value is not null &&
                frames.Any(f =>
                    f.FrameId == x.FrameId &&
                    f.EvidenceHash == x.EvidenceHash))
            .GroupBy(x => (x.FrameId, x.EvidenceHash))
            .Select(x => x.OrderByDescending(y => y.Confidence).First())
            .ToArray();
        var distinct = valid.Select(x => x.Value).Distinct().ToArray();
        if (distinct.Length <= 1)
        {
            return Resolve(
                frames, observations, "CP",
                minimumAnchorConfidence: 1.0);
        }

        var groups = valid
            .GroupBy(x => x.Value)
            .OrderByDescending(x => x.Count())
            .ToArray();
        if (valid.Length >= 4 &&
            groups[0].Count() >= 3 &&
            (groups.Length == 1 ||
                groups[0].Count() > groups[1].Count()))
        {
            var winner = groups[0].Key!.Value;
            var winnerEvidence = groups[0].ToArray();
            var winnerLength = winner.ToString(
                System.Globalization.CultureInfo.InvariantCulture).Length;
            var maximumObservedLength = valid.Max(x =>
                x.Value!.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture).Length);
            if (winnerLength == maximumObservedLength &&
                winnerEvidence.Count(x => x.Confidence >= 1.0) >= 3 &&
                valid.All(x => IsWithinOneDigitEdit(
                    winner, x.Value!.Value)))
            {
                return new(
                    winner,
                    SemanticFieldStatus.Known,
                    winnerEvidence.Average(x => x.Confidence),
                    winnerEvidence.Select(x => x.FrameId).ToArray(),
                    winnerEvidence.Select(x => x.EvidenceHash).ToArray(),
                    ["CP_MULTI_FRAME_OCCLUSION_AGREEMENT"]);
            }
        }

        if (valid.Length >= 3 &&
            distinct.Length >= 3)
        {
            var candidates = valid
                .Where(x => x.Confidence >= 1.0)
                .Where(candidate => valid.All(other =>
                    IsWithinOneDigitEdit(
                        candidate.Value!.Value,
                        other.Value!.Value)))
                .ToArray();
            if (candidates.Length == 1)
            {
                var candidate = candidates[0];
                return new(
                    candidate.Value,
                    SemanticFieldStatus.Known,
                    valid.Average(x => x.Confidence),
                    valid.Select(x => x.FrameId).ToArray(),
                    valid.Select(x => x.EvidenceHash).ToArray(),
                    ["CP_THREE_FRAME_EDIT_CHAIN_AGREEMENT"]);
            }
        }

        if (valid.Length >= 5)
        {
            var maximumObservedLength = valid
                .Max(x => x.Value!.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                    .Length);
            var reconstructed = Enumerable.Range(10, 5991)
                .Where(candidate =>
                    candidate.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                        .Length == maximumObservedLength + 1)
                .Select(candidate => new
                {
                    Value = candidate,
                    Supporting = valid.Where(observation =>
                        IsSingleDeletionOf(
                            candidate, observation.Value!.Value)).ToArray()
                })
                .Where(candidate =>
                    candidate.Supporting.Length >= 4 &&
                    candidate.Supporting.Count(x =>
                        x.Confidence >= .75) >= 2)
                .OrderByDescending(candidate =>
                    candidate.Supporting.Length)
                .ThenBy(candidate => candidate.Value)
                .ToArray();
            if (reconstructed.Length == 1 ||
                reconstructed.Length > 1 &&
                reconstructed[0].Supporting.Length >
                    reconstructed[1].Supporting.Length)
            {
                var winner = reconstructed[0];
                return new(
                    winner.Value,
                    SemanticFieldStatus.Known,
                    winner.Supporting.Average(x => x.Confidence),
                    winner.Supporting.Select(x => x.FrameId).ToArray(),
                    winner.Supporting.Select(x => x.EvidenceHash).ToArray(),
                    ["CP_UNIQUE_MULTI_FRAME_DELETION_RECONSTRUCTION"]);
            }
        }

        return new(
            default,
            SemanticFieldStatus.Conflicting,
            0,
            valid.Select(x => x.FrameId).ToArray(),
            valid.Select(x => x.EvidenceHash).ToArray(),
            ["CP_CONFLICTING_EVIDENCE"]);
    }

    private static bool IsWithinOneDigitEdit(int first, int second)
    {
        var left = first.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var right = second.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }
        if (Math.Abs(left.Length - right.Length) > 1)
        {
            return false;
        }
        if (left.Length == right.Length)
        {
            return left.Zip(right).Count(pair =>
                pair.First != pair.Second) <= 1;
        }

        var shorter = left.Length < right.Length ? left : right;
        var longer = left.Length < right.Length ? right : left;
        var shortIndex = 0;
        var skipped = false;
        for (var longIndex = 0; longIndex < longer.Length; longIndex++)
        {
            if (shortIndex < shorter.Length &&
                shorter[shortIndex] == longer[longIndex])
            {
                shortIndex++;
                continue;
            }
            if (skipped)
            {
                return false;
            }
            skipped = true;
        }
        return true;
    }

    private static bool IsSingleDeletionOf(
        int candidate,
        int observation)
    {
        var longer = candidate.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var shorter = observation.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        if (longer.Length != shorter.Length + 1)
        {
            return false;
        }

        for (var removed = 0; removed < longer.Length; removed++)
        {
            if (string.Equals(
                longer.Remove(removed, 1),
                shorter,
                StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static SemanticFieldResult<int?> ResolveIv(IReadOnlyList<PokemonEvidenceFrame> frames, IReadOnlyList<SemanticObservation<(int Attack, int Defense, int Hp)>> observations, string field)
    {
        var valid = observations
            .Where(x => frames.Any(f => f.FrameId == x.FrameId && f.EvidenceHash == x.EvidenceHash) && x.Confidence >= .5)
            .GroupBy(x => (x.FrameId, x.EvidenceHash))
            .Select(x => x.OrderByDescending(y => y.Confidence).First())
            .ToArray();
        if (valid.Select(x => x.Value).Distinct().Skip(1).Any())
            return new(default, SemanticFieldStatus.Conflicting, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{field.ToUpperInvariant()}_CONFLICTING_EVIDENCE"]);
        if (!SemanticConsensus.TryResolve(valid.Select(x => x.Value), out var resolved)) return new(default, SemanticFieldStatus.Unknown, 0, valid.Select(x => x.FrameId).ToArray(), valid.Select(x => x.EvidenceHash).ToArray(), [$"{field.ToUpperInvariant()}_CONSENSUS_NOT_REACHED"]);
        var group = valid.Where(x => x.Value.Equals(resolved)).ToArray();
        var value = field switch { "AttackIV" => resolved.Attack, "DefenseIV" => resolved.Defense, _ => resolved.Hp };
        return new(value, SemanticFieldStatus.Known, group.Average(x => x.Confidence), group.Select(x => x.FrameId).ToArray(), group.Select(x => x.EvidenceHash).ToArray(), [$"{field.ToUpperInvariant()}_TWO_FRAME_AGREEMENT"]);
    }
}
