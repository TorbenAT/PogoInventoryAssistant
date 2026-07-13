namespace PogoInventory.Device.Models;

public sealed record DeviceCaptureManifest
{
    public required string SchemaVersion { get; init; }
    public required string HarnessVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required AndroidDeviceMetadata Device { get; init; }
    public required string ScreenshotFileName { get; init; }
    public required long ScreenshotLengthBytes { get; init; }
    public required string ScreenshotSha256 { get; init; }
}
