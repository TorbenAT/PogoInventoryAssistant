using PogoInventory.Core.Models;
using PogoInventory.Observations.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public sealed record RealScanObservationRecord
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required int Sequence { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public ScreenState DetectedState { get; init; }
    public required double StateConfidence { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required string IdentityFingerprintSha256 { get; init; }
    public required string ProviderName { get; init; }
    public string? ProviderVersion { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public required double AppraisalConfidence { get; init; }
    public required string AppraisalStatus { get; init; }
    public CalcyObservationStatus ProviderStatus { get; init; }
    public string? RawProviderOutput { get; init; }
    public string? RawProviderOutputSha256 { get; init; }
    public required PokemonVariantIdentity VariantIdentity { get; init; }
    public int? SpeciesId => VariantIdentity.SpeciesId;
    public string? SpeciesName => VariantIdentity.SpeciesName;
    public string? FormId => VariantIdentity.FormId;
    public string? FormName => VariantIdentity.FormName;
    public string? CostumeId => VariantIdentity.CostumeId;
    public string? CostumeName => VariantIdentity.CostumeName;
    public string? BackgroundId => VariantIdentity.BackgroundId;
    public string? BackgroundName => VariantIdentity.BackgroundName;
    public string? GenderVariant => VariantIdentity.GenderVariant;
    public bool? IsShiny => VariantIdentity.IsShiny;
    public string? ShadowState => VariantIdentity.ShadowState;
    public string? LuckyState => VariantIdentity.LuckyState;
    public string? DynamaxState => VariantIdentity.DynamaxState;
    public IdentityConfidence VariantIdentityConfidence =>
        VariantIdentity.VariantIdentityConfidence;
    public IReadOnlyList<string> MissingVariantFields =>
        VariantIdentity.MissingVariantFields;
    public int? Cp { get; init; }
    public bool? IsFavorite { get; init; }
    public bool? HasSpecialMove { get; init; }
    public bool? IsXxl { get; init; }
    public bool? IsXxs { get; init; }
    public DateOnly? CatchDate { get; init; }
    public string? CatchLocation { get; init; }
    public required string ObservationStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
    public required PokemonInstanceEvidence InstanceEvidence { get; init; }
    public IReadOnlyCollection<string> EvidenceReferences { get; init; } =
        Array.Empty<string>();
}

public sealed record RealScanDecisionRow
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required int Sequence { get; init; }
    public required string InstanceEvidenceKey { get; init; }
    public string? VariantKey { get; init; }
    public string? Species { get; init; }
    public string? Form { get; init; }
    public string? Costume { get; init; }
    public string? Background { get; init; }
    public bool? Shiny { get; init; }
    public string? ShadowState { get; init; }
    public string? DynamaxState { get; init; }
    public int? Cp { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public IdentityConfidence VariantIdentityConfidence { get; init; }
    public IdentityConfidence ProtectionDataConfidence { get; init; }
    public required DecisionCategory Recommendation { get; init; }
    public required double Confidence { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyCollection<string> MissingEvidence { get; init; } =
        Array.Empty<string>();
    public int? BetterDuplicateSequence { get; init; }
}

public sealed record RealScanRunManifest
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string RunId { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string SourceCheckpointSha256 { get; init; }
    public required string DeviceSerial { get; init; }
    public required string DeviceProfileHash { get; init; }
    public required string AutomationProfileHash { get; init; }
    public required string ScreenProfileHash { get; init; }
    public required int Scanned { get; init; }
    public required int UniqueChangedFrames { get; init; }
    public required int SwipesSucceeded { get; init; }
    public required int UnknownStops { get; init; }
    public required int CandidateObservations { get; init; }
    public required int IncompleteObservations { get; init; }
    public required int CompleteObservations { get; init; }
    public required int TransferActions { get; init; }
    public required bool VariantSchemaReady { get; init; }
    public required int ExactVariantIdentities { get; init; }
    public required int Keep { get; init; }
    public required int Review { get; init; }
    public required int Delete { get; init; }
    public required bool RealPhoneDemoPassed { get; init; }
}

public sealed record RealScanExportResult
{
    public required RealScanRunManifest Manifest { get; init; }
    public required string ManifestPath { get; init; }
    public required string ReportPath { get; init; }
    public required string DecisionPlanPath { get; init; }
    public required string CalibrationJsonPath { get; init; }
    public required string CalibrationMarkdownPath { get; init; }
    public required int CalibrationCases { get; init; }
    public required bool CalibrationStable { get; init; }
}
