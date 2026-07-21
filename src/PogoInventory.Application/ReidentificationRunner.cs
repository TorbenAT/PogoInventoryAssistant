using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

/// <summary>
/// The outcome recorded for a single database-B PokemonRecord when matched
/// against every database-A PokemonRecord.
/// </summary>
public sealed record ReidentificationRecordDetail
{
    public required string LocalPokemonId { get; init; }
    public required string Outcome { get; init; }
    public string? MatchedLocalPokemonId { get; init; }
    public IReadOnlyList<string> CandidateLocalPokemonIds { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The offline double-scan re-identification report: how well semantic
/// identity keys re-identify the same real Pokémon across two independent
/// scan-run databases, without any phone access.
/// </summary>
public sealed record ReidentificationReport
{
    public required string DatabaseAPath { get; init; }
    public required string DatabaseBPath { get; init; }
    public required int TotalA { get; init; }
    public required int TotalB { get; init; }
    public required int ComparableA { get; init; }
    public required int ComparableB { get; init; }
    public required int PartialA { get; init; }
    public required int PartialB { get; init; }
    public required int InsufficientA { get; init; }
    public required int InsufficientB { get; init; }
    public required int MatchedCount { get; init; }
    public required int AmbiguousCollisionCount { get; init; }
    public required int UnmatchedCount { get; init; }
    public required double ReMatchRatePercent { get; init; }
    public required double FalseMergeGuardBlockedPercent { get; init; }
    public required IReadOnlyList<ReidentificationRecordDetail> Details { get; init; }
}

/// <summary>
/// Runs the offline, phone-free double-scan re-identification comparison
/// between two cleanup-proof SQLite databases produced by independent scan
/// runs. This is the measurement tool for the cross-run re-identification
/// acceptance test: it never opens a phone connection and never mutates
/// either database.
/// </summary>
public sealed class ReidentificationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ReidentificationReport> RunAsync(
        string databaseAPath,
        string databaseBPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseAPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseBPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var serviceA = new InventoryPersistenceService(databaseAPath);
        var serviceB = new InventoryPersistenceService(databaseBPath);
        var recordsA = await serviceA.LoadAllPokemonRecordsAsync(cancellationToken);
        var recordsB = await serviceB.LoadAllPokemonRecordsAsync(cancellationToken);

        var priorRecords = recordsA.Select(ToSemanticRecord).ToList();

        var details = new List<ReidentificationRecordDetail>(recordsB.Count);
        var matched = 0;
        var ambiguous = 0;
        var unmatched = 0;
        foreach (var row in recordsB)
        {
            var newRecord = ToSemanticRecord(row);
            var result = SemanticIdentityMatcher.Match(priorRecords, newRecord);
            switch (result.Outcome)
            {
                case SemanticMatchOutcome.Matched:
                    matched++;
                    break;
                case SemanticMatchOutcome.AmbiguousCollision:
                    ambiguous++;
                    break;
                default:
                    unmatched++;
                    break;
            }

            details.Add(new ReidentificationRecordDetail
            {
                LocalPokemonId = row.LocalPokemonId,
                Outcome = result.Outcome.ToString(),
                MatchedLocalPokemonId = result.MatchedLocalPokemonId,
                CandidateLocalPokemonIds = result.CandidateLocalPokemonIds
            });
        }

        var comparableA = recordsA.Count(row => Completeness(row) == SemanticKeyCompleteness.Comparable);
        var comparableB = recordsB.Count(row => Completeness(row) == SemanticKeyCompleteness.Comparable);
        var partialA = recordsA.Count(row => Completeness(row) == SemanticKeyCompleteness.Partial);
        var partialB = recordsB.Count(row => Completeness(row) == SemanticKeyCompleteness.Partial);
        var insufficientA = recordsA.Count(row => Completeness(row) == SemanticKeyCompleteness.Insufficient);
        var insufficientB = recordsB.Count(row => Completeness(row) == SemanticKeyCompleteness.Insufficient);

        var report = new ReidentificationReport
        {
            DatabaseAPath = Path.GetFullPath(databaseAPath),
            DatabaseBPath = Path.GetFullPath(databaseBPath),
            TotalA = recordsA.Count,
            TotalB = recordsB.Count,
            ComparableA = comparableA,
            ComparableB = comparableB,
            PartialA = partialA,
            PartialB = partialB,
            InsufficientA = insufficientA,
            InsufficientB = insufficientB,
            MatchedCount = matched,
            AmbiguousCollisionCount = ambiguous,
            UnmatchedCount = unmatched,
            ReMatchRatePercent = recordsB.Count == 0 ? 0 : matched * 100.0 / recordsB.Count,
            // The guard never auto-merges ambiguous collisions or Insufficient/Partial
            // keys. This is the share of database-B records the guard deliberately
            // refused to silently match, out of every record that was NOT a clean match.
            FalseMergeGuardBlockedPercent = (ambiguous + unmatched) == 0
                ? 0
                : ambiguous * 100.0 / (ambiguous + unmatched),
            Details = details
        };

        await WriteReportsAsync(report, outputDirectory, cancellationToken);
        return report;
    }

    private static SemanticIdentityRecord ToSemanticRecord(PokemonRecordSemanticRow row) => new()
    {
        LocalPokemonId = row.LocalPokemonId,
        FullKey = row.SemanticKey ?? string.Empty,
        Completeness = Completeness(row)
    };

    private static SemanticKeyCompleteness Completeness(PokemonRecordSemanticRow row) =>
        Enum.TryParse<SemanticKeyCompleteness>(row.SemanticKeyCompleteness, ignoreCase: true, out var value)
            ? value
            : SemanticKeyCompleteness.Insufficient;

    private static async Task WriteReportsAsync(
        ReidentificationReport report,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);

        await File.WriteAllTextAsync(
            Path.Combine(output, "reidentification-report.json"),
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);

        var markdown = new StringBuilder();
        markdown.AppendLine("# Cross-run re-identification report");
        markdown.AppendLine();
        markdown.AppendLine($"- Database A: `{report.DatabaseAPath}`");
        markdown.AppendLine($"- Database B: `{report.DatabaseBPath}`");
        markdown.AppendLine($"- Total A / Total B: {report.TotalA} / {report.TotalB}");
        markdown.AppendLine($"- Comparable A / Comparable B: {report.ComparableA} / {report.ComparableB}");
        markdown.AppendLine($"- Partial A / Partial B: {report.PartialA} / {report.PartialB}");
        markdown.AppendLine($"- Insufficient (species unknown) A / B: {report.InsufficientA} / {report.InsufficientB}");
        markdown.AppendLine($"- Matched: {report.MatchedCount}");
        markdown.AppendLine($"- Ambiguous collisions (never auto-merged): {report.AmbiguousCollisionCount}");
        markdown.AppendLine($"- Unmatched: {report.UnmatchedCount}");
        markdown.AppendLine($"- Re-match rate: {report.ReMatchRatePercent:F2}%");
        markdown.AppendLine($"- False-merge guard blocked share of non-matches: {report.FalseMergeGuardBlockedPercent:F2}%");
        markdown.AppendLine();
        markdown.AppendLine("## Per-record outcomes (database B)");
        markdown.AppendLine();
        markdown.AppendLine("| LocalPokemonId | Outcome | Matched LocalPokemonId | Candidates |");
        markdown.AppendLine("|---|---|---|---|");
        foreach (var detail in report.Details)
        {
            markdown.AppendLine(
                $"| {detail.LocalPokemonId} | {detail.Outcome} | {detail.MatchedLocalPokemonId ?? ""} | {string.Join(';', detail.CandidateLocalPokemonIds)} |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(output, "reidentification-report.md"),
            markdown.ToString(),
            cancellationToken);
    }
}
