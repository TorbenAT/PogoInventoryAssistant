using System.Globalization;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Models;

namespace PogoInventory.Device.Transport;

public sealed class AdbAndroidDeviceTransport : IAndroidAutomationTransport
{
    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly IAdbProcessRunner _runner;
    private readonly DeviceHarnessOptions _options;
    private readonly IDeviceLog _log;

    public AdbAndroidDeviceTransport(
        IAdbProcessRunner runner,
        DeviceHarnessOptions options,
        IDeviceLog? log = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            new[] { "devices", "-l" },
            "list devices",
            cancellationToken);

        var devices = AdbOutputParser.ParseDeviceList(result.StandardOutputText);
        _log.Write(
            DeviceLogLevel.Information,
            "device.discovery.complete",
            "ADB device discovery completed.",
            new Dictionary<string, string>
            {
                ["count"] = devices.Count.ToString(CultureInfo.InvariantCulture),
                ["authorized"] = devices.Count(x => x.IsAuthorized).ToString(CultureInfo.InvariantCulture)
            });
        return devices;
    }

    public async Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var propertiesResult = await RunAsync(
            ForDevice(serial, "shell", "getprop"),
            "read Android properties",
            cancellationToken);
        var screenResult = await RunAsync(
            ForDevice(serial, "shell", "wm", "size"),
            "read screen size",
            cancellationToken);
        var batteryResult = await RunAsync(
            ForDevice(serial, "shell", "dumpsys", "battery"),
            "read battery state",
            cancellationToken);

        var properties = AdbOutputParser.ParseGetProperties(propertiesResult.StandardOutputText);
        var screen = AdbOutputParser.ParseScreenInfo(screenResult.StandardOutputText);
        var battery = AdbOutputParser.ParseBatteryInfo(batteryResult.StandardOutputText);

        return new AndroidDeviceMetadata
        {
            Serial = serial,
            Manufacturer = Get(properties, "ro.product.manufacturer"),
            Model = Get(properties, "ro.product.model"),
            Product = Get(properties, "ro.product.name"),
            DeviceName = Get(properties, "ro.product.device"),
            AndroidVersion = Get(properties, "ro.build.version.release"),
            ApiLevel = ParseInt(Get(properties, "ro.build.version.sdk")),
            BuildFingerprint = Get(properties, "ro.build.fingerprint"),
            Screen = screen,
            Battery = battery,
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var result = await RunAsync(
            ForDevice(serial, "exec-out", "screencap", "-p"),
            "capture screenshot",
            cancellationToken);

        if (!HasPngSignature(result.StandardOutput))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.InvalidScreenshot,
                "ADB screenshot output was empty or did not contain a valid PNG signature.",
                "adb -s <serial> exec-out screencap -p");
        }

        return result.StandardOutput;
    }


    public async Task TapAsync(
        string serial,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));

        await RunAsync(
            ForDevice(
                serial,
                "shell",
                "input",
                "tap",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture)),
            "tap the Android screen",
            cancellationToken);
    }

    public async Task SwipeAsync(
        string serial,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ValidateCoordinate(startX, nameof(startX));
        ValidateCoordinate(startY, nameof(startY));
        ValidateCoordinate(endX, nameof(endX));
        ValidateCoordinate(endY, nameof(endY));

        if (durationMilliseconds is < 50 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMilliseconds),
                "Swipe duration must be between 50 and 5000 milliseconds.");
        }

        await RunAsync(
            ForDevice(
                serial,
                "shell",
                "input",
                "swipe",
                startX.ToString(CultureInfo.InvariantCulture),
                startY.ToString(CultureInfo.InvariantCulture),
                endX.ToString(CultureInfo.InvariantCulture),
                endY.ToString(CultureInfo.InvariantCulture),
                durationMilliseconds.ToString(CultureInfo.InvariantCulture)),
            "swipe the Android screen",
            cancellationToken);
    }

    private async Task<AdbProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await _runner.ExecuteAsync(
            arguments,
            _options.CommandTimeout,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError)
                ? "No error text was returned by ADB."
                : result.StandardError.Trim();

            throw new DeviceHarnessException(
                DeviceErrorCode.CommandFailed,
                $"ADB failed to {operation}: {error}",
                string.Join(" ", arguments));
        }

        return result;
    }


    private static void ValidateCoordinate(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Android input coordinates cannot be negative.");
        }
    }

    private static string[] ForDevice(string serial, params string[] command) =>
        new[] { "-s", serial }.Concat(command).ToArray();

    private static string? Get(
        IReadOnlyDictionary<string, string> properties,
        string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static bool HasPngSignature(IReadOnlyList<byte> bytes) =>
        bytes.Count >= PngSignature.Length &&
        PngSignature.Select((value, index) => bytes[index] == value).All(x => x);
}
