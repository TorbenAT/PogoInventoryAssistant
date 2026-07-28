using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.Streaming.Observe.Gates;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        ObserveGateArguments parsed;
        try
        {
            parsed = ObserveGateArguments.Parse(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine(ObserveGateArguments.Usage);
            return 64;
        }

        Directory.CreateDirectory(parsed.OutputDirectory);
        var report = new GateObservationReport
        {
            DeviceSerial = parsed.DeviceSerial,
            ProfileName = parsed.Profile,
            StartUtc = DateTimeOffset.UtcNow,
            InputCommandsSent = 0,
            RealPhoneAcceptance = "NOT RUN until this command completes against a real phone"
        };
        var baselineReferences = RetainedFrame.ActiveReferences;
        StreamingFrameSource? source = null;
        SelectedFrameSet? selectedFrames = null;
        TemporalGateRun? run = null;

        try
        {
            var profile = await GateProfileLoader.LoadAsync(parsed.Profile).ConfigureAwait(false);
            report.ProfileName = profile.Name;
            var scrcpyOptions = new ScrcpyOptions
            {
                DeviceSerial = parsed.DeviceSerial,
                AdbPath = parsed.AdbPath,
                ScrcpyServerJar = parsed.ScrcpyServerJar,
                MaxFps = parsed.MaxFps,
                MaxSize = parsed.MaxSize,
                RequestedWidth = parsed.Width,
                RequestedHeight = parsed.Height
            };
            var decoderOptions = new FfmpegDecoderOptions
            {
                FfmpegPath = parsed.FfmpegPath,
                Width = parsed.Width ?? 0,
                Height = parsed.Height ?? 0
            };

            var transport = new ScrcpyReadOnlyVideoTransport(scrcpyOptions);
            var decoder = new FfmpegBgraVideoFrameDecoder(decoderOptions);
            var producer = new ScrcpyRawFrameProducer(transport, decoder);
            source = new StreamingFrameSource(
                producer,
                options: new StreamingFrameSourceOptions
                {
                    BufferCapacity = Math.Clamp(parsed.MaxFps * parsed.BufferSeconds, 8, 900),
                    DropOldestWhenFull = true
                });
            await source.StartAsync().ConfigureAwait(false);

            var leaseSource = new StreamingFrameLeaseSource(source, subscriptionCapacity: 4);
            var gate = GateFactory.Create(profile);
            var engine = new TemporalGateEngine(
                profile,
                new TemporalGateEngineOptions { MaximumDuration = TimeSpan.FromSeconds(parsed.DurationSeconds) });
            run = await engine.RunAsync(leaseSource).ConfigureAwait(false);

            var selector = new FrameSetSelector();
            selectedFrames = await selector.SelectAsync(
                run.Session,
                new FrameSetRequest
                {
                    StableOptions = profile.Stable,
                    TransitionOptions = profile.Transition,
                    Diversity = profile.Diversity
                }).ConfigureAwait(false);
            var evidenceDirectory = Path.Combine(parsed.OutputDirectory, "frames");
            var evidence = await GateEvidenceExporter.ExportAsync(
                selectedFrames,
                evidenceDirectory,
                profile.MaximumEvidenceFrames).ConfigureAwait(false);

            report.Apply(run, selectedFrames, evidence);
            report.RealPhoneAcceptance = run.Result.GateState == TemporalGateState.Passed
                ? "READ-ONLY RUN COMPLETED; review report and evidence before declaring acceptance"
                : "READ-ONLY RUN COMPLETED WITHOUT PASS";

            await File.WriteAllTextAsync(
                Path.Combine(parsed.OutputDirectory, "gate-result.json"),
                JsonSerializer.Serialize(run.Result, JsonOptions)).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(parsed.OutputDirectory, "gate-timeline.json"),
                JsonSerializer.Serialize(run.Timeline, JsonOptions)).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            report.FinalGateState = TemporalGateState.Faulted.ToString();
            report.ReasonCode = GateReasonCode.Faulted.ToString();
            report.Errors.Add(new DiagnosticError(error.GetType().FullName ?? error.GetType().Name, error.Message));
        }
        finally
        {
            selectedFrames?.Dispose();
            if (run is not null)
            {
                await run.DisposeAsync().ConfigureAwait(false);
            }

            if (source is not null)
            {
                try
                {
                    await source.StopAsync().ConfigureAwait(false);
                    report.ShutdownResult = "Clean";
                }
                catch (Exception error)
                {
                    report.ShutdownResult = "Faulted";
                    report.Errors.Add(new DiagnosticError(error.GetType().FullName ?? error.GetType().Name, error.Message));
                }

                await source.DisposeAsync().ConfigureAwait(false);
            }

            report.EndUtc = DateTimeOffset.UtcNow;
            report.Duration = report.EndUtc - report.StartUtc;
            report.LeasesOutstandingAtShutdown = RetainedFrame.ActiveReferences - baselineReferences;
            report.PeakManagedMemory = GC.GetTotalMemory(false);
            report.PeakProcessMemory = Process.GetCurrentProcess().PeakWorkingSet64;
            await File.WriteAllTextAsync(
                Path.Combine(parsed.OutputDirectory, "gate-observation.json"),
                JsonSerializer.Serialize(report, JsonOptions)).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }

        return string.Equals(report.FinalGateState, TemporalGateState.Passed.ToString(), StringComparison.Ordinal) &&
               report.InputCommandsSent == 0 &&
               report.LeasesOutstandingAtShutdown == 0
            ? 0
            : 2;
    }
}

