using System.Diagnostics;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Core.Reference;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.HeaderText;
using PogoInventory.Semantics;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;
using PogoInventory.Streaming.Semantics;
using PogoInventory.TesseractOcr;
using PogoInventory.Vision.Imaging;

internal static class StreamPokemonReaderCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = Options.Parse(args);
        Directory.CreateDirectory(options.Output);
        Directory.CreateDirectory(Path.Combine(options.Output, "items"));
        var profile = await GateProfileLoader.LoadAsync(options.Profile, cancellationToken);
        var appraisalProfile = await AppraisalProfileLoader.LoadAsync(options.AppraisalProfile, cancellationToken);
        var reference = new StaticSpeciesReference(SpeciesReferenceLoader.LoadFromFile(Path.Combine("data", "reference", "species-reference.json")).Species.Select(x => x.Name));
        var tessdata = Path.GetFullPath(Path.Combine("tools", "tessdata-best"));
        if (!TesseractTextRecognizer.IsSupported(tessdata, "eng")) throw new InvalidOperationException($"Tesseract tessdata is unavailable: {tessdata}");
        using var tesseract = new TesseractTextRecognizer(tessdata, "eng");
        var headerAnalyzer = new PokemonHeaderAnalyzer(tesseract, reference);
        var adb = new AdbAndroidDeviceTransport(new AdbProcessRunner(options.Adb, new ConsoleDeviceLog()), new DeviceHarnessOptions { AdbPath = options.Adb, HarnessVersion = DeviceHarnessOptions.CurrentVersion }, new ConsoleDeviceLog());
        var named = new AndroidVerifiedInventoryNamedOperations(adb, options.Device, await AutomationProfileLoader.LoadAsync(options.AutomationProfile, cancellationToken), Path.Combine(options.Output, "named-evidence"), appraisalProfile);
        await using var transport = new ScrcpyReadOnlyVideoTransport(new ScrcpyOptions { DeviceSerial = options.Device, AdbPath = options.Adb, ScrcpyServerJar = options.ScrcpyServer, MaxFps = 30, MaxSize = 1920 });
        await using var decoder = new FfmpegBgraVideoFrameDecoder(new FfmpegDecoderOptions { FfmpegPath = options.Ffmpeg });
        await using var source = new StreamingFrameSource(new ScrcpyRawFrameProducer(transport, decoder), options: new StreamingFrameSourceOptions { BufferCapacity = 120, DropOldestWhenFull = true });
        await source.StartAsync(cancellationToken);

        var records = new List<StreamItemRecord>();
        var handoffs = new List<HandoffReport>();
        var runStatus = StreamRunStatus.Failed;
        string? stopReason = null;
        var setupInputs = 0;
        var swipes = 0;
        var marker = await LatestMarkerAsync(source, cancellationToken);
        var actionStarted = DateTimeOffset.UtcNow;
        try
        {
            var state = await WaitForStreamStateAsync(source, appraisalProfile, cancellationToken);
            if (state is PokemonGoGameState.GameplayMap or PokemonGoGameState.Inventory)
            {
                var inventory = await named.EnsureFilteredInventoryAsync("age0-1825", cancellationToken);
                var opened = inventory == VerifiedSequenceState.Inventory ? await named.OpenFirstPokemonAsync(cancellationToken) : VerifiedSequenceState.Unknown;
                var setup = opened == VerifiedSequenceState.PokemonDetails ? await named.CaptureAppraisalAsync(cancellationToken) : "Unknown";
                if (inventory != VerifiedSequenceState.Inventory || opened != VerifiedSequenceState.PokemonDetails || !string.Equals(setup, "AppraisalBarsObserved", StringComparison.Ordinal)) throw new InvalidOperationException("Named setup did not reach AppraisalBars.");
                setupInputs = named.LastCaptureAppraisalInputCount;
            }
            else if (state == PokemonGoGameState.PokemonDetails)
            {
                var setup = await named.CaptureAppraisalAsync(cancellationToken);
                if (!string.Equals(setup, "AppraisalBarsObserved", StringComparison.Ordinal)) throw new InvalidOperationException("Named appraisal route did not reach AppraisalBars.");
                setupInputs = named.LastCaptureAppraisalInputCount;
            }
            else if (state != PokemonGoGameState.Appraisal) throw new InvalidOperationException($"Stream start state '{state}' is not an accepted AppraisalBars start state.");
            var actionCompleted = DateTimeOffset.UtcNow;
            string? previousFingerprint = null;

            for (var ordinal = 1; ordinal <= options.Items; ordinal++)
            {
                var itemStarted = Stopwatch.GetTimestamp();
                var handoff = await WaitForSettledAppraisalHandoffAsync(source, profile, appraisalProfile, marker, actionStarted, actionCompleted, previousFingerprint, Path.Combine(options.Output, "settling", $"item-{ordinal:000}"), cancellationToken);
                handoffs.Add(handoff.Report with { Ordinal = ordinal });
                if (handoff.Status == AppraisalHandoffStatus.NoEffectOrFilterEnd) { runStatus = StreamRunStatus.FilterExhausted; stopReason = "NO_EFFECT_OR_FILTER_END"; break; }
                if (handoff.Status != AppraisalHandoffStatus.Ready) { runStatus = StreamRunStatus.SafeStopped; stopReason = "APPRAISAL_SETTLING_TIMEOUT"; break; }

                var record = await AnalyzeItemAsync(ordinal, handoff.Frames, headerAnalyzer, appraisalProfile, itemStarted, options.Output, cancellationToken);
                records.Add(record with { SettlingMilliseconds = handoff.Report.ElapsedMilliseconds, SwipeToStableMilliseconds = ordinal == 1 ? null : handoff.Report.ElapsedMilliseconds });
                previousFingerprint = handoff.Report.NewFingerprint ?? handoff.Report.CurrentFingerprint;
                await File.AppendAllTextAsync(Path.Combine(options.Output, "items.jsonl"), JsonSerializer.Serialize(records[^1]) + Environment.NewLine, cancellationToken);
                await WriteReportsAsync(options.Output, options.Items, records, handoffs, runStatus, stopReason, setupInputs, swipes, cancellationToken);
                if (ordinal == options.Items) { runStatus = StreamRunStatus.CompletedRequestedItems; break; }

                marker = await LatestMarkerAsync(source, cancellationToken);
                actionStarted = DateTimeOffset.UtcNow;
                var advance = await named.AdvanceToNextPokemonInAppraisalAsync(records[^1].EvidenceHashes[0], new CleanupProofAppraisalCapture { Status = "Partial", StableFingerprintSha256 = records[^1].EvidenceHashes[0] }, cancellationToken);
                actionCompleted = DateTimeOffset.UtcNow;
                swipes += named.LastAppraisalCarouselSwipeInputCount;
                if (advance == AppraisalCarouselAdvanceResult.UNKNOWN_STOP && named.LastAppraisalCarouselSwipeInputCount == 0) { runStatus = StreamRunStatus.SafeStopped; stopReason = "UNKNOWN_STOP"; break; }
                if (advance == AppraisalCarouselAdvanceResult.NO_EFFECT_OR_FILTER_END && named.LastAppraisalCarouselSwipeInputCount == 0) { runStatus = StreamRunStatus.FilterExhausted; stopReason = "NO_EFFECT_OR_FILTER_END"; break; }
            }
        }
        catch (Exception error)
        {
            runStatus = StreamRunStatus.Failed;
            stopReason = error.Message;
        }
        finally
        {
            await WriteReportsAsync(options.Output, options.Items, records, handoffs, runStatus, stopReason, setupInputs, swipes, CancellationToken.None);
            await source.StopAsync(CancellationToken.None);
        }
        return runStatus is StreamRunStatus.CompletedRequestedItems or StreamRunStatus.FilterExhausted ? 0 : 2;
    }

    private static async Task<StreamItemRecord> AnalyzeItemAsync(int ordinal, IReadOnlyList<SettledFrame> frames, PokemonHeaderAnalyzer headerAnalyzer, AppraisalVisualProfile appraisalProfile, long started, string output, CancellationToken cancellationToken)
    {
        var evidence = new List<PokemonEvidenceFrame>(); var species = new List<SemanticObservation<string>>(); var cp = new List<SemanticObservation<int?>>(); var iv = new List<SemanticObservation<(int, int, int)>>(); var raw = new List<string>(); var ocrMs = 0d; var ivMs = 0d;
        var directory = Path.Combine(output, "items", $"item-{ordinal:000}");
        Directory.CreateDirectory(directory);
        foreach (var frame in frames)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, $"frame-{frame.Id:000000}.png"), frame.Png, cancellationToken);
            var hash = BgraPixelBridge.Sha256(frame.Bgra); evidence.Add(new PokemonEvidenceFrame(frame.Id, frame.CapturedAtUtc, hash, "AppraisalBars", "scrcpy-stream"));
            var ocrStart = Stopwatch.GetTimestamp(); var header = await headerAnalyzer.AnalyzeAsync(frame.Png, HeaderScreenType.AppraisalBars, cancellationToken); ocrMs += ElapsedMs(ocrStart); raw.AddRange(header.RawLines.Select(x => x.Text));
            if (header.Species is not null) species.Add(new(header.Species, header.SpeciesConfidence, frame.Id, hash)); if (header.Cp is not null) cp.Add(new(header.Cp, header.CpConfidence, frame.Id, hash));
            var ivStart = Stopwatch.GetTimestamp(); var analysis = new AppraisalAnalyzer().Analyze(PngDecoder.Decode(frame.Png), appraisalProfile, allowComplete: false); ivMs += ElapsedMs(ivStart);
            if (analysis.AttackIv is not null && analysis.DefenseIv is not null && analysis.HpIv is not null && analysis.Bars.All(x => x.TrackDetected && x.Confidence >= appraisalProfile.CompleteBarConfidenceMinimum)) iv.Add(new((analysis.AttackIv.Value, analysis.DefenseIv.Value, analysis.HpIv.Value), analysis.Confidence, frame.Id, hash));
        }
        var result = new PokemonItemSemanticAnalyzer().Analyze(new PokemonItemEvidenceSet($"stream:{ordinal:D6}", evidence, evidence), species, cp, iv);
        return new StreamItemRecord(ordinal, result, raw, ocrMs, ivMs, ElapsedMs(started), frames.Select(x => x.Id).ToArray(), evidence.Select(x => x.EvidenceHash).ToArray(), "No", 0, null);
    }

    private static async Task<SettlingHandoff> WaitForSettledAppraisalHandoffAsync(StreamingFrameSource source, GateProfile profile, AppraisalVisualProfile appraisalProfile, FrameMarker marker, DateTimeOffset actionStarted, DateTimeOffset actionCompleted, string? previousFingerprint, string evidenceDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(evidenceDirectory);
        var barrier = new FrameBarrier(marker.FrameId, actionStarted, TimeSpan.FromSeconds(2), "AppraisalBars");
        var evaluator = new AppraisalHandoffEvaluator(profile.Stable, profile.Regions, previousFingerprint);
        await using var observer = new MultiRegionTemporalObserver(profile.Regions, profile.Observer);
        var frames = new Dictionary<long, SettledFrame>(); var stale = 0; var wrong = 0; var observed = 0; var last = marker.FrameId; byte[]? lastPng = null;
        var started = Stopwatch.GetTimestamp();
        while (ElapsedMs(started) < 8000)
        {
            var lease = await source.GetLatestAsync(new FrameQuery { AfterFrameId = new FrameId(last), MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0, SearchWindow = TimeSpan.FromMilliseconds(100) }, cancellationToken);
            if (lease is null) { await Task.Delay(25, cancellationToken); continue; }
            using var retained = new RetainedFrame(lease);
            last = retained.Metadata.Id.Value; observed++;
            using var copy = retained.Acquire();
            var bgra = copy.Pixels.ToArray();
            if (bgra.Length < copy.Metadata.Descriptor.RequiredByteLength) { stale++; continue; }
            var rgba = BgraPixelBridge.ToTightlyPackedRgba32(bgra, copy.Metadata.Descriptor.Width, copy.Metadata.Descriptor.Height, copy.Metadata.Descriptor.Stride);
            var png = PngEncoder.Encode(new PixelImage(copy.Metadata.Descriptor.Width, copy.Metadata.Descriptor.Height, rgba)); lastPng = png;
            var isAppraisal = new PokemonGoGameStateDetector().Detect(png, appraisalProfile).State == PokemonGoGameState.Appraisal;
            var tagged = copy.Metadata with { Tags = isAppraisal ? new Dictionary<string, string> { ["screen"] = "AppraisalBars" } : new Dictionary<string, string>() };
            if (!barrier.Accepts(tagged, DateTimeOffset.UtcNow)) { if (isAppraisal) stale++; else wrong++; continue; }
            var observation = await observer.AnalyzeAsync(retained, cancellationToken);
            var before = evaluator.Observe(observation, isAppraisal);
            foreach (var id in before.QualifiedFrameIds.Where(id => id.Value == observation.FrameId.Value)) frames[observation.FrameId.Value] = new SettledFrame(observation.FrameId.Value, observation.UtcTimestamp, bgra, png);
            if (before.Status == AppraisalHandoffStatus.Ready)
            {
                var selected = before.QualifiedFrameIds.Select(id => frames[id.Value]).TakeLast(3).ToArray();
                var report = new HandoffReport(0, observed, stale, wrong, before.ReasonCounts, selected.Length, ElapsedMs(started), before.PreviousFingerprint, before.CurrentFingerprint, before.NewFingerprint, actionCompleted, null);
                return new SettlingHandoff(before.Status, selected, report);
            }
        }
        var timeout = evaluator.CompleteTimeout();
        if (lastPng is not null) { await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "best-header-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "best-panel-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "lowest-motion-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "highest-sharpness-frame.png"), lastPng, cancellationToken); }
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "reason-counts.json"), JsonSerializer.Serialize(timeout.ReasonCounts, JsonOptions), cancellationToken);
        var timeoutReport = new HandoffReport(0, observed, stale, wrong, timeout.ReasonCounts, timeout.QualifiedFrameIds.Count, ElapsedMs(started), timeout.PreviousFingerprint, timeout.CurrentFingerprint, timeout.NewFingerprint, actionCompleted, "APPRAISAL_SETTLING_TIMEOUT");
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "settling-summary.json"), JsonSerializer.Serialize(timeoutReport, JsonOptions), cancellationToken);
        return new SettlingHandoff(timeout.Status, Array.Empty<SettledFrame>(), timeoutReport);
    }

    private static async Task<FrameMarker> LatestMarkerAsync(StreamingFrameSource source, CancellationToken cancellationToken)
    {
        using var lease = await source.GetLatestAsync(new FrameQuery { MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0 }, cancellationToken);
        return lease is null ? new FrameMarker(-1, DateTimeOffset.UtcNow) : new FrameMarker(lease.Metadata.Id.Value, lease.Metadata.Timestamp.CapturedAtUtc);
    }

    private static double ElapsedMs(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private static async Task<PokemonGoGameState> WaitForStreamStateAsync(StreamingFrameSource source, AppraisalVisualProfile profile, CancellationToken ct)
    { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(8)); while (true) { using var lease = await source.GetLatestAsync(new FrameQuery { MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0 }, timeout.Token); if (lease is not null) { var rgba = BgraPixelBridge.ToTightlyPackedRgba32(lease.Pixels.Span, lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, lease.Metadata.Descriptor.Stride); var state = new PokemonGoGameStateDetector().Detect(PngEncoder.Encode(new PixelImage(lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, rgba)), profile).State; if (state != PokemonGoGameState.Unknown) return state; } await Task.Delay(50, timeout.Token); } }

    private static async Task WriteReportsAsync(string output, int requested, IReadOnlyList<StreamItemRecord> records, IReadOnlyList<HandoffReport> handoffs, StreamRunStatus status, string? stop, int setupInputs, int swipes, CancellationToken ct)
    {
        var summary = new { RequestedItems = requested, CompletedItems = records.Count, RunStatus = status, StopReason = stop, CompleteItems = records.Count(x => x.Result.IsComplete), SetupInputCommandsSent = setupInputs, ProgressionSwipesSent = swipes, SemanticInputCommandsSent = 0, VlmUsed = false, Records = records, Handoffs = handoffs };
        await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, JsonOptions), ct);
        var csv = "Ordinal,Species,SpeciesStatus,CP,CPStatus,IV,IVStatus,ItemMs,SettlingMs,OCRMs,IVMs\n" + string.Join(Environment.NewLine, records.Select(x => $"{x.Ordinal},{x.Result.Species.Value ?? "Unknown"},{x.Result.Species.Status},{x.Result.Cp.Value?.ToString() ?? "Unknown"},{x.Result.Cp.Status},{x.Result.AttackIv.Value}/{x.Result.DefenseIv.Value}/{x.Result.HpIv.Value},{x.Result.AttackIv.Status},{x.ItemMilliseconds:F1},{x.SettlingMilliseconds:F1},{x.OcrMilliseconds:F1},{x.IvMilliseconds:F1}"));
        await File.WriteAllTextAsync(Path.Combine(output, "items.csv"), csv, ct);
        var rows = string.Join(Environment.NewLine, records.Select(x => $"<tr><td>{x.Ordinal}</td><td>{x.Result.Species.Value ?? "Unknown"} ({x.Result.Species.Status})</td><td>{x.Result.Cp.Value?.ToString() ?? "Unknown"} ({x.Result.Cp.Status})</td><td>{x.Result.AttackIv.Value}/{x.Result.DefenseIv.Value}/{x.Result.HpIv.Value} ({x.Result.AttackIv.Status})</td><td>{x.ItemMilliseconds:F0}</td><td>{x.SettlingMilliseconds:F0}</td><td>{string.Join(",", x.FrameIds)}</td></tr>"));
        var html = $"<!doctype html><meta charset='utf-8'><meta http-equiv='refresh' content='2'><title>Stream Pokemon reader</title><style>body{{font-family:sans-serif}}td{{padding:.4rem}}</style><h1>Stream Pokemon reader</h1><p>Run status: {status}; requested/completed: {requested}/{records.Count}; stop: {stop ?? "none"}; setup inputs: {setupInputs}; actual swipes: {swipes}; semantic inputs: 0; VLM: disabled</p><table border='1'><tr><th>Item</th><th>Species</th><th>CP</th><th>IV</th><th>Item ms</th><th>Settling ms</th><th>Frames</th></tr>{rows}</table>";
        await File.WriteAllTextAsync(Path.Combine(output, "live.html"), html, ct);
    }

    private sealed record Options(string Device, string Profile, string AppraisalProfile, int Items, string Output, string Adb, string Ffmpeg, string ScrcpyServer, string AutomationProfile)
    { public static Options Parse(string[] args) { string Get(string n, string? fallback = null) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"{n} is required."); } var root = Directory.GetCurrentDirectory(); return new(Get("--device"), Get("--profile"), Get("--appraisal-profile"), int.Parse(Get("--items")), Path.GetFullPath(Get("--out")), Get("--adb", Path.Combine(root, "tools", "local", "scrcpy", "scrcpy-win64-v4.0", "adb.exe")), Get("--ffmpeg", Path.Combine(root, "tools", "local", "ffmpeg", "ffmpeg-8.1.2-essentials_build", "bin", "ffmpeg.exe")), Get("--scrcpy-server", Path.Combine(root, "tools", "local", "scrcpy", "scrcpy-win64-v4.0", "scrcpy-server")), Get("--automation-profile", Path.Combine(root, "local-data", "automation-profile.local.json"))); } }
    private enum StreamRunStatus { CompletedRequestedItems, FilterExhausted, SafeStopped, Failed }
    private sealed record FrameMarker(long FrameId, DateTimeOffset CapturedAtUtc);
    private sealed record SettledFrame(long Id, DateTimeOffset CapturedAtUtc, byte[] Bgra, byte[] Png);
    private sealed record SettlingHandoff(AppraisalHandoffStatus Status, IReadOnlyList<SettledFrame> Frames, HandoffReport Report);
    private sealed record HandoffReport(int Ordinal, int FramesObserved, int FramesRejectedStale, int FramesRejectedWrongState, IReadOnlyDictionary<string, int> ReasonCounts, int StableQualifyingFrames, double ElapsedMilliseconds, string? PreviousFingerprint, string? CurrentFingerprint, string? NewFingerprint, DateTimeOffset ActionCompletedUtc, string? StopReason);
    private sealed record StreamItemRecord(int Ordinal, PokemonItemSemanticResult Result, IReadOnlyList<string> RawOcr, double OcrMilliseconds, double IvMilliseconds, double ItemMilliseconds, IReadOnlyList<long> FrameIds, IReadOnlyList<string> EvidenceHashes, string VlmUsed, double SettlingMilliseconds, double? SwipeToStableMilliseconds);
}
