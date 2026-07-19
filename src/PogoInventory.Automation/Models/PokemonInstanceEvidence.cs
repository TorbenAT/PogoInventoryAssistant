namespace PogoInventory.Automation.Models;

public sealed record PokemonInstanceEvidence
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ScanRunId { get; init; }
    public required int Sequence { get; init; }
    public required string InstanceEvidenceKey { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required string IdentityFingerprintSha256 { get; init; }
    public string? PreviousIdentityFingerprintSha256 { get; init; }
    public string? NextIdentityFingerprintSha256 { get; init; }
    public required DateTimeOffset CaptureTimestampUtc { get; init; }
    public required string DeviceProfileHash { get; init; }
    public required string NavigationAuditReference { get; init; }
}
