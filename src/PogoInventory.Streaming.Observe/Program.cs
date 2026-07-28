using System.Diagnostics;
using System.Text.Json;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Scrcpy;

var a = Args.Parse(args);
Directory.CreateDirectory(a.Output);
var report = new Report { DeviceSerial = a.Device, StartUtc = DateTimeOffset.UtcNow, ConfiguredMaxFps = a.MaxFps, InputCommandsSent = 0 };

try
{
    var options = new ScrcpyOptions { DeviceSerial = a.Device, AdbPath = a.Adb, ScrcpyServerJar = a.Server, MaxFps = a.MaxFps };
    await using var transport = new ScrcpyReadOnlyVideoTransport(options);
    await using var decoder = new FfmpegBgraVideoFrameDecoder(new() { FfmpegPath = a.Ffmpeg, Width = a.Width, Height = a.Height });
    await using var producer = new ScrcpyRawFrameProducer(transport, decoder);
    await using var source = new StreamingFrameSource(producer, options: new() { BufferCapacity = Math.Max(8, a.MaxFps * a.BufferSeconds) });
    await source.StartAsync();
    var observations = new List<StreamObservation>();
    var observer = new StreamingObservationService(source);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(a.Duration));
    try
    {
        await foreach (var observation in observer.ObserveAsync(cts.Token))
        {
            observations.Add(observation);
            report.FramesPublished++;
            if (observation.IsStable) report.StableWindowsDetected++;
            if (observation.ChangeScore > .08) report.VisualChangesDetected++;
            if (observation.IsFrozen) report.StreamFreezeEvents++;
            if (observation.ResolutionChanged) report.ResolutionChanges++;
        }
    }
    catch (OperationCanceledException) { }

    var selected = await new FrameCandidateSelector(source).SelectAsync(FrameCandidateKind.LatestStableFrame, TimeSpan.FromSeconds(1));
    selected.Lease?.Dispose();
    await source.StopAsync();
    if (source.LastError is StreamTransportException sourceFailure) throw sourceFailure;
    if (source.LastError is Exception sourceError) throw sourceError;

    report.TcpBytesReceived = transport.TcpBytesReceived;
    report.EncodedPayloadBytes = transport.TcpBytesReceived;
    report.EncodedPacketsReceived = transport.EncodedPacketsPublished;
    report.FramesDecoded = decoder.CompleteBgraFramesAssembled;
    report.RawVideoBytesRead = decoder.RawVideoBytesRead;
    report.CompleteBgraFramesAssembled = decoder.CompleteBgraFramesAssembled;
    report.BytesWrittenToFfmpegStdin = decoder.BytesWrittenToFfmpegStdin;
    report.FirstByteLatency = transport.FirstByteLatency?.TotalMilliseconds;
    report.FirstFrameLatency = decoder.FirstDecodedFrameLatency?.TotalMilliseconds;
    report.FfmpegExitCode = decoder.FfmpegExitCode;
    report.ServerExitCode = transport.ServerExitCode;
    report.FinalStatus = report.FramesPublished > 0 ? "Completed" : "NoFramesReceived";
    report.ShutdownResult = "Clean";
    report.AverageFrameInterval = observations.Where(x => x.FrameInterval.HasValue).Select(x => x.FrameInterval!.Value.TotalMilliseconds).DefaultIfEmpty().Average();
}
catch (StreamTransportException error)
{
    report.FinalStatus = error.Failure.Code.ToString();
    report.Errors.Add(error.Failure);
}
catch (Exception error)
{
    report.FinalStatus = "Faulted";
    report.Errors.Add(new(StreamFailureCode.StreamEndedUnexpectedly, error.Message, error.ToString()));
}

report.EndUtc = DateTimeOffset.UtcNow;
report.Duration = (report.EndUtc - report.StartUtc).TotalSeconds;
report.PeakManagedMemory = GC.GetTotalMemory(false);
report.PeakProcessMemory = Process.GetCurrentProcess().PeakWorkingSet64;
await File.WriteAllTextAsync(Path.Combine(a.Output, "stream-observation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return report.FinalStatus == "Completed" ? 0 : 2;

sealed class Args
{
    public required string Device { get; init; }
    public string Adb { get; init; } = "adb";
    public string Server { get; init; } = "scrcpy-server-v4.0";
    public string Ffmpeg { get; init; } = "ffmpeg";
    public string Output { get; init; } = "evidence";
    public int Duration { get; init; } = 30;
    public int BufferSeconds { get; init; } = 2;
    public int MaxFps { get; init; } = 30;
    public int Width { get; init; } = 1080;
    public int Height { get; init; } = 2400;
    public static Args Parse(string[] values)
    {
        string Get(string name, string fallback) { var index = Array.IndexOf(values, name); return index >= 0 && index + 1 < values.Length ? values[index + 1] : fallback; }
        var device = Get("--device", "");
        if (string.IsNullOrWhiteSpace(device)) throw new ArgumentException("--device is required");
        return new() { Device = device, Adb = Get("--adb", "adb"), Server = Get("--server", "scrcpy-server-v4.0"), Ffmpeg = Get("--ffmpeg", "ffmpeg"), Output = Get("--output", "evidence"), Duration = int.Parse(Get("--duration", "30")), BufferSeconds = int.Parse(Get("--buffer-seconds", "2")), MaxFps = int.Parse(Get("--max-fps", "30")), Width = int.Parse(Get("--width", "1080")), Height = int.Parse(Get("--height", "2400")) };
    }
}

sealed class Report
{
    public string DeviceSerial { get; set; } = ""; public DateTimeOffset StartUtc { get; set; } public DateTimeOffset EndUtc { get; set; } public double Duration { get; set; }
    public string StreamCodec { get; set; } = "h264"; public string StreamResolution { get; set; } = "configured"; public int ConfiguredMaxFps { get; set; }
    public long? TcpBytesReceived { get; set; } public long? EncodedPayloadBytes { get; set; } public long? EncodedPacketsReceived { get; set; }
    public long FramesDecoded { get; set; } public long FramesPublished { get; set; } public long? FramesDroppedByDecoder { get; set; } public long? FramesDroppedByRingBuffer { get; set; } public long? SubscriberDrops { get; set; }
    public double? FirstByteLatency { get; set; } public double? FirstFrameLatency { get; set; } public double? AverageFrameInterval { get; set; } public double? P50FrameInterval { get; set; } public double? P95FrameInterval { get; set; } public double? P99FrameInterval { get; set; }
    public double? AverageDecodeDuration { get; set; } public double? P50DecodeDuration { get; set; } public double? P95DecodeDuration { get; set; } public double? P99DecodeDuration { get; set; }
    public long StableWindowsDetected { get; set; } public long VisualChangesDetected { get; set; } public long StreamFreezeEvents { get; set; } public long ResolutionChanges { get; set; } public long DecoderErrors { get; set; } public long StreamInterruptions { get; set; }
    public long? RawVideoBytesRead { get; set; } public long? CompleteBgraFramesAssembled { get; set; } public long? BytesWrittenToFfmpegStdin { get; set; } public int? FfmpegExitCode { get; set; } public int? ServerExitCode { get; set; }
    public long PeakManagedMemory { get; set; } public long PeakProcessMemory { get; set; } public string ShutdownResult { get; set; } = "Unknown"; public string FinalStatus { get; set; } = "Unknown"; public int InputCommandsSent { get; set; } public List<StreamFailure> Errors { get; } = [];
}
