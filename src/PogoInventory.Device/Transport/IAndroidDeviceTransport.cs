using PogoInventory.Device.Models;

namespace PogoInventory.Device.Transport;

public interface IAndroidDeviceTransport
{
    Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default);

    Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default);

    Task<string> CaptureUiHierarchyAsync(
        string serial,
        CancellationToken cancellationToken = default);
}
