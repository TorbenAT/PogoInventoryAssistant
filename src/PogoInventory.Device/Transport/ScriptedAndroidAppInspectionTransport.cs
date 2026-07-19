using PogoInventory.Device.Models;

namespace PogoInventory.Device.Transport;

public sealed class ScriptedAndroidAppInspectionTransport : IAndroidAppInspectionTransport
{
    private readonly IReadOnlyList<AndroidDeviceDescriptor> _devices;
    private readonly AndroidDeviceMetadata _metadata;
    private readonly byte[] _screenshot;
    private readonly IReadOnlyDictionary<string, string> _packageDumps;
    private readonly IReadOnlyDictionary<string, string> _packagePaths;
    private readonly IReadOnlyDictionary<string, string> _processIds;
    private readonly IReadOnlyDictionary<string, string> _appOps;
    private readonly IReadOnlyDictionary<string, string> _activityServices;
    private readonly string _logcat;
    private readonly string _accessibilityState;

    public ScriptedAndroidAppInspectionTransport(
        IReadOnlyList<AndroidDeviceDescriptor> devices,
        AndroidDeviceMetadata metadata,
        byte[] screenshot,
        IReadOnlyDictionary<string, string>? packageDumps = null,
        IReadOnlyDictionary<string, string>? packagePaths = null,
        IReadOnlyDictionary<string, string>? processIds = null,
        IReadOnlyDictionary<string, string>? appOps = null,
        IReadOnlyDictionary<string, string>? activityServices = null,
        string? logcat = null,
        string? accessibilityState = null)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _screenshot = screenshot ?? throw new ArgumentNullException(nameof(screenshot));
        _packageDumps = packageDumps ?? new Dictionary<string, string>();
        _packagePaths = packagePaths ?? new Dictionary<string, string>();
        _processIds = processIds ?? new Dictionary<string, string>();
        _appOps = appOps ?? new Dictionary<string, string>();
        _activityServices = activityServices ?? new Dictionary<string, string>();
        _logcat = logcat ?? string.Empty;
        _accessibilityState = accessibilityState ?? string.Empty;
    }

    public List<string> Operations { get; } = new();

    public Task StopKnownAppAsync(
        string serial,
        KnownAndroidPackage app,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        Operations.Add($"stop-known-app:{app}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Operations.Add("devices");
        return Task.FromResult(_devices);
    }

    public Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        Operations.Add("metadata");
        return Task.FromResult(_metadata);
    }

    public Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        Operations.Add("screenshot");
        return Task.FromResult(_screenshot.ToArray());
    }

    public Task<string> CaptureUiHierarchyAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        Operations.Add("ui-hierarchy");
        return Task.FromResult("<hierarchy rotation=\"0\"><node class=\"scripted\" /></hierarchy>");
    }

    public Task<string> ReadPackageDumpAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default) =>
        ReadPackageValueAsync(serial, packageName, _packageDumps, "package-dump", cancellationToken);

    public Task<string> ReadPackagePathAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default) =>
        ReadPackageValueAsync(serial, packageName, _packagePaths, "package-path", cancellationToken);

    public Task<string> ReadProcessIdAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default) =>
        ReadPackageValueAsync(serial, packageName, _processIds, "process-id", cancellationToken);

    public Task<string> ReadRecentLogcatAsync(
        string serial,
        int maximumLines,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        if (maximumLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLines));
        }

        Operations.Add($"logcat:{maximumLines}");
        var lines = _logcat.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        return Task.FromResult(string.Join(
            Environment.NewLine,
            lines.TakeLast(maximumLines)));
    }

    public Task<string> ReadAccessibilityStateAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        Operations.Add("accessibility");
        return Task.FromResult(_accessibilityState);
    }

    public Task<string> ReadAppOpsAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default) =>
        ReadPackageValueAsync(serial, packageName, _appOps, "appops", cancellationToken);

    public Task<string> ReadActivityServicesAsync(
        string serial,
        string packageName,
        CancellationToken cancellationToken = default) =>
        ReadPackageValueAsync(
            serial,
            packageName,
            _activityServices,
            "activity-services",
            cancellationToken);

    private Task<string> ReadPackageValueAsync(
        string serial,
        string packageName,
        IReadOnlyDictionary<string, string> values,
        string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        Operations.Add($"{operation}:{packageName}");
        return Task.FromResult(values.TryGetValue(packageName, out var value)
            ? value
            : string.Empty);
    }

    private void EnsureSerial(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (!string.Equals(_metadata.Serial, serial, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Scripted transport contains serial '{_metadata.Serial}', not '{serial}'.");
        }
    }
}
