using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

public enum GroundTruthStatus
{
    Verified,
    Unverifiable,
    NotApplicable
}

public sealed record GroundTruthLabelRow
{
    public required string RunId { get; init; }
    public required int Ordinal { get; init; }
    public required string ObservationId { get; init; }
    public string? GroundTruthEntityId { get; init; }
    public string? Species { get; init; }
    public string? Cp { get; init; }
    public string? AttackIv { get; init; }
    public string? DefenseIv { get; init; }
    public string? HpIv { get; init; }
    public string? Nickname { get; init; }
    public required GroundTruthStatus GroundTruthStatus { get; init; }
    public required string GroundTruthSource { get; init; }
    public string? ReviewerNote { get; init; }
    public string? DetailsEvidence { get; init; }
    public string? AppraisalEvidence { get; init; }
    public string? ScannerSpecies { get; init; }
    public string? ScannerCp { get; init; }
    public string? ScannerAttackIv { get; init; }
    public string? ScannerDefenseIv { get; init; }
    public string? ScannerHpIv { get; init; }
    public string? ScannerNickname { get; init; }
}

public sealed record GroundTruthFieldMetric
{
    public required string Field { get; init; }
    public required int VerifiedRows { get; init; }
    public required int Correct { get; init; }
    public required int Incorrect { get; init; }
    public required int Unknown { get; init; }
    public required int Unverifiable { get; init; }
    public required int NotApplicable { get; init; }
    public double? AccuracyAmongVerifiablePercent { get; init; }
    public double? CompletenessAmongVerifiablePercent { get; init; }
}

public sealed record GroundTruthReviewCase
{
    public required int Ordinal { get; init; }
    public required string Run1ObservationId { get; init; }
    public required string Run2ObservationId { get; init; }
    public required string MatchOutcome { get; init; }
    public string? Run1GroundTruthEntityId { get; init; }
    public string? Run2GroundTruthEntityId { get; init; }
    public bool? SamePokemon { get; init; }
    public required string Cause { get; init; }
    public required string MissingOrConflictingFeature { get; init; }
    public bool? MatcherCorrectWithCorrectExtraction { get; init; }
    public string? GroundTruthValues { get; init; }
    public string? ScannerValues { get; init; }
}

public sealed record GroundTruthGainEstimate
{
    public required string Scenario { get; init; }
    public int? AutomaticMatches { get; init; }
    public int? ReviewCases { get; init; }
    public double? ReMatchRatePercent { get; init; }
    public required string Status { get; init; }
}

