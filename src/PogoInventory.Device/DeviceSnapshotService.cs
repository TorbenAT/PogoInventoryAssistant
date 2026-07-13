using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;

namespace PogoInventory.Device;

public sealed class DeviceSnapshotService
{
    private const string ManifestSchemaVersion = "1.0";
    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAndroidDeviceTransport _transport;
    private readonly string _harnessVersion;
    private readonly IDeviceLog _log;

    public DeviceSnapshotService(
        IAndroidDeviceTransport transport,
        string harnessVersion,
        IDeviceLog? log = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentException.ThrowIfNullOrWhiteSpace(harnessVersion);
        _harnessVersion = harnessVersion;
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<DeviceSnapshotResult> CaptureAsync(
        string outputDirectory,
        string? requestedSerial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var devices = await _transport.ListDevicesAsync(cancellationToken);
        var selectedDevice = SelectDevice(devices, requestedSerial);

        _log.Write(
            DeviceLogLevel.Information,
            "device.snapshot.selected",
            "Selected authorised Android device.",
            new Dictionary<string, string>
            {
                ["serial"] = selectedDevice.Serial,
                ["model"] = selectedDevice.Model ?? "unknown"
            });

        var metadata = await _transport.ReadMetadataAsync(
            selectedDevice.Serial,
            cancellationToken);

        if (!string.Equals(metadata.Serial, selectedDevice.Serial, StringComparison.Ordinal))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.InvalidAdbOutput,
                $"Device metadata serial '{metadata.Serial}' did not match selected serial '{selectedDevice.Serial}'.");
        }

        var screenshot = await _transport.CaptureScreenshotPngAsync(
            selectedDevice.Serial,
            cancellationToken);

        ValidateScreenshot(screenshot);
        var screenshotHash = Convert.ToHexString(SHA256.HashData(screenshot)).ToLowerInvariant();

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var screenshotPath = Path.Combine(fullOutputDirectory, "screen.png");
        var metadataPath = Path.Combine(fullOutputDirectory, "device-metadata.json");
        var manifestPath = Path.Combine(fullOutputDirectory, "device-snapshot.json");

        var manifest = new DeviceCaptureManifest
        {
            SchemaVersion = ManifestSchemaVersion,
            HarnessVersion = _harnessVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Device = metadata,
            ScreenshotFileName = Path.GetFileName(screenshotPath),
            ScreenshotLengthBytes = screenshot.LongLength,
            ScreenshotSha256 = screenshotHash
        };

        try
        {
            Directory.CreateDirectory(fullOutputDirectory);
            await WriteSnapshotFilesAsync(
                screenshotPath,
                screenshot,
                metadataPath,
                JsonSerializer.Serialize(metadata, JsonOptions),
                manifestPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);
        }
        catch (IOException exception)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.IoFailure,
                $"Could not write the device snapshot to '{fullOutputDirectory}'.",
                innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.IoFailure,
                $"Access was denied while writing the device snapshot to '{fullOutputDirectory}'.",
                innerException: exception);
        }

        _log.Write(
            DeviceLogLevel.Information,
            "device.snapshot.complete",
            "Read-only device snapshot completed.",
            new Dictionary<string, string>
            {
                ["serial"] = selectedDevice.Serial,
                ["output"] = fullOutputDirectory,
                ["sha256"] = screenshotHash
            });

        return new DeviceSnapshotResult
        {
            Device = selectedDevice,
            Metadata = metadata,
            ScreenshotPath = screenshotPath,
            MetadataPath = metadataPath,
            ManifestPath = manifestPath,
            ScreenshotSha256 = screenshotHash
        };
    }

    public static AndroidDeviceDescriptor SelectDevice(
        IReadOnlyList<AndroidDeviceDescriptor> devices,
        string? requestedSerial = null)
    {
        ArgumentNullException.ThrowIfNull(devices);

        if (!string.IsNullOrWhiteSpace(requestedSerial))
        {
            var requested = devices.FirstOrDefault(x =>
                string.Equals(x.Serial, requestedSerial, StringComparison.Ordinal));

            if (requested is null)
            {
                throw new DeviceHarnessException(
                    DeviceErrorCode.RequestedDeviceNotFound,
                    $"Requested Android device '{requestedSerial}' was not found.");
            }

            if (!requested.IsAuthorized)
            {
                throw new DeviceHarnessException(
                    DeviceErrorCode.RequestedDeviceNotAuthorized,
                    $"Requested Android device '{requestedSerial}' is in state {requested.State}, not Authorized.");
            }

            return requested;
        }

        var authorized = devices.Where(x => x.IsAuthorized).ToList();
        if (authorized.Count == 0)
        {
            var states = devices.Count == 0
                ? "no devices reported"
                : string.Join(
                    ", ",
                    devices.Select(x => $"{x.Serial}:{x.State}"));

            throw new DeviceHarnessException(
                DeviceErrorCode.NoAuthorizedDevice,
                $"Exactly one authorised Android device is required, but none were available ({states}).");
        }

        if (authorized.Count > 1)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.MultipleAuthorizedDevices,
                $"Exactly one authorised Android device is required, but {authorized.Count} were found: {string.Join(", ", authorized.Select(x => x.Serial))}. Use --serial to select one explicitly.");
        }

        return authorized[0];
    }

    private static void ValidateScreenshot(IReadOnlyList<byte> screenshot)
    {
        if (screenshot.Count < 24 ||
            !PngSignature.Select((value, index) => screenshot[index] == value).All(x => x))
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.InvalidScreenshot,
                "Captured screenshot is not a valid PNG stream.");
        }
    }

    private static async Task WriteSnapshotFilesAsync(
        string screenshotPath,
        byte[] screenshot,
        string metadataPath,
        string metadataJson,
        string manifestPath,
        string manifestJson,
        CancellationToken cancellationToken)
    {
        var suffix = $".tmp-{Guid.NewGuid():N}";
        var screenshotTemporaryPath = screenshotPath + suffix;
        var metadataTemporaryPath = metadataPath + suffix;
        var manifestTemporaryPath = manifestPath + suffix;

        try
        {
            await File.WriteAllBytesAsync(
                screenshotTemporaryPath,
                screenshot,
                cancellationToken);
            await File.WriteAllTextAsync(
                metadataTemporaryPath,
                metadataJson,
                cancellationToken);
            await File.WriteAllTextAsync(
                manifestTemporaryPath,
                manifestJson,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // The manifest is the commit marker. Remove the old one before replacing
            // data files, and move the new manifest only after the other files succeed.
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            File.Move(screenshotTemporaryPath, screenshotPath, overwrite: true);
            File.Move(metadataTemporaryPath, metadataPath, overwrite: true);
            File.Move(manifestTemporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            DeleteTemporaryFile(screenshotTemporaryPath);
            DeleteTemporaryFile(metadataTemporaryPath);
            DeleteTemporaryFile(manifestTemporaryPath);
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
