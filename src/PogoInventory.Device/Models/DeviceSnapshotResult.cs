namespace PogoInventory.Device.Models;

public sealed record DeviceSnapshotResult
{
    public required AndroidDeviceDescriptor Device { get; init; }
    public required AndroidDeviceMetadata Metadata { get; init; }
    public required string ScreenshotPath { get; init; }
    public required string MetadataPath { get; init; }
    public required string ManifestPath { get; init; }
    public required string ScreenshotSha256 { get; init; }
}