internal sealed class ObserveGateArguments
{
    public const string Usage = "observe-gates --device SERIAL --server PATH --ffmpeg PATH [--width PX --height PX] [--profile NAME_OR_JSON] [--duration 30] [--buffer-seconds 2] [--max-fps 30] [--max-size 1920] [--adb adb] [--output evidence]";

    public required string DeviceSerial { get; init; }
    public required string ScrcpyServerJar { get; init; }
    public required string FfmpegPath { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string AdbPath { get; init; } = "adb";
    public string Profile { get; init; } = "StableHeaderAndPanel";
    public string OutputDirectory { get; init; } = "evidence-gates";
    public int DurationSeconds { get; init; } = 30;
    public int BufferSeconds { get; init; } = 2;
    public int MaxFps { get; init; } = 30;
    public int MaxSize { get; init; } = 1920;

    public static ObserveGateArguments Parse(string[] args)
    {
        string Get(string name, string defaultValue)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : defaultValue;
        }

        string? GetOptional(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        var device = Get("--device", string.Empty);
        var server = Get("--server", string.Empty);
        var ffmpeg = Get("--ffmpeg", "ffmpeg");
        if (string.IsNullOrWhiteSpace(device))
        {
            throw new ArgumentException("--device is required and must be an explicit ADB serial.");
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            throw new ArgumentException("--server must point to the matching scrcpy server JAR.");
        }

        var width = ParseOptionalPositiveInt(GetOptional("--width"), "--width");
        var height = ParseOptionalPositiveInt(GetOptional("--height"), "--height");
        if (width.HasValue != height.HasValue)
        {
            throw new ArgumentException("--width and --height must be supplied together.");
        }
        return new ObserveGateArguments
        {
            DeviceSerial = device,
            ScrcpyServerJar = server,
            FfmpegPath = ffmpeg,
            Width = width,
            Height = height,
            AdbPath = Get("--adb", "adb"),
            Profile = Get("--profile", "StableHeaderAndPanel"),
            OutputDirectory = Get("--output", "evidence-gates"),
            DurationSeconds = ParsePositiveInt(Get("--duration", "30"), "--duration"),
            BufferSeconds = ParsePositiveInt(Get("--buffer-seconds", "2"), "--buffer-seconds"),
            MaxFps = ParsePositiveInt(Get("--max-fps", "30"), "--max-fps"),
            MaxSize = ParsePositiveInt(Get("--max-size", "1920"), "--max-size")
        };
    }

    private static int? ParseOptionalPositiveInt(string? value, string name)
    {
        return value is null ? null : ParsePositiveInt(value, name);
    }

    private static int ParsePositiveInt(string value, string name)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"{name} must be a positive integer.");
        }

        return parsed;
    }

}

