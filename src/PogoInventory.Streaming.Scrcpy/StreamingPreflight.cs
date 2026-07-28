using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PogoInventory.Streaming.Scrcpy;

public enum StreamingPreflightReasonCode
{
    Ready,
    DotNetUnavailable,
    AdbUnavailable,
    DeviceNotFound,
    DeviceUnauthorized,
    DeviceOffline,
    ScrcpyServerMissing,
    ScrcpyVersionMismatch,
    FfmpegUnavailable,
    OutputDirectoryUnavailable,
    DisplayDimensionsUnavailable
}

public sealed record PreflightCheck(string Name, bool Passed, StreamingPreflightReasonCode ReasonCode, string Detail);

public sealed record StreamingPreflightReport
{
    public string DeviceSerial { get; init; } = string.Empty;
    public string AdbPath { get; init; } = string.Empty;
    public string? AdbVersion { get; init; }
    public string? FfmpegVersion { get; init; }
    public string ScrcpyServerPath { get; init; } = string.Empty;
    public string ExpectedScrcpyServerVersion { get; init; } = string.Empty;
    public DisplayDimensions? Display { get; init; }
    public ResolvedStreamDimensions? Stream { get; init; }
    public bool InputCommandsSent { get; init; }
    public IReadOnlyList<PreflightCheck> Checks { get; init; } = Array.Empty<PreflightCheck>();
    public StreamingPreflightReasonCode FinalReasonCode { get; init; } = StreamingPreflightReasonCode.Ready;
    public bool IsReady => FinalReasonCode == StreamingPreflightReasonCode.Ready;
}

public sealed record StreamingPreflightOptions
{
    public required string DeviceSerial { get; init; }
    public string AdbPath { get; init; } = "adb";
    public string FfmpegPath { get; init; } = "ffmpeg";
    public required string ScrcpyServerPath { get; init; }
    public string ExpectedScrcpyServerVersion { get; init; } = "4.0";
    public string OutputDirectory { get; init; } = "evidence-preflight";
    public int MaxSize { get; init; } = 1920;
    public int? RequestedWidth { get; init; }
    public int? RequestedHeight { get; init; }
}

public sealed class StreamingPreflightRunner
{
    public async Task<StreamingPreflightReport> RunAsync(StreamingPreflightOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var checks = new List<PreflightCheck>();
        var adb = await ProbeExecutableAsync(options.AdbPath, "version", cancellationToken).ConfigureAwait(false);
        checks.Add(new("ADB", adb.Passed, adb.Passed ? StreamingPreflightReasonCode.Ready : StreamingPreflightReasonCode.AdbUnavailable, adb.Detail));
        var ffmpeg = await ProbeExecutableAsync(options.FfmpegPath, "-version", cancellationToken).ConfigureAwait(false);
        checks.Add(new("FFmpeg", ffmpeg.Passed, ffmpeg.Passed ? StreamingPreflightReasonCode.Ready : StreamingPreflightReasonCode.FfmpegUnavailable, ffmpeg.Detail));

        var serverPassed = File.Exists(options.ScrcpyServerPath);
        var serverVersionPassed = serverPassed && (string.IsNullOrWhiteSpace(options.ExpectedScrcpyServerVersion) || Path.GetFileName(options.ScrcpyServerPath).Contains(options.ExpectedScrcpyServerVersion, StringComparison.OrdinalIgnoreCase));
        checks.Add(new("scrcpy server", serverPassed, serverPassed ? StreamingPreflightReasonCode.Ready : StreamingPreflightReasonCode.ScrcpyServerMissing, serverPassed ? "File exists." : "Server JAR was not found."));
        checks.Add(new("scrcpy server version", serverVersionPassed, serverVersionPassed ? StreamingPreflightReasonCode.Ready : StreamingPreflightReasonCode.ScrcpyVersionMismatch, serverVersionPassed ? "Configured version matches the server filename." : "Expected version was not identifiable in the server filename."));

        var device = default((bool Passed, StreamingPreflightReasonCode Reason, string Detail, string Output));
        if (adb.Passed)
        {
            device = await ProbeDeviceAsync(options, cancellationToken).ConfigureAwait(false);
            checks.Add(new("ADB device", device.Passed, device.Reason, device.Detail));
        }
        else
        {
            checks.Add(new("ADB device", false, StreamingPreflightReasonCode.AdbUnavailable, "ADB was not executable."));
        }

        DisplayDimensions? display = null;
        ResolvedStreamDimensions? stream = null;
        if (device.Passed)
        {
            try
            {
                var dimensionsOutput = (await AdbDeviceValidator.RunAsync(options.AdbPath, $"-s \"{options.DeviceSerial}\" shell wm size", cancellationToken).ConfigureAwait(false)).Output;
                display = AdbDisplayDimensionParser.ParseWmSize(dimensionsOutput);
                stream = StreamDimensionResolver.Resolve(display, options.MaxSize, options.RequestedWidth, options.RequestedHeight);
                checks.Add(new("Display dimensions", true, StreamingPreflightReasonCode.Ready, $"{display.Width}x{display.Height} -> {stream.Width}x{stream.Height} ({stream.Source})."));
            }
            catch (StreamTransportException error)
            {
                checks.Add(new("Display dimensions", false, StreamingPreflightReasonCode.DisplayDimensionsUnavailable, error.Failure.Message));
            }
            catch (Exception error)
            {
                checks.Add(new("Display dimensions", false, StreamingPreflightReasonCode.DisplayDimensionsUnavailable, error.Message));
            }
        }
        else
        {
            checks.Add(new("Display dimensions", false, adb.Passed ? device.Reason : StreamingPreflightReasonCode.AdbUnavailable, "Skipped because the device is not ready."));
        }

        var outputPassed = TryVerifyOutputDirectory(options.OutputDirectory, out var outputDetail);
        checks.Add(new("Output directory", outputPassed, outputPassed ? StreamingPreflightReasonCode.Ready : StreamingPreflightReasonCode.OutputDirectoryUnavailable, outputDetail));
        var firstFailure = checks.FirstOrDefault(x => !x.Passed);
        return new StreamingPreflightReport
        {
            DeviceSerial = options.DeviceSerial,
            AdbPath = options.AdbPath,
            AdbVersion = adb.Passed ? adb.Detail : null,
            FfmpegVersion = ffmpeg.Passed ? ffmpeg.Detail : null,
            ScrcpyServerPath = options.ScrcpyServerPath,
            ExpectedScrcpyServerVersion = options.ExpectedScrcpyServerVersion,
            Display = display,
            Stream = stream,
            InputCommandsSent = false,
            Checks = checks,
            FinalReasonCode = firstFailure?.ReasonCode ?? StreamingPreflightReasonCode.Ready
        };
    }

