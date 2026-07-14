using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;

namespace PogoInventory.Device.Transport;

public sealed class FakeAndroidDeviceTransport : IAndroidAutomationTransport
{
    private const string DefaultScreenshotBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAADklEQVR4nGP4DwYMEAoAU7oL9ZisIGcAAAAASUVORK5CYII=";

    private readonly IReadOnlyList<AndroidDeviceDescriptor> _devices;
    private readonly IReadOnlyDictionary<string, AndroidDeviceMetadata> _metadataBySerial;
    private readonly byte[] _screenshotPng;

    public FakeAndroidDeviceTransport(
        IReadOnlyList<AndroidDeviceDescriptor> devices,
        IReadOnlyDictionary<string, AndroidDeviceMetadata> metadataBySerial,
        byte[] screenshotPng)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _metadataBySerial = metadataBySerial ?? throw new ArgumentNullException(nameof(metadataBySerial));
        _screenshotPng = screenshotPng?.ToArray() ?? throw new ArgumentNullException(nameof(screenshotPng));
    }

    public static FakeAndroidDeviceTransport CreateSingleAuthorized()
    {
        var device = CreateDescriptor("FAKE-001");
        var metadata = CreateMetadata(device.Serial);
        return new FakeAndroidDeviceTransport(
            new[] { device },
            new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
            {
                [device.Serial] = metadata
            },
            CreateDefaultScreenshotPng());
    }

    public static AndroidDeviceDescriptor CreateDescriptor(string serial) =>
        new()
        {
            Serial = serial,
            State = AndroidDeviceState.Authorized,
            Product = "fake product",
            Model = "Fake Android",
            Device = "fake-device",
            TransportId = "1"
        };

    public static AndroidDeviceMetadata CreateMetadata(string serial) =>
        new()
        {
            Serial = serial,
            Manufacturer = "Pogo Inventory Assistant",
            Model = "Fake Android",
            Product = "fake product",
            DeviceName = "fake-device",
            AndroidVersion = "16",
            ApiLevel = 36,
            BuildFingerprint = "fake/fingerprint/0.9.0",
            Screen = new AndroidScreenInfo
            {
                PhysicalWidth = 1080,
                PhysicalHeight = 2400
            },
            Battery = new AndroidBatteryInfo
            {
                LevelPercent = 85,
                TemperatureCelsius = 29.5m,
                StatusCode = 2,
                StatusName = "Charging",
                UsbPowered = true,
                Present = true,
                Technology = "Li-ion"
            },
            CapturedAtUtc = new DateTimeOffset(2026, 7, 13, 20, 0, 0, TimeSpan.Zero)
        };

    public static byte[] CreateDefaultScreenshotPng() =>
        Convert.FromBase64String(DefaultScreenshotBase64);

    public Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_devices);
    }

    public Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_metadataBySerial.TryGetValue(serial, out var metadata))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.RequestedDeviceNotFound,
                $"Fake device '{serial}' has no metadata.");
        }

        return Task.FromResult(metadata);
    }

    public Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_devices.Any(x => string.Equals(x.Serial, serial, StringComparison.Ordinal)))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.RequestedDeviceNotFound,
                $"Fake device '{serial}' was not found.");
        }

        return Task.FromResult(_screenshotPng.ToArray());
    }
    public Task TapAsync(
        string serial,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureKnownDevice(serial);
        if (x < 0 || y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinates cannot be negative.");
        }

        return Task.CompletedTask;
    }

    public Task SwipeAsync(
        string serial,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureKnownDevice(serial);
        if (startX < 0 || startY < 0 || endX < 0 || endY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startX), "Coordinates cannot be negative.");
        }

        if (durationMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMilliseconds),
                "Swipe duration must be positive.");
        }

        return Task.CompletedTask;
    }

    private void EnsureKnownDevice(string serial)
    {
        if (!_metadataBySerial.ContainsKey(serial))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.RequestedDeviceNotFound,
                $"Fake device '{serial}' has no metadata.");
        }
    }

}