internal sealed class GateObservationReport
{
    public string DeviceSerial { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public TimeSpan Duration { get; set; }
    public long FramesObserved { get; set; }
    public long FramesRejected { get; set; }
    public long StableFrames { get; set; }
    public long TransitionFrames { get; set; }
    public long FreezeEvents { get; set; }
    public long ResolutionChanges { get; set; }
    public IReadOnlyList<GateTransitionRecord> GateTransitions { get; set; } = Array.Empty<GateTransitionRecord>();
    public string FinalGateState { get; set; } = TemporalGateState.Faulted.ToString();
    public string ReasonCode { get; set; } = GateReasonCode.Faulted.ToString();
    public IReadOnlyList<long> SelectedEvidenceFrameIds { get; set; } = Array.Empty<long>();
    public IReadOnlyList<string> EvidencePaths { get; set; } = Array.Empty<string>();
    public double? AverageObservationDuration { get; set; }
    public double? P50ObservationDuration { get; set; }
    public double? P95ObservationDuration { get; set; }
    public double? P99ObservationDuration { get; set; }
    public int MaximumConcurrentAnalysis { get; set; }
    public int? PeakQueueDepth { get; set; }
    public int PeakHistoryDepth { get; set; }
    public long HistoryEvictions { get; set; }
    public long DroppedObservations { get; set; }
    public long DroppedTimelineEntries { get; set; }
    public IReadOnlyDictionary<string, RegionMetricPercentiles> RegionMetrics { get; set; } =
        new Dictionary<string, RegionMetricPercentiles>(StringComparer.Ordinal);
    public long LeasesOutstandingAtShutdown { get; set; }
    public int InputCommandsSent { get; set; }
    public string ShutdownResult { get; set; } = "NotStarted";
    public string RealPhoneAcceptance { get; set; } = "NOT RUN";
    public long PeakManagedMemory { get; set; }
    public long PeakProcessMemory { get; set; }
    public List<DiagnosticError> Errors { get; } = new();

    public void Apply(TemporalGateRun run, SelectedFrameSet frames, EvidenceExportResult evidence)
    {
        FramesObserved = run.Session.FramesObserved;
        FramesRejected = run.Session.FramesRejected;
        StableFrames = run.StableFrames;
        TransitionFrames = run.TransitionFrames;
        FreezeEvents = run.FreezeEvents;
        ResolutionChanges = run.ResolutionChanges;
        GateTransitions = run.Timeline;
        FinalGateState = run.Result.GateState.ToString();
        ReasonCode = run.Result.ReasonCode.ToString();
        SelectedEvidenceFrameIds = frames.Frames.Values.Select(x => x.FrameId.Value).Distinct().OrderBy(x => x).ToArray();
        EvidencePaths = evidence.Paths;
        AverageObservationDuration = Percentiles.Average(run.ObservationDurationsMs);
        P50ObservationDuration = Percentiles.Value(run.ObservationDurationsMs, 0.50);
        P95ObservationDuration = Percentiles.Value(run.ObservationDurationsMs, 0.95);
        P99ObservationDuration = Percentiles.Value(run.ObservationDurationsMs, 0.99);
        MaximumConcurrentAnalysis = run.MaximumConcurrentAnalysis;
        PeakQueueDepth = null;
        PeakHistoryDepth = run.Session.PeakHistoryDepth;
        HistoryEvictions = run.Session.HistoryEvictions;
        DroppedObservations = run.DroppedObservations;
        DroppedTimelineEntries = run.DroppedTimelineEntries;
        RegionMetrics = run.RegionalMetricSamples.ToDictionary(
            x => x.Key,
            x => new RegionMetricPercentiles
            {
                MotionP50 = Percentiles.Value(x.Value.Motion, 0.50),
                MotionP95 = Percentiles.Value(x.Value.Motion, 0.95),
                MotionP99 = Percentiles.Value(x.Value.Motion, 0.99),
                DifferenceP50 = Percentiles.Value(x.Value.Difference, 0.50),
                DifferenceP95 = Percentiles.Value(x.Value.Difference, 0.95),
                DifferenceP99 = Percentiles.Value(x.Value.Difference, 0.99),
                SimilarityP50 = Percentiles.Value(x.Value.Similarity, 0.50),
                SimilarityP95 = Percentiles.Value(x.Value.Similarity, 0.95),
                SimilarityP99 = Percentiles.Value(x.Value.Similarity, 0.99),
                SharpnessP50 = Percentiles.Value(x.Value.Sharpness, 0.50),
                SharpnessP95 = Percentiles.Value(x.Value.Sharpness, 0.95),
                SharpnessP99 = Percentiles.Value(x.Value.Sharpness, 0.99)
            },
            StringComparer.Ordinal);
    }
}

internal sealed class RegionMetricPercentiles
{
    public double? MotionP50 { get; init; }
    public double? MotionP95 { get; init; }
    public double? MotionP99 { get; init; }
    public double? DifferenceP50 { get; init; }
    public double? DifferenceP95 { get; init; }
    public double? DifferenceP99 { get; init; }
    public double? SimilarityP50 { get; init; }
    public double? SimilarityP95 { get; init; }
    public double? SimilarityP99 { get; init; }
    public double? SharpnessP50 { get; init; }
    public double? SharpnessP95 { get; init; }
    public double? SharpnessP99 { get; init; }
}

internal sealed record DiagnosticError(string Type, string Message);

internal static class Percentiles
{
    public static double? Average(IReadOnlyList<double> values) => values.Count == 0 ? null : values.Average();

    public static double? Value(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(x => x).ToArray();
        var position = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
    }
}