    private static async Task<(bool Passed, string Detail)> ProbeExecutableAsync(string executable, string args, CancellationToken cancellationToken)
    {
        try
        {
            var result = await AdbDeviceValidator.RunAsync(executable, args, cancellationToken).ConfigureAwait(false);
            var detail = (result.Output + result.Error).Trim();
            return (result.ExitCode == 0, detail.Length > 500 ? detail[..500] : detail);
        }
        catch (Exception error)
        {
            return (false, error.Message);
        }
    }

    private static async Task<(bool Passed, StreamingPreflightReasonCode Reason, string Detail, string Output)> ProbeDeviceAsync(StreamingPreflightOptions options, CancellationToken cancellationToken)
    {
        var result = await AdbDeviceValidator.RunAsync(options.AdbPath, "devices", cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return (false, StreamingPreflightReasonCode.AdbUnavailable, result.Error, result.Output);
        }
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).FirstOrDefault(x => x.StartsWith(options.DeviceSerial + "\t", StringComparison.Ordinal));
        if (line is null)
        {
            return (false, StreamingPreflightReasonCode.DeviceNotFound, $"Device '{options.DeviceSerial}' was not found.", result.Output);
        }
        if (line.Contains("\tunauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return (false, StreamingPreflightReasonCode.DeviceUnauthorized, "Device is unauthorized.", result.Output);
        }
        if (line.Contains("\toffline", StringComparison.OrdinalIgnoreCase))
        {
            return (false, StreamingPreflightReasonCode.DeviceOffline, "Device is offline.", result.Output);
        }
        return line.EndsWith("\tdevice", StringComparison.Ordinal) ? (true, StreamingPreflightReasonCode.Ready, "Device is authorized.", result.Output) : (false, StreamingPreflightReasonCode.DeviceNotFound, "Device is not authorized.", result.Output);
    }

    private static bool TryVerifyOutputDirectory(string path, out string detail)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".streaming-preflight-write-test");
            File.WriteAllText(probe, "read-only-preflight");
            File.Delete(probe);
            detail = "Directory exists and is writable.";
            return true;
        }
        catch (Exception error)
        {
            detail = error.Message;
            return false;
        }
    }
}
