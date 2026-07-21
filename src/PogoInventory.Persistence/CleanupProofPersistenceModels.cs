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
