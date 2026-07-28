using System.Text.Json;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;
using PogoInventory.Streaming.Semantics;
using PogoInventory.Streaming.Semantics.Shadow;

var commandArgs = ShadowArgs.Parse(System.Environment.GetCommandLineArgs().Skip(1).ToArray());
var profile = await GateProfileLoader.LoadAsync(commandArgs.Profile);
var regions = profile.Regions.ToDictionary(x => x.Name, x => new PogoInventory.Vision.Models.NormalizedRegion
{
    X = x.Region.X, Y = x.Region.Y, Width = x.Region.Width, Height = x.Region.Height
}, StringComparer.Ordinal);
var started = DateTimeOffset.UtcNow;
var sessionId = $"shadow-{started:yyyyMMddTHHmmssZ}";
var output = Path.GetFullPath(commandArgs.Output);
Directory.CreateDirectory(output);

await using var transport = new ScrcpyReadOnlyVideoTransport(new ScrcpyOptions
{
    DeviceSerial = commandArgs.Device,
    AdbPath = commandArgs.Adb,
    ScrcpyServerJar = commandArgs.Server,
    MaxFps = commandArgs.MaxFps
});
await using var decoder = new FfmpegBgraVideoFrameDecoder(new FfmpegDecoderOptions
{
    FfmpegPath = commandArgs.Ffmpeg,
    Width = commandArgs.Width,
    Height = commandArgs.Height
});
await using var producer = new ScrcpyRawFrameProducer(transport, decoder);
await using var source = new StreamingFrameSource(producer, options: new() { BufferCapacity = Math.Max(8, commandArgs.MaxFps * 2) });
await source.StartAsync();

var capture = new StreamingShadowFrameCapture(source, regions);
var analyzers = new[]
{
    UnsupportedAnalyzer("screen-state", "ScreenState"),
    UnsupportedAnalyzer("details-identity", "Species"),
    UnsupportedAnalyzer("details-cp", "CP"),
    UnsupportedAnalyzer("appraisal-attack", "AttackIV"),
    UnsupportedAnalyzer("appraisal-defense", "DefenseIV"),
    UnsupportedAnalyzer("appraisal-hp", "HPIV")
};

ShadowSessionReport report;
using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(commandArgs.Duration)))
{
    report = await new SemanticShadowRunner().RunAsync(
        sessionId,
        capture.CaptureAsync(new StreamingShadowCaptureOptions
        {
            MaximumFrames = commandArgs.MaximumFrames,
            RequireStable = true,
            MinimumStableFrames = profile.Stable.MinimumStableFrames,
            MinimumStableDuration = profile.Stable.MinimumStableDuration,
            MinimumCompositeQuality = .35
        }, timeout.Token),
        analyzers,
        new EmptyShadowReferenceProvider(),
        new SemanticShadowOptions
        {
            MaximumFrames = commandArgs.MaximumFrames,
            MaximumDuration = TimeSpan.FromSeconds(commandArgs.Duration),
            AnalyzerTimeout = TimeSpan.FromSeconds(Math.Min(2, Math.Max(1, commandArgs.Duration))),
            MaximumAnalyzerConcurrency = 2
        },
        timeout.Token);
}

await source.StopAsync();
var paths = await new ShadowReportWriter().WriteAsync(report, output);
Console.WriteLine(JsonSerializer.Serialize(new
{
    report.SessionId,
    report.FinalStatus,
    Frames = report.Frames.Count,
    report.KnownCandidates,
    report.AnalyzerFaults,
    report.AnalyzerTimeouts,
    report.InputCommandsSent,
    report.AuthorizesPhoneInput,
    paths.JsonPath,
    paths.MarkdownPath
}, new JsonSerializerOptions { WriteIndented = true }));
return report.FinalStatus is "Completed" or "TimedOutWithFrames" ? 0 : 2;

static IShadowSemanticAnalyzer UnsupportedAnalyzer(string name, string field) =>
    new DelegateShadowAnalyzer(name, (frame, token) =>
    {
        token.ThrowIfCancellationRequested();
        IReadOnlyList<ShadowFieldCandidate> result = new[]
        {
            new ShadowFieldCandidate(name, field, FieldReadingStatus.Unsupported, null, 0,
                "ANALYZER_NOT_CONFIGURED", frame.FrameId, frame.EvidenceHash)
        };
        return ValueTask.FromResult(result);
    });

sealed class ShadowArgs
{
    public required string Device { get; init; }
    public required string Profile { get; init; }
    public string Adb { get; init; } = "adb";
    public string Server { get; init; } = "scrcpy-server-v4.0";
    public string Ffmpeg { get; init; } = "ffmpeg";
    public string Output { get; init; } = "evidence\\phase6b-shadow";
    public int Duration { get; init; } = 30;
    public int MaximumFrames { get; init; } = 10;
    public int MaxFps { get; init; } = 30;
    public int Width { get; init; } = 1080;
    public int Height { get; init; } = 2400;

    public static ShadowArgs Parse(string[] values)
    {
        string Get(string name, string fallback) { var i = Array.IndexOf(values, name); return i >= 0 && i + 1 < values.Length ? values[i + 1] : fallback; }
        var device = Get("--device", "");
        var profile = Get("--profile", "");
        if (string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("--device and --profile are required");
        return new()
        {
            Device = device, Profile = profile, Adb = Get("--adb", "adb"), Server = Get("--server", "scrcpy-server-v4.0"),
            Ffmpeg = Get("--ffmpeg", "ffmpeg"), Output = Get("--output", "evidence\\phase6b-shadow"),
            Duration = int.Parse(Get("--duration", "30")), MaximumFrames = int.Parse(Get("--maximum-frames", "10")),
            MaxFps = int.Parse(Get("--max-fps", "30")), Width = int.Parse(Get("--width", "1080")), Height = int.Parse(Get("--height", "2400"))
        };
    }
}
