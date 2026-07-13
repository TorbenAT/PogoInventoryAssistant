namespace PogoInventory.Vision.Models;

public sealed record ScreenFingerprintResult
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string SourceImage { get; init; }
    public required NormalizedRegion Region { get; init; }
    public required FingerprintMode Mode { get; init; }
    public required int FingerprintWidth { get; init; }
    public required int FingerprintHeight { get; init; }
    public required string FingerprintBase64 { get; init; }
}
