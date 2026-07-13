using System.ComponentModel;
using System.Diagnostics;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;

namespace PogoInventory.Device.Adb;

public sealed class AdbProcessRunner : IAdbProcessRunner
{
    private readonly string _adbPath;
    private readonly IDeviceLog _log;

    public AdbProcessRunner(string adbPath, IDeviceLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbPath);
        _adbPath = adbPath;
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<AdbProcessResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var commandText = BuildCommandText(arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = _adbPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            _log.Write(
                DeviceLogLevel.Debug,
                "adb.command.start",
                "Starting read-only ADB command.",
                new Dictionary<string, string> { ["command"] = commandText });

            if (!process.Start())
            {
                throw new DeviceHarnessException(
                    DeviceErrorCode.AdbStartFailed,
                    "ADB process did not start.",
                    commandText);
            }
        }
        catch (Win32Exception exception)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.AdbNotFound,
                $"ADB could not be started from '{_adbPath}'. Install Android Platform Tools or provide --adb with the full adb.exe path.",
                commandText,
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.AdbStartFailed,
                "ADB process could not be started.",
                commandText,
                exception);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var stdoutTask = ReadAllBytesAsync(
            process.StandardOutput.BaseStream,
            timeoutSource.Token);
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _log.Write(
                process.ExitCode == 0 ? DeviceLogLevel.Debug : DeviceLogLevel.Warning,
                "adb.command.complete",
                "ADB command completed.",
                new Dictionary<string, string>
                {
                    ["command"] = commandText,
                    ["exitCode"] = process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["stdoutBytes"] = stdout.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });

            return new AdbProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr
            };
        }
        catch (OperationCanceledException exception)
        {
            TryKill(process);
            await ObserveAsync(stdoutTask, stderrTask);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new DeviceHarnessException(
                DeviceErrorCode.CommandTimedOut,
                $"ADB command exceeded the timeout of {timeout.TotalSeconds:0.##} seconds.",
                commandText,
                exception);
        }
        catch (IOException exception)
        {
            TryKill(process);
            await ObserveAsync(stdoutTask, stderrTask);
            throw new DeviceHarnessException(
                DeviceErrorCode.CommandFailed,
                "ADB output could not be read.",
                commandText,
                exception);
        }
    }

    private string BuildCommandText(IReadOnlyList<string> arguments) =>
        string.Join(
            " ",
            new[] { _adbPath }.Concat(arguments.Select(QuoteArgument)));

    private static string QuoteArgument(string value) =>
        value.Any(char.IsWhiteSpace)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;

    private static async Task<byte[]> ReadAllBytesAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken);
        return buffer.ToArray();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (SystemException)
        {
        }
    }

    private static async Task ObserveAsync(
        Task<byte[]> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            await stdoutTask;
        }
        catch (Exception)
        {
        }

        try
        {
            await stderrTask;
        }
        catch (Exception)
        {
        }
    }
}
