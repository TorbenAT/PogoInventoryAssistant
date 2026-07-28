using System.Diagnostics;
using System.Security.Cryptography;
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
using PogoInventory.Exploration.Services;
using PogoInventory.Exploration.Models;
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
        var itemsDirectory = Path.Combine(options.Output, "items");
        Directory.CreateDirectory(itemsDirectory);
        var records = new List<StreamItemRecord>();
        var profile = await GateProfileLoader.LoadAsync(options.Profile, cancellationToken);
        // Carousel settling can exceed the offline profile's four-second observation window.
        // Keep the same thresholds and regions, but allow one bounded post-swipe settling window.
        profile = profile with { Stable = profile.Stable with { MaximumObservationDuration = TimeSpan.FromSeconds(8) } };
        var appraisalProfile = await AppraisalProfileLoader.LoadAsync(options.AppraisalProfile, cancellationToken);
        var speciesReferencePath = Path.Combine("data", "reference", "species-reference.json");
        var speciesReference = SpeciesReferenceLoader.LoadFromFile(speciesReferencePath);
        var reference = new StaticSpeciesReference(speciesReference.Species.Select(x => x.Name));
        var tessdata = Path.GetFullPath(Path.Combine("tools", "tessdata-best"));
        if (!TesseractTextRecognizer.IsSupported(tessdata, "eng"))
            throw new InvalidOperationException($"Tesseract tessdata is unavailable: {tessdata}");
        using var tesseract = new TesseractTextRecognizer(tessdata, "eng");
        var headerAnalyzer = new PokemonHeaderAnalyzer(tesseract, reference);
        var appraisalAnalyzer = new AppraisalAnalyzer();
        var semanticAnalyzer = new PokemonItemSemanticAnalyzer();
        var adb = new AdbAndroidDeviceTransport(
            new AdbProcessRunner(options.Adb, new ConsoleDeviceLog()),
            new DeviceHarnessOptions { AdbPath = options.Adb, HarnessVersion = DeviceHarnessOptions.CurrentVersion },
            new ConsoleDeviceLog());
        var automationProfile = await AutomationProfileLoader.LoadAsync(options.AutomationProfile, cancellationToken);
        var named = new AndroidVerifiedInventoryNamedOperations(adb, options.Device, automationProfile, Path.Combine(options.Output, "named-evidence"), appraisalProfile);

        await using var transport = new ScrcpyReadOnlyVideoTransport(new ScrcpyOptions { DeviceSerial = options.Device, AdbPath = options.Adb, ScrcpyServerJar = options.ScrcpyServer, MaxFps = 30, MaxSize = 1920 });
        await using var decoder = new FfmpegBgraVideoFrameDecoder(new FfmpegDecoderOptions { FfmpegPath = options.Ffmpeg });
        await using var source = new StreamingFrameSource(new ScrcpyRawFrameProducer(transport, decoder), options: new StreamingFrameSourceOptions { BufferCapacity = 120, DropOldestWhenFull = true });
        await source.StartAsync(cancellationToken);
        var startState = await WaitForStreamStateAsync(source, appraisalProfile, cancellationToken);
        if (startState == PokemonGoGameState.GameplayMap || startState == PokemonGoGameState.Inventory)
        {
            var inventory = await named.EnsureFilteredInventoryAsync("age0-1825", cancellationToken);
            if (inventory != VerifiedSequenceState.Inventory)
                throw new InvalidOperationException($"Named inventory navigation did not reach Inventory: {inventory}");
            var opened = await named.OpenFirstPokemonAsync(cancellationToken);
            if (opened != VerifiedSequenceState.PokemonDetails)
                throw new InvalidOperationException($"Named first-item navigation did not reach PokemonDetails: {opened}");
            var setup = await named.CaptureAppraisalAsync(cancellationToken);
            if (!string.Equals(setup, "AppraisalBarsObserved", StringComparison.Ordinal))
                throw new InvalidOperationException($"Named appraisal route did not reach AppraisalBars: {setup}");
        }
        else if (startState == PokemonGoGameState.PokemonDetails)
        {
            var setup = await named.CaptureAppraisalAsync(cancellationToken);
            if (!string.Equals(setup, "AppraisalBarsObserved", StringComparison.Ordinal))
                throw new InvalidOperationException($"Named appraisal route did not reach AppraisalBars: {setup}");
        }
        else if (startState != PokemonGoGameState.Appraisal)
        {
            throw new InvalidOperationException($"Stream start state '{startState}' is not an accepted AppraisalBars start state.");
        }
        var leaseSource = new StreamingFrameLeaseSource(source, 4);
        var jsonlPath = Path.Combine(options.Output, "items.jsonl");

        try
        {
            for (var ordinal = 1; ordinal <= options.Items; ordinal++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var itemStarted = Stopwatch.GetTimestamp();
                await using var gateRun = await new TemporalGateEngine(profile, new TemporalGateEngineOptions { MaximumDuration = TimeSpan.FromSeconds(30) }).RunAsync(leaseSource, cancellationToken);
                if (gateRun.Result.GateState != TemporalGateState.Passed)
                    throw new InvalidOperationException($"AppraisalBars gate failed for item {ordinal}: {gateRun.Result.ReasonCode}");
                using var selected = await new FrameSetSelector().SelectAsync(gateRun.Session, new FrameSetRequest { Roles = [FrameRole.BestHeaderFrame, FrameRole.BestPanelFrame, FrameRole.BestOverallStableFrame], StableOptions = profile.Stable, TransitionOptions = profile.Transition, Diversity = profile.Diversity }, cancellationToken);
                var frames = selected.Frames.Values.GroupBy(x => x.FrameId.Value).Select(x => x.First()).Take(3).ToArray();
                if (frames.Length < 3) throw new InvalidOperationException($"AppraisalBars gate produced only {frames.Length} distinct evidence frames for item {ordinal}.");
                var itemDirectory = Path.Combine(itemsDirectory, $"item-{ordinal:000}");
                Directory.CreateDirectory(itemDirectory);
                var evidence = new List<PokemonEvidenceFrame>();
                var speciesObservations = new List<SemanticObservation<string>>();
                var cpObservations = new List<SemanticObservation<int?>>();
                var ivObservations = new List<SemanticObservation<(int Attack, int Defense, int Hp)>>();
                var rawOcr = new List<string>();
                var ocrMs = 0d;
                var ivMs = 0d;
                foreach (var frame in frames)
                {
                    var bgra = frame.Lease.Pixels.ToArray();
                    var hash = BgraPixelBridge.Sha256(bgra);
                    var rgba = BgraPixelBridge.ToTightlyPackedRgba32(bgra, frame.Lease.Metadata.Descriptor.Width, frame.Lease.Metadata.Descriptor.Height, frame.Lease.Metadata.Descriptor.Stride);
                    var png = PngEncoder.Encode(new PixelImage(frame.Lease.Metadata.Descriptor.Width, frame.Lease.Metadata.Descriptor.Height, rgba));
                    await File.WriteAllBytesAsync(Path.Combine(itemDirectory, $"frame-{frame.FrameId.Value:000000}.png"), png, cancellationToken);
                    evidence.Add(new PokemonEvidenceFrame(frame.FrameId.Value, frame.TimestampUtc, hash, "AppraisalBars", "scrcpy-stream"));
                    var ocrStart = Stopwatch.GetTimestamp();
                    var header = await headerAnalyzer.AnalyzeAsync(png, HeaderScreenType.AppraisalBars, cancellationToken);
                    ocrMs += ElapsedMs(ocrStart);
                    rawOcr.AddRange(header.RawLines.Select(x => x.Text));
                    if (header.Species is not null) speciesObservations.Add(new(header.Species, header.SpeciesConfidence, frame.FrameId.Value, hash));
                    if (header.Cp is not null) cpObservations.Add(new(header.Cp, header.CpConfidence, frame.FrameId.Value, hash));
                    var ivStart = Stopwatch.GetTimestamp();
                    var appraisal = appraisalAnalyzer.Analyze(PngDecoder.Decode(png), appraisalProfile, allowComplete: false);
                    ivMs += ElapsedMs(ivStart);
                    if (appraisal.AttackIv is not null && appraisal.DefenseIv is not null && appraisal.HpIv is not null && appraisal.Bars.All(x => x.TrackDetected && x.Confidence >= appraisalProfile.CompleteBarConfidenceMinimum))
                        ivObservations.Add(new((appraisal.AttackIv.Value, appraisal.DefenseIv.Value, appraisal.HpIv.Value), appraisal.Confidence, frame.FrameId.Value, hash));
                }
                var result = semanticAnalyzer.Analyze(new PokemonItemEvidenceSet($"stream:{ordinal:D6}", evidence, evidence), speciesObservations, cpObservations, ivObservations);
                var record = new StreamItemRecord(ordinal, result, rawOcr, ocrMs, ivMs, ElapsedMs(itemStarted), frames.Select(x => x.FrameId.Value).ToArray(), evidence.Select(x => x.EvidenceHash).ToArray(), "No");
                records.Add(record);
                await File.AppendAllTextAsync(jsonlPath, JsonSerializer.Serialize(record) + Environment.NewLine, cancellationToken);
                await WriteReportsAsync(options.Output, records, cancellationToken);
                if (ordinal == options.Items) break;
                var advance = await named.AdvanceToNextPokemonInAppraisalAsync(record.EvidenceHashes[0], new CleanupProofAppraisalCapture { Status = "Partial", StableFingerprintSha256 = record.EvidenceHashes[0] }, cancellationToken);
                if (advance is AppraisalCarouselAdvanceResult.UNKNOWN_STOP or AppraisalCarouselAdvanceResult.NO_EFFECT_OR_FILTER_END) break;
            }
        }
        finally
        {
            await source.StopAsync(CancellationToken.None);
            await source.DisposeAsync();
        }
        return records.Count >= Math.Min(3, options.Items) ? 0 : 2;
    }

    private static double ElapsedMs(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;

    private static async Task<PokemonGoGameState> WaitForStreamStateAsync(StreamingFrameSource source, AppraisalVisualProfile appraisalProfile, CancellationToken cancellationToken)
    {
        var detector = new PokemonGoGameStateDetector();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        while (true)
        {
            var lease = await source.GetLatestAsync(new FrameQuery { MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0 }, timeout.Token);
            if (lease is not null)
            {
                using (lease)
                {
                    var bgra = lease.Pixels.ToArray();
                    var rgba = BgraPixelBridge.ToTightlyPackedRgba32(bgra, lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, lease.Metadata.Descriptor.Stride);
                    var png = PngEncoder.Encode(new PixelImage(lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, rgba));
                    var state = detector.Detect(png, appraisalProfile).State;
                    if (state != PokemonGoGameState.Unknown) return state;
                }
            }
            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task WriteReportsAsync(string output, IReadOnlyList<StreamItemRecord> records, CancellationToken cancellationToken)
    {
        var summary = new
        {
            Items = records.Count,
            CompleteItems = records.Count(x => x.Result.IsComplete),
            UnknownOrIncompleteItems = records.Count(x => !x.Result.IsComplete),
            AverageItemMilliseconds = records.Count == 0 ? 0 : records.Average(x => x.ItemMilliseconds),
            EvidenceFrames = records.Sum(x => x.FrameIds.Count),
            CarouselSwipesSent = Math.Max(0, records.Count - 1),
            SemanticInputCommandsSent = 0,
            VlmUsed = false,
            Records = records
        };
        await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, JsonOptions), cancellationToken);
        var csv = "Ordinal,Species,SpeciesStatus,CP,CPStatus,IV,IVStatus,ItemMs,OCRMs,IVMs\n" + string.Join(Environment.NewLine, records.Select(x => $"{x.Ordinal},{x.Result.Species.Value ?? "Unknown"},{x.Result.Species.Status},{x.Result.Cp.Value?.ToString() ?? "Unknown"},{x.Result.Cp.Status},{x.Result.AttackIv.Value}/{x.Result.DefenseIv.Value}/{x.Result.HpIv.Value},{x.Result.AttackIv.Status},{x.ItemMilliseconds:F1},{x.OcrMilliseconds:F1},{x.IvMilliseconds:F1}"));
        await File.WriteAllTextAsync(Path.Combine(output, "items.csv"), csv, cancellationToken);
        var rows = string.Join(Environment.NewLine, records.Select(x => $"<tr><td>{x.Ordinal}</td><td>{x.Result.Species.Value ?? "Unknown"} ({x.Result.Species.Status})</td><td>{x.Result.Cp.Value?.ToString() ?? "Unknown"} ({x.Result.Cp.Status})</td><td>{x.Result.AttackIv.Value}/{x.Result.DefenseIv.Value}/{x.Result.HpIv.Value} ({x.Result.AttackIv.Status})</td><td>{x.ItemMilliseconds:F0} ms</td><td>{string.Join(",", x.FrameIds)}</td></tr>"));
        var html = "<!doctype html><meta charset='utf-8'><meta http-equiv='refresh' content='2'><title>Stream Pokémon reader</title><style>body{font-family:sans-serif}td{padding:.4rem}.Known{background:#d9f7d9}.Unknown{background:#fff3bf}.Conflicting{background:#ffd6d6}</style><h1>Stream Pokémon reader</h1><p>VLM: disabled · SemanticInputCommandsSent: 0</p><table border='1'><tr><th>Item</th><th>Species</th><th>CP</th><th>IV</th><th>Total</th><th>Frame IDs</th></tr>" + rows + "</table>";
        await File.WriteAllTextAsync(Path.Combine(output, "live.html"), html, cancellationToken);
    }

    private sealed record Options(string Device, string Profile, string AppraisalProfile, int Items, string Output, string Adb, string Ffmpeg, string ScrcpyServer, string AutomationProfile)
    {
        public static Options Parse(string[] args)
        {
            string Get(string name, string? fallback = null) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"{name} is required."); }
            var root = Directory.GetCurrentDirectory();
            return new(Get("--device"), Get("--profile"), Get("--appraisal-profile"), int.Parse(Get("--items")), Path.GetFullPath(Get("--out")), Get("--adb", Path.Combine(root, "tools", "local", "scrcpy", "scrcpy-win64-v4.0", "adb.exe")), Get("--ffmpeg", Path.Combine(root, "tools", "local", "ffmpeg", "ffmpeg-8.1.2-essentials_build", "bin", "ffmpeg.exe")), Get("--scrcpy-server", Path.Combine(root, "tools", "local", "scrcpy", "scrcpy-win64-v4.0", "scrcpy-server")), Get("--automation-profile", Path.Combine(root, "local-data", "automation-profile.local.json")));
        }
    }

    private sealed record StreamItemRecord(int Ordinal, PokemonItemSemanticResult Result, IReadOnlyList<string> RawOcr, double OcrMilliseconds, double IvMilliseconds, double ItemMilliseconds, IReadOnlyList<long> FrameIds, IReadOnlyList<string> EvidenceHashes, string VlmUsed);
}
