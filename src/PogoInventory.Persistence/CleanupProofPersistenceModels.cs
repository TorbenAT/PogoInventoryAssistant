using PogoInventory.Core.Models;

namespace PogoInventory.Persistence;

public sealed record CleanupProofRunStart
{
    public required string RunId { get; init; }
    public required string SearchQuery { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required string DeviceSerial { get; init; }
    public required int RequestedItems { get; init; }
    public required string SourceDirectory { get; init; }
}

public sealed record CleanupProofObservationRecord
{
    public required string RunId { get; init; }
    public required int Ordinal { get; init; }
    public required string LocalPokemonId { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required PokemonObservation Observation { get; init; }
    public required string ObservationStatus { get; init; }
    public required double IdentityConfidenceValue { get; init; }
    public required double ProtectionConfidenceValue { get; init; }
    public required string StableFingerprint { get; init; }
    public required IReadOnlyList<string> ScreenshotPaths { get; init; }
    public required IReadOnlyList<string> ScreenshotHashes { get; init; }
    public required IReadOnlyList<string> AppraisalEvidence { get; init; }
    public required IReadOnlyDictionary<string, string> FieldEvidenceSources { get; init; }
}

public sealed record CleanupProofSemanticReviewField
{
    public required string? Value { get; init; }
    public required string Source { get; init; }
    public required string EvidencePath { get; init; }
    public required string ReviewNote { get; init; }
}

public sealed record CleanupProofDatabaseRow
{
    public required string RunId { get; init; }
    public required int Ordinal { get; init; }
    public required string LocalPokemonId { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required PokemonObservation Observation { get; init; }
    public required string ObservationStatus { get; init; }
    public required double IdentityConfidenceValue { get; init; }
    public required double ProtectionConfidenceValue { get; init; }
    public required string StableFingerprint { get; init; }
    public required IReadOnlyList<string> ScreenshotPaths { get; init; }
    public required IReadOnlyList<string> ScreenshotHashes { get; init; }
    public required IReadOnlyList<string> AppraisalEvidence { get; init; }
    public required IReadOnlyDictionary<string, string> FieldEvidenceSources { get; init; }
    public required string CurrentRecommendation { get; init; }
    public required string RecommendationReason { get; init; }
    public string? ComparatorLocalPokemonId { get; init; }
    public string? SemanticKey { get; init; }
    public string? SemanticKeyCompleteness { get; init; }
}

/// <summary>
/// A lightweight, run-independent view of a persisted PokemonRecord row, used
/// for offline cross-run semantic identity re-identification. Deliberately
/// omits observation evidence blobs.
/// </summary>
public sealed record PokemonRecordSemanticRow
{
    public required string LocalPokemonId { get; init; }
    public string? SpeciesName { get; init; }
    public int? Cp { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public string? SemanticKey { get; init; }
    public string? SemanticKeyCompleteness { get; init; }
    public required string FirstSeenRunId { get; init; }
    public required string LastSeenRunId { get; init; }
}

public sealed record CleanupProofSqlSummary
{
    public required string IntegrityCheck { get; init; }
    public required long ScanRunCount { get; init; }
    public required long ObservationCount { get; init; }
    public required long PokemonRecordCount { get; init; }
    public required long InventoryEventCount { get; init; }
    public required IReadOnlyDictionary<string, long> RecommendationCounts { get; init; }
}