public sealed record GroundTruthMeasurementReport
{
    public required string GroundTruthCsvPath { get; init; }
    public required string Run1DatabasePath { get; init; }
    public required string Run2DatabasePath { get; init; }
    public required int GroundTruthRows { get; init; }
    public required IReadOnlyList<GroundTruthFieldMetric> OverallFields { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<GroundTruthFieldMetric>> ByRun { get; init; }
    public required IReadOnlyList<GroundTruthReviewCase> ReviewCases { get; init; }
    public required IReadOnlyList<GroundTruthGainEstimate> GainEstimates { get; init; }
}

internal sealed record GroundTruthCaptureRow
{
    public required string RunId { get; init; }
    public required int Ordinal { get; init; }
    public required string LocalPokemonId { get; init; }
    public required PokemonObservation Observation { get; init; }
    public IReadOnlyList<string> ScreenshotPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AppraisalEvidence { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Offline, evidence-first ground-truth preparation and comparison. It never
/// connects to a phone and never modifies scanner databases.
/// </summary>
public sealed class GroundTruthMeasurementService
{
    private static readonly string[] Fields = ["Species", "Cp", "AttackIv", "DefenseIv", "HpIv", "Nickname"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<int> PrepareAsync(
        string evidenceRoot,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(evidenceRoot);
        var output = Path.GetFullPath(outputDirectory);
        var rows = new List<GroundTruthLabelRow>();
        foreach (var runDirectory in Directory.GetDirectories(root, "run*", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var captured = Path.Combine(runDirectory, "captured-observations.json");
            if (!File.Exists(captured)) continue;
            var capturedRows = JsonSerializer.Deserialize<List<GroundTruthCaptureRow>>(
                await File.ReadAllTextAsync(captured, cancellationToken), JsonOptions)
                ?? throw new InvalidOperationException($"No observations in {captured}.");
            foreach (var row in capturedRows.OrderBy(x => x.Ordinal))
            {
                rows.Add(new GroundTruthLabelRow
                {
                    RunId = row.RunId,
                    Ordinal = row.Ordinal,
                    ObservationId = row.LocalPokemonId,
                    GroundTruthStatus = GroundTruthStatus.Unverifiable,
                    GroundTruthSource = EvidenceSource(row),
                    ReviewerNote = "Pending manual verification. Scanner values are shown separately and must not be copied as ground truth.",
                    DetailsEvidence = string.Join("|", row.ScreenshotPaths),
                    AppraisalEvidence = string.Join("|", row.AppraisalEvidence),
                    ScannerSpecies = NullIfUnknown(row.Observation.Species),
                    ScannerCp = row.Observation.Cp?.ToString(CultureInfo.InvariantCulture),
                    ScannerAttackIv = row.Observation.AttackIv?.ToString(CultureInfo.InvariantCulture),
                    ScannerDefenseIv = row.Observation.DefenseIv?.ToString(CultureInfo.InvariantCulture),
                    ScannerHpIv = row.Observation.HpIv?.ToString(CultureInfo.InvariantCulture),
                    ScannerNickname = row.Observation.Nickname
                });
            }
        }
        if (rows.Count == 0) throw new InvalidOperationException($"No captured-observations.json files found under {root}.");
        Directory.CreateDirectory(output);
        await WriteCsvAsync(Path.Combine(output, "ground-truth.csv"), rows, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "ground-truth-task-k-v1",
            evidenceRoot = root,
            rowCount = rows.Count,
            status = "Unverifiable until manually reviewed",
            generatedAtUtc = DateTimeOffset.UtcNow
        }, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "labeling.html"), BuildHtml(rows), cancellationToken);
        return rows.Count;
    }

    public async Task<GroundTruthMeasurementReport> AnalyzeAsync(
        string groundTruthCsv,
        string run1Database,
        string run2Database,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var labels = await ReadCsvAsync(groundTruthCsv, cancellationToken);
        ValidateLabels(labels);
        var rows1 = await LoadRowsAsync(run1Database, cancellationToken);
        var rows2 = await LoadRowsAsync(run2Database, cancellationToken);
        var allRows = rows1.Concat(rows2).ToList();
        var byKey = labels.ToDictionary(x => Key(x.RunId, x.Ordinal));
        var labeledRows = allRows.Where(row => byKey.ContainsKey(Key(row.RunId, row.Ordinal))).ToList();
        var overall = Fields.Select(field => Metric(field, labeledRows.Select(row => (row, label: byKey[Key(row.RunId, row.Ordinal)])))).ToList();
        var byRun = labeledRows.GroupBy(x => x.RunId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<GroundTruthFieldMetric>)Fields.Select(field => Metric(field, group.Select(row => (row, label: byKey[Key(row.RunId, row.Ordinal)])))).ToList());

        var prior = rows1.Select(ToSemantic).ToList();
        var rows1ByOrdinal = rows1.ToDictionary(x => x.Ordinal);
        var review = new List<GroundTruthReviewCase>();
        foreach (var row2 in rows2)
        {
            var match = SemanticIdentityMatcher.Match(prior, ToSemantic(row2));
            if (match.Outcome == SemanticMatchOutcome.Matched) continue;
            if (!rows1ByOrdinal.TryGetValue(row2.Ordinal, out var row1)) continue;
            if (!byKey.TryGetValue(Key(row1.RunId, row1.Ordinal), out var label1) ||
                !byKey.TryGetValue(Key(row2.RunId, row2.Ordinal), out var label2)) continue;
            var same = label1.GroundTruthEntityId is not null && label1.GroundTruthEntityId == label2.GroundTruthEntityId;
            var sameKnown = label1.GroundTruthEntityId is not null && label2.GroundTruthEntityId is not null ? same : (bool?)null;
            var feature = Cause(row1.Observation, row2.Observation, label1, label2);
            review.Add(new GroundTruthReviewCase
            {
                Ordinal = row2.Ordinal,
                Run1ObservationId = row1.LocalPokemonId,
                Run2ObservationId = row2.LocalPokemonId,
                MatchOutcome = match.Outcome.ToString(),
                Run1GroundTruthEntityId = label1.GroundTruthEntityId,
                Run2GroundTruthEntityId = label2.GroundTruthEntityId,
                SamePokemon = sameKnown,
                Cause = feature.Cause,
                MissingOrConflictingFeature = feature.Feature,
                MatcherCorrectWithCorrectExtraction = sameKnown is null ? null : same && CanCompare(label1, label2),
                GroundTruthValues = Values(label1, label2, true),
                ScannerValues = Values(label1, label2, false)
            });
        }
        var report = new GroundTruthMeasurementReport
        {
            GroundTruthCsvPath = Path.GetFullPath(groundTruthCsv),
            Run1DatabasePath = Path.GetFullPath(run1Database),
            Run2DatabasePath = Path.GetFullPath(run2Database),
            GroundTruthRows = labels.Count,
            OverallFields = overall,
            ByRun = byRun,
            ReviewCases = review,
            GainEstimates = BuildGainEstimates(review, rows1ByOrdinal, rows2, byKey)
        };
        var output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "field-completeness-report.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "field-completeness-report.md"), BuildMarkdown(report), cancellationToken);
        await WriteReviewCsvAsync(Path.Combine(output, "review-cases.csv"), review, cancellationToken);
        return report;
    }

    public static IReadOnlyList<GroundTruthLabelRow> ParseCsv(string csv) => Parse(csv);

    public static GroundTruthFieldMetric MeasureFieldForTests(
        string field,
        IEnumerable<(GroundTruthLabelRow Label, PokemonObservation Observation)> entries) =>
        Metric(field, entries.Select(entry => (new CleanupProofDatabaseRow
        {
            RunId = entry.Label.RunId,
            Ordinal = entry.Label.Ordinal,
            LocalPokemonId = entry.Label.ObservationId,
            CapturedAtUtc = DateTimeOffset.UnixEpoch,
            Observation = entry.Observation,
            ObservationStatus = "Partial",
            IdentityConfidenceValue = 0,
            ProtectionConfidenceValue = 0,
            StableFingerprint = "test",
            ScreenshotPaths = Array.Empty<string>(),
            ScreenshotHashes = Array.Empty<string>(),
            AppraisalEvidence = Array.Empty<string>(),
            FieldEvidenceSources = new Dictionary<string, string>(),
            CurrentRecommendation = "REVIEW",
            RecommendationReason = "test"
        }, entry.Label)));

    public static (string Cause, string Feature) ClassifyCauseForTests(
        PokemonObservation run1,
        PokemonObservation run2,
        GroundTruthLabelRow label1,
        GroundTruthLabelRow label2) => Cause(run1, run2, label1, label2);

    private static async Task<List<CleanupProofDatabaseRow>> LoadRowsAsync(string database, CancellationToken cancellationToken)
    {
        var service = new InventoryPersistenceService(database);
        var runIds = await service.LoadAllCleanupRunIdsAsync(cancellationToken);
        if (runIds.Count != 1) throw new InvalidOperationException($"Expected exactly one scan run in {database}, found {runIds.Count}.");
        return (await service.LoadCleanupProofRowsAsync(runIds[0], cancellationToken)).ToList();
    }

    private static GroundTruthFieldMetric Metric(string field, IEnumerable<(CleanupProofDatabaseRow row, GroundTruthLabelRow label)> entries)
    {
        var verified = 0;
        var correct = 0;
        var incorrect = 0;
        var unknown = 0;
        var unverifiable = 0;
        var notApplicable = 0;
        foreach (var entry in entries)
        {
            var expected = Field(entry.label, field);
            var actual = Field(entry.row.Observation, field);
            switch (entry.label.GroundTruthStatus)
            {
                case GroundTruthStatus.Unverifiable: unverifiable++; break;
                case GroundTruthStatus.NotApplicable: notApplicable++; break;
                case GroundTruthStatus.Verified:
                    verified++;
                    if (actual is null) unknown++;
                    else if (Normalize(actual) == Normalize(expected)) correct++;
                    else incorrect++;
                    break;
            }
        }
        var comparable = correct + incorrect;
        return new GroundTruthFieldMetric
        {
            Field = field,
            VerifiedRows = verified,
            Correct = correct,
            Incorrect = incorrect,
            Unknown = unknown,
            Unverifiable = unverifiable,
            NotApplicable = notApplicable,
            AccuracyAmongVerifiablePercent = comparable == 0 ? null : correct * 100.0 / comparable,
            CompletenessAmongVerifiablePercent = verified == 0 ? null : comparable * 100.0 / verified
        };
    }

    private static (string Cause, string Feature) Cause(PokemonObservation a, PokemonObservation b, GroundTruthLabelRow la, GroundTruthLabelRow lb)
    {
        if (string.IsNullOrWhiteSpace(a.Species) || a.Species.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(b.Species) || b.Species.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return ("SpeciesNotExtracted", "Species");
        if (a.Cp is null || b.Cp is null) return ("CpNotExtracted", "CP");
        if (a.Cp != b.Cp) return (la.GroundTruthStatus == GroundTruthStatus.Verified && lb.GroundTruthStatus == GroundTruthStatus.Verified ? "CpIncorrect" : "Other", "CP conflict");
        if (a.AttackIv is null || a.DefenseIv is null || a.HpIv is null || b.AttackIv is null || b.DefenseIv is null || b.HpIv is null) return ("IvNotExtracted", "IV");
        return ("Other", "Semantic identity remains unresolved");
    }

    private static IReadOnlyList<GroundTruthGainEstimate> BuildGainEstimates(IReadOnlyList<GroundTruthReviewCase> review, IReadOnlyDictionary<int, CleanupProofDatabaseRow> rows1, IReadOnlyList<CleanupProofDatabaseRow> rows2, IReadOnlyDictionary<string, GroundTruthLabelRow> labels)
    {
        var scenarios = new[] { "CP alone", "IV alone", "Species alone", "CP and IV", "All identified extractor errors" };
        var eligible = review.Where(x => x.SamePokemon is true &&
            labels.TryGetValue(Key(rows1[x.Ordinal].RunId, x.Ordinal), out var a) &&
            labels.TryGetValue(Key(rows2.First(y => y.Ordinal == x.Ordinal).RunId, x.Ordinal), out var b) &&
            a.GroundTruthStatus == GroundTruthStatus.Verified &&
            b.GroundTruthStatus == GroundTruthStatus.Verified).ToList();
        if (eligible.Count == 0)
        {
            return scenarios.Select(x => new GroundTruthGainEstimate
            {
                Scenario = x,
                Status = "Not calculable: manually verified identity labels are incomplete."
            }).ToList();
        }

        var definitions = new (string Name, bool Species, bool Cp, bool Iv, bool Nickname)[]
        {
            ("CP alone", false, true, false, false),
            ("IV alone", false, false, true, false),
            ("Species alone", true, false, false, false),
            ("CP and IV", false, true, true, false),
            ("All identified extractor errors", true, true, true, true)
        };
        return definitions.Select(definition =>
        {
            var matches = 0;
            foreach (var item in eligible)
            {
                var row1 = rows1[item.Ordinal];
                var row2 = rows2.First(row => row.Ordinal == item.Ordinal);
                var label1 = labels[Key(row1.RunId, row1.Ordinal)];
                var label2 = labels[Key(row2.RunId, row2.Ordinal)];
                var corrected1 = Correct(row1.Observation, label1, definition);
                var corrected2 = Correct(row2.Observation, label2, definition);
                var key1 = SemanticIdentityKey.FromObservation(corrected1);
                var key2 = SemanticIdentityKey.FromObservation(corrected2);
                if (key1.Completeness == SemanticKeyCompleteness.Comparable && key1.FullKey == key2.FullKey) matches++;
            }
            var rate = rows2.Count == 0 ? 0 : (rows2.Count - review.Count + matches) * 100.0 / rows2.Count;
            return new GroundTruthGainEstimate
            {
                Scenario = definition.Name,
                AutomaticMatches = matches,
                ReviewCases = eligible.Count,
                ReMatchRatePercent = rate,
                Status = "Calculated from verified identity labels."
            };
        }).ToList();
    }

    private static PokemonObservation Correct(PokemonObservation observation, GroundTruthLabelRow label, (string Name, bool Species, bool Cp, bool Iv, bool Nickname) definition) => observation with
    {
        Species = definition.Species && label.Species is not null ? label.Species : observation.Species,
        Cp = definition.Cp && int.TryParse(label.Cp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cp) ? cp : observation.Cp,
        AttackIv = definition.Iv && int.TryParse(label.AttackIv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var attack) ? attack : observation.AttackIv,
        DefenseIv = definition.Iv && int.TryParse(label.DefenseIv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var defense) ? defense : observation.DefenseIv,
        HpIv = definition.Iv && int.TryParse(label.HpIv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hp) ? hp : observation.HpIv,
        Nickname = definition.Nickname && label.Nickname is not null ? label.Nickname : observation.Nickname
    };

    private static bool CanCompare(GroundTruthLabelRow a, GroundTruthLabelRow b) => a.GroundTruthStatus == GroundTruthStatus.Verified && b.GroundTruthStatus == GroundTruthStatus.Verified;
    private static string? Field(GroundTruthLabelRow row, string field) => field switch { "Species" => row.Species, "Cp" => row.Cp, "AttackIv" => row.AttackIv, "DefenseIv" => row.DefenseIv, "HpIv" => row.HpIv, "Nickname" => row.Nickname, _ => null };
    private static string? Field(PokemonObservation o, string field) => field switch { "Species" => NullIfUnknown(o.Species), "Cp" => o.Cp?.ToString(CultureInfo.InvariantCulture), "AttackIv" => o.AttackIv?.ToString(CultureInfo.InvariantCulture), "DefenseIv" => o.DefenseIv?.ToString(CultureInfo.InvariantCulture), "HpIv" => o.HpIv?.ToString(CultureInfo.InvariantCulture), "Nickname" => o.Nickname, _ => null };
    private static string? NullIfUnknown(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? null : value;
    private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();
    private static string Key(string run, int ordinal) => $"{run}|{ordinal}";
    private static string EvidenceSource(GroundTruthCaptureRow row) => string.Join("|", row.ScreenshotPaths.Concat(row.AppraisalEvidence));
    private static SemanticIdentityRecord ToSemantic(CleanupProofDatabaseRow row) => new() { LocalPokemonId = row.LocalPokemonId, FullKey = row.SemanticKey ?? "", Completeness = Enum.TryParse<SemanticKeyCompleteness>(row.SemanticKeyCompleteness, true, out var c) ? c : SemanticKeyCompleteness.Insufficient };
    private static string Values(GroundTruthLabelRow a, GroundTruthLabelRow b, bool truth) => string.Join("; ", $"run1={(truth ? a.Species : a.ScannerSpecies)}/{(truth ? a.Cp : a.ScannerCp)}", $"run2={(truth ? b.Species : b.ScannerSpecies)}/{(truth ? b.Cp : b.ScannerCp)}");
    private static string BuildHtml(IEnumerable<GroundTruthLabelRow> rows)
    {
        var sb = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Ground truth labeling</title><style>body{font-family:Arial}table{border-collapse:collapse}td,th{border:1px solid #bbb;padding:4px;vertical-align:top}img{max-width:220px;max-height:300px}</style><h1>Task-K ground-truth labeling</h1><p>Scanner values are reference only. Fill ground-truth fields independently, set status to Verified only when readable, and retain a concrete source path.</p><table><tr><th>Run</th><th>Ordinal</th><th>Details evidence</th><th>Appraisal evidence</th><th>Scanner output</th><th>Ground truth/status/note</th></tr>");
        foreach (var row in rows) { var details = (row.DetailsEvidence ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries); sb.Append("<tr><td>").Append(H(row.RunId)).Append("</td><td>").Append(row.Ordinal).Append("</td><td>"); foreach (var path in details) sb.Append("<img src='").Append(H(new Uri(Path.GetFullPath(path)).AbsoluteUri)).Append("'><br>"); sb.Append("</td><td>").Append(H(row.AppraisalEvidence)).Append("</td><td>").Append(H($"Species={row.ScannerSpecies}; CP={row.ScannerCp}; IV={row.ScannerAttackIv}/{row.ScannerDefenseIv}/{row.ScannerHpIv}")).Append("</td><td>Status=Unverifiable<br>EntityId=<br>Species=<br>CP=<br>AttackIV=<br>DefenseIV=<br>HpIV=<br>Nickname=<br>Source=").Append(H(row.GroundTruthSource)).Append("<br>Note=").Append(H(row.ReviewerNote)).Append("</td></tr>"); }
        return sb.Append("</table>").ToString();
    }
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string BuildMarkdown(GroundTruthMeasurementReport r) { var sb = new StringBuilder("# Ground-truth field completeness report\n\n"); sb.AppendLine($"- Ground-truth rows: {r.GroundTruthRows}"); sb.AppendLine($"- Review cases: {r.ReviewCases.Count}"); sb.AppendLine("\n## Overall\n\n|Field|Verified|Correct|Incorrect|Unknown|Unverifiable|NotApplicable|Accuracy|Completeness|\n|---|---:|---:|---:|---:|---:|---:|---:|---:|"); foreach (var m in r.OverallFields) sb.AppendLine($"|{m.Field}|{m.VerifiedRows}|{m.Correct}|{m.Incorrect}|{m.Unknown}|{m.Unverifiable}|{m.NotApplicable}|{Fmt(m.AccuracyAmongVerifiablePercent)}|{Fmt(m.CompletenessAmongVerifiablePercent)}|"); sb.AppendLine("\n## Review cases\n\n|Ordinal|Cause|Feature|Same Pokémon|Matcher with corrected extraction|\n|---:|---|---|---|---|"); foreach (var c in r.ReviewCases) sb.AppendLine($"|{c.Ordinal}|{c.Cause}|{c.MissingOrConflictingFeature}|{c.SamePokemon?.ToString() ?? "Unverifiable"}|{c.MatcherCorrectWithCorrectExtraction?.ToString() ?? "Unverifiable"}|"); sb.AppendLine("\n## Gain estimates\n"); foreach (var g in r.GainEstimates) sb.AppendLine($"- {g.Scenario}: {g.Status}"); return sb.ToString(); }
    private static string Fmt(double? value) => value is null ? "N/A" : value.Value.ToString("F2", CultureInfo.InvariantCulture) + "%";
    private static async Task WriteCsvAsync(string path, IEnumerable<GroundTruthLabelRow> rows, CancellationToken token) { var sb = new StringBuilder(Header); foreach (var row in rows) sb.AppendLine(string.Join(',', Row(row).Select(Csv))); await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, token); }
    private static async Task WriteReviewCsvAsync(string path, IEnumerable<GroundTruthReviewCase> rows, CancellationToken token) { var sb = new StringBuilder("Ordinal,Run1ObservationId,Run2ObservationId,MatchOutcome,SamePokemon,Cause,MissingOrConflictingFeature,MatcherCorrectWithCorrectExtraction,GroundTruthValues,ScannerValues\n"); foreach (var r in rows) sb.AppendLine(string.Join(',', new[] { r.Ordinal.ToString(), r.Run1ObservationId, r.Run2ObservationId, r.MatchOutcome, r.SamePokemon?.ToString() ?? "", r.Cause, r.MissingOrConflictingFeature, r.MatcherCorrectWithCorrectExtraction?.ToString() ?? "", r.GroundTruthValues, r.ScannerValues }.Select(Csv))); await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, token); }
    private const string Header = "RunId,Ordinal,ObservationId,GroundTruthEntityId,Species,Cp,AttackIv,DefenseIv,HpIv,Nickname,GroundTruthStatus,GroundTruthSource,ReviewerNote,DetailsEvidence,AppraisalEvidence,ScannerSpecies,ScannerCp,ScannerAttackIv,ScannerDefenseIv,ScannerHpIv,ScannerNickname\n";
    private static IEnumerable<string?> Row(GroundTruthLabelRow r) => [r.RunId, r.Ordinal.ToString(), r.ObservationId, r.GroundTruthEntityId, r.Species, r.Cp, r.AttackIv, r.DefenseIv, r.HpIv, r.Nickname, r.GroundTruthStatus.ToString(), r.GroundTruthSource, r.ReviewerNote, r.DetailsEvidence, r.AppraisalEvidence, r.ScannerSpecies, r.ScannerCp, r.ScannerAttackIv, r.ScannerDefenseIv, r.ScannerHpIv, r.ScannerNickname];
    private static string Csv(string? value) { var v = value ?? ""; return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? '"' + v.Replace("\"", "\"\"") + '"' : v; }
    private static async Task<List<GroundTruthLabelRow>> ReadCsvAsync(string path, CancellationToken token) => Parse(await File.ReadAllTextAsync(path, token));
    private static List<GroundTruthLabelRow> Parse(string csv) { var records = CsvRecords(csv); var header = records.FirstOrDefault() ?? throw new InvalidOperationException("Ground-truth CSV is empty."); var index = header.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i, StringComparer.OrdinalIgnoreCase); string V(IReadOnlyList<string> r, string k) => index.TryGetValue(k, out var i) && i < r.Count ? r[i] : ""; var result = new List<GroundTruthLabelRow>(); foreach (var r in records.Skip(1).Where(x => x.Count > 1)) { if (!int.TryParse(V(r, "Ordinal"), out var ordinal)) throw new InvalidOperationException("Ground-truth Ordinal is invalid."); if (!Enum.TryParse<GroundTruthStatus>(V(r, "GroundTruthStatus"), true, out var status)) throw new InvalidOperationException($"GroundTruthStatus is invalid for ordinal {ordinal}."); result.Add(new GroundTruthLabelRow { RunId = V(r, "RunId"), Ordinal = ordinal, ObservationId = V(r, "ObservationId"), GroundTruthEntityId = N(V(r, "GroundTruthEntityId")), Species = N(V(r, "Species")), Cp = N(V(r, "Cp")), AttackIv = N(V(r, "AttackIv")), DefenseIv = N(V(r, "DefenseIv")), HpIv = N(V(r, "HpIv")), Nickname = N(V(r, "Nickname")), GroundTruthStatus = status, GroundTruthSource = V(r, "GroundTruthSource"), ReviewerNote = N(V(r, "ReviewerNote")), DetailsEvidence = N(V(r, "DetailsEvidence")), AppraisalEvidence = N(V(r, "AppraisalEvidence")), ScannerSpecies = N(V(r, "ScannerSpecies")), ScannerCp = N(V(r, "ScannerCp")), ScannerAttackIv = N(V(r, "ScannerAttackIv")), ScannerDefenseIv = N(V(r, "ScannerDefenseIv")), ScannerHpIv = N(V(r, "ScannerHpIv")), ScannerNickname = N(V(r, "ScannerNickname")) }); } return result; }
    private static string? N(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static void ValidateLabels(IReadOnlyList<GroundTruthLabelRow> rows) { if (rows.Count == 0) throw new InvalidOperationException("Ground-truth CSV has no rows."); var duplicate = rows.GroupBy(x => Key(x.RunId, x.Ordinal)).FirstOrDefault(x => x.Count() > 1); if (duplicate is not null) throw new InvalidOperationException($"Duplicate ground-truth row: {duplicate.Key}."); foreach (var row in rows) { if (string.IsNullOrWhiteSpace(row.GroundTruthSource)) throw new InvalidOperationException($"Missing GroundTruthSource for {row.RunId}/{row.Ordinal}."); if (row.GroundTruthStatus == GroundTruthStatus.Verified && string.IsNullOrWhiteSpace(row.GroundTruthEntityId)) throw new InvalidOperationException($"Verified row needs GroundTruthEntityId: {row.RunId}/{row.Ordinal}."); } }
    private static IEnumerable<IReadOnlyList<string>> CsvRecords(string text) { var rows = new List<IReadOnlyList<string>>(); var fields = new List<string>(); var current = new StringBuilder(); var quoted = false; for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { fields.Add(current.ToString()); current.Clear(); } else if ((c == '\n' || c == '\r') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; fields.Add(current.ToString()); current.Clear(); rows.Add(fields.ToArray()); fields = new List<string>(); } else current.Append(c); } if (current.Length > 0 || fields.Count > 0) { fields.Add(current.ToString()); rows.Add(fields.ToArray()); } return rows; }
}
