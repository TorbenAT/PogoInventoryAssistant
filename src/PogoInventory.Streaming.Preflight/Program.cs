using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Streaming.Scrcpy;

var arguments = Arguments.Parse(args);
var report = await new StreamingPreflightRunner().RunAsync(new StreamingPreflightOptions
{
    DeviceSerial = arguments.Device,
    AdbPath = arguments.Adb,
    FfmpegPath = arguments.Ffmpeg,
    ScrcpyServerPath = arguments.Server,
    ExpectedScrcpyServerVersion = arguments.ServerVersion,
    OutputDirectory = arguments.Output,
    MaxSize = arguments.MaxSize,
    RequestedWidth = arguments.Width,
    RequestedHeight = arguments.Height
});

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
});
Directory.CreateDirectory(arguments.Output);
await File.WriteAllTextAsync(Path.Combine(arguments.Output, "streaming-preflight.json"), json);
await File.WriteAllTextAsync(Path.Combine(arguments.Output, "streaming-preflight.md"), MarkdownReport.Create(report));
Console.WriteLine(json);
return report.IsReady ? 0 : 2;

sealed class Arguments
{
    public required string Device { get; init; }
    public string Adb { get; init; } = "adb";
    public string Ffmpeg { get; init; } = "ffmpeg";
    public string Server { get; init; } = "scrcpy-server-v4.0";
    public string ServerVersion { get; init; } = "4.0";
    public string Output { get; init; } = "evidence-preflight";
    public int MaxSize { get; init; } = 1920;
    public int? Width { get; init; }
    public int? Height { get; init; }

    public static Arguments Parse(string[] args)
    {
        string? Get(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        var device = Get("--device");
        if (string.IsNullOrWhiteSpace(device)) throw new ArgumentException("--device is required.");
        var width = ParseOptional(Get("--width"), "--width");
        var height = ParseOptional(Get("--height"), "--height");
        if (width.HasValue != height.HasValue) throw new ArgumentException("--width and --height must be supplied together.");
        return new Arguments
        {
            Device = device,
            Adb = Get("--adb") ?? "adb",
            Ffmpeg = Get("--ffmpeg") ?? "ffmpeg",
            Server = Get("--server") ?? "scrcpy-server-v4.0",
            ServerVersion = Get("--server-version") ?? "4.0",
            Output = Get("--output") ?? "evidence-preflight",
            MaxSize = ParsePositive(Get("--max-size") ?? "1920", "--max-size"),
            Width = width,
            Height = height
        };
    }

    private static int? ParseOptional(string? value, string name) => value is null ? null : ParsePositive(value, name);
    private static int ParsePositive(string value, string name) => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : throw new ArgumentException($"{name} must be a positive integer.");
}

static class MarkdownReport
{
    public static string Create(StreamingPreflightReport report)
    {
        var lines = new List<string> { "# Streaming Vision preflight", "", $"- Device: `{report.DeviceSerial}`", $"- Device model: `{report.DeviceModel ?? "Unknown"}`", $"- Final reason: `{report.FinalReasonCode}`", $"- Read-only configuration: `{report.ReadOnlyConfiguration}`", $"- Input commands sent: `{(report.InputCommandsSent ? 1 : 0)}`", "", "| Check | Result | Reason | Detail |", "|---|---|---|---|" };
        lines.AddRange(report.Checks.Select(x => $"| {x.Name} | {(x.Passed ? "PASS" : "FAIL")} | `{x.ReasonCode}` | {x.Detail.Replace("|", "\\|")} |"));
        if (report.Display is not null) lines.Add($"\n- Display: `{report.Display.Width}x{report.Display.Height}` ({report.Display.Orientation})");
        if (report.Stream is not null) lines.Add($"- Resolved stream: `{report.Stream.Width}x{report.Stream.Height}` from `{report.Stream.Source}`");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
