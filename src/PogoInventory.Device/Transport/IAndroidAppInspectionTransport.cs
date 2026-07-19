using PogoInventory.Device.Models;

namespace PogoInventory.Device.Transport;

public interface IAndroidAppInspectionTransport : IAndroidDeviceTransport
{
    Task StopKnownAppAsync(
        string serial,
        KnownAndroidPackage app,
        CancellationToken cancellationToken = default);

    Task<string> ReadPackageDumpAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default);

    Task<string> ReadPackagePathAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default);

    Task<string> ReadProcessIdAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default);

    Task<string> ReadRecentLogcatAsync(
        string serial,
        int maximumLines,
        CancellationToken cancellationToken = default);

    Task<string> ReadAccessibilityStateAsync(
        string serial,
        CancellationToken cancellationToken = default);

    Task<string> ReadAppOpsAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default);

    Task<string> ReadActivityServicesAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default);
}
