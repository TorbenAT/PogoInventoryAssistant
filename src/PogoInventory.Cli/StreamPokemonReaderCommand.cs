using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const double CalibratedIvBarConfidenceMinimum = .70;
    private const double ProgressionOnlyIvBarConfidenceMinimum = .65;
    private const double ZeroIvBarConfidenceMinimum = .45;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly JsonSerializerOptions JsonLinesOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = Options.Parse(args);
        if (Directory.Exists(options.Output) &&
            Directory.EnumerateFileSystemEntries(options.Output).Any())
        {
            throw new InvalidOperationException(
                $"Output directory must be new or empty: {options.Output}");
        }
        Directory.CreateDirectory(options.Output);
        Directory.CreateDirectory(Path.Combine(options.Output, "items"));
        var runId = $"stream-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}"[..43];
        var runStarted = DateTimeOffset.UtcNow;
        var commit = ReadGitCommit();
        var profile = await GateProfileLoader.LoadAsync(options.Profile, cancellationToken);
        var appraisalProfile = await AppraisalProfileLoader.LoadAsync(options.AppraisalProfile, cancellationToken);
        var speciesReferenceData = SpeciesReferenceLoader.LoadFromFile(
            Path.Combine("data", "reference", "species-reference.json"));
        var reference = new StaticSpeciesReference(
            speciesReferenceData.Species.Select(x => x.Name));
        var tessdata = Path.GetFullPath(Path.Combine("tools", "tessdata-best"));
        if (!TesseractTextRecognizer.IsSupported(tessdata, "eng")) throw new InvalidOperationException($"Tesseract tessdata is unavailable: {tessdata}");
        using var tesseractRaw = new TesseractTextRecognizer(
            tessdata, "eng", binarizeCpRegion: false);
        using var tesseractBinarized = new TesseractTextRecognizer(
            tessdata, "eng", binarizeCpRegion: true);
        var headerAnalyzer = new PokemonHeaderAnalyzer(
            tesseractRaw, reference);
        var binarizedHeaderAnalyzer = new PokemonHeaderAnalyzer(
            tesseractBinarized, reference);
        var adb = new AdbAndroidDeviceTransport(new AdbProcessRunner(options.Adb, new ConsoleDeviceLog()), new DeviceHarnessOptions { AdbPath = options.Adb, HarnessVersion = DeviceHarnessOptions.CurrentVersion }, new ConsoleDeviceLog());
        var named = new AndroidVerifiedInventoryNamedOperations(adb, options.Device, await AutomationProfileLoader.LoadAsync(options.AutomationProfile, cancellationToken), Path.Combine(options.Output, "named-evidence"), appraisalProfile);
        await using var transport = new ScrcpyReadOnlyVideoTransport(new ScrcpyOptions { DeviceSerial = options.Device, AdbPath = options.Adb, ScrcpyServerJar = options.ScrcpyServer, MaxFps = 30, MaxSize = 1920 });
        await using var decoder = new FfmpegBgraVideoFrameDecoder(new FfmpegDecoderOptions { FfmpegPath = options.Ffmpeg });
        var producer = new ScrcpyRawFrameProducer(transport, decoder);
        await using var source = new StreamingFrameSource(producer, options: new StreamingFrameSourceOptions { BufferCapacity = 120, DropOldestWhenFull = true });
        var leaseBaseline = RetainedFrame.ActiveReferences;
        await source.StartAsync(cancellationToken);
        var observer = new MultiRegionTemporalObserver(
            profile.Regions, profile.Observer);

        var records = new List<StreamProofRecord>();
        var handoffs = new List<StreamProofHandoff>();
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        var runStatus = StreamProofRunStatus.Running;
        string? stopReason = null;
        var swipes = 0;
        var marker = await LatestMarkerAsync(source, cancellationToken);
        var actionStarted = DateTimeOffset.UtcNow;
        StreamProofIntegrity? integrity = null;
        try
        {
            _ = await WaitForStreamStateAsync(source, appraisalProfile, cancellationToken);
            var inventory = await named.EnsureFilteredInventoryAsync(
                options.InventoryQuery,
                cancellationToken);
            var opened = inventory == VerifiedSequenceState.Inventory
                ? await named.OpenFirstPokemonAsync(cancellationToken)
                : VerifiedSequenceState.Unknown;
            var setup = opened == VerifiedSequenceState.PokemonDetails
                ? await named.CaptureAppraisalAsync(cancellationToken)
                : "Unknown";
            if (inventory != VerifiedSequenceState.Inventory ||
                opened != VerifiedSequenceState.PokemonDetails ||
                !string.Equals(setup, "AppraisalBarsObserved", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Named setup did not establish broad query {options.InventoryQuery} and reach AppraisalBars.");
            }
            var actionCompleted = DateTimeOffset.UtcNow;
            string? previousVisualFingerprint = null;
            string? previousItemFingerprint = null;
            var actionTransitionObserved = false;

            for (var ordinal = 1; ordinal <= options.Items; ordinal++)
            {
                var itemStarted = Stopwatch.GetTimestamp();
                var handoff = await WaitForSettledAppraisalHandoffAsync(source, observer, profile, appraisalProfile, marker, actionStarted, actionCompleted, previousVisualFingerprint, actionTransitionObserved, Path.Combine(options.Output, "settling", $"item-{ordinal:000}"), cancellationToken);
                handoffs.Add(handoff.Report with { Ordinal = ordinal });
                if (handoff.Status == AppraisalHandoffStatus.NoEffectOrFilterEnd) { runStatus = StreamProofRunStatus.FilterExhausted; stopReason = "NO_EFFECT_OR_FILTER_END"; break; }
                if (handoff.Status != AppraisalHandoffStatus.Ready) { runStatus = StreamProofRunStatus.SafeStopped; stopReason = "APPRAISAL_SETTLING_TIMEOUT"; break; }

                var visualFingerprint = handoff.Report.NewFingerprint ?? handoff.Report.CurrentFingerprint
                    ?? throw new InvalidOperationException("Ready handoff did not publish an item fingerprint.");
                var record = await AnalyzeItemAsync(
                    runId, ordinal, handoff.Frames, visualFingerprint,
                    headerAnalyzer, binarizedHeaderAnalyzer,
                    appraisalProfile, profile, speciesReferenceData, handoff.Report, itemStarted,
                    actionStarted, options.Output, cancellationToken);
                var itemFingerprint =
                    StreamPokemonProofReporter.BuildItemFingerprint(
                        visualFingerprint, record.Result);
                record = record with { ItemFingerprint = itemFingerprint };
                handoffs[^1] = handoffs[^1] with
                {
                    PreviousFingerprint = previousItemFingerprint,
                    CurrentFingerprint = itemFingerprint,
                    NewFingerprint = previousItemFingerprint is null
                        ? null
                        : itemFingerprint
                };
                if (handoff.Report.ReasonCounts.ContainsKey(
                    "SemanticProgressionProofRequired"))
                {
                    var progressionReasons = records.Count == 0
                        ? Array.Empty<string>()
                        : PokemonItemProgressionEvidence.ProveDifferent(
                            records[^1].ProgressionResult,
                            record.ProgressionResult).ToArray();
                    if (progressionReasons.Length == 0)
                    {
                        runStatus = StreamProofRunStatus.SafeStopped;
                        stopReason = "SEMANTIC_PROGRESSION_NOT_PROVEN";
                        handoffs[^1] = handoffs[^1] with { StopReason = stopReason };
                        break;
                    }

                    var reasonCounts = new Dictionary<string, int>(
                        handoff.Report.ReasonCounts, StringComparer.Ordinal);
                    foreach (var reason in progressionReasons)
                    {
                        reasonCounts[$"SemanticProgression:{reason}"] = 1;
                    }
                    handoffs[^1] = handoffs[^1] with { ReasonCounts = reasonCounts };
                    record = record with { GateRejectionCounts = reasonCounts };
                }
                if (!fingerprints.Add(itemFingerprint))
                {
                    runStatus = StreamProofRunStatus.SafeStopped;
                    stopReason = "DUPLICATE_ITEM_FINGERPRINT";
                    break;
                }
                records.Add(record);
                previousVisualFingerprint = visualFingerprint;
                previousItemFingerprint = itemFingerprint;
                await File.AppendAllTextAsync(
                    Path.Combine(options.Output, "items.jsonl"),
                    JsonSerializer.Serialize(records[^1], JsonLinesOptions) + Environment.NewLine,
                    cancellationToken);
                await StreamPokemonProofReporter.WriteLiveAsync(
                    options.Output,
                    BuildContext(runId, commit, options, runStarted, null, runStatus, stopReason,
                        CountSetupInputs(options.Output, swipes), swipes, source, decoder, transport,
                        leaseBaseline, "Running"),
                    records, handoffs, cancellationToken);
                if (ordinal == options.Items) { runStatus = StreamProofRunStatus.CompletedRequestedItems; break; }

                marker = await LatestMarkerAsync(source, cancellationToken);
                actionStarted = DateTimeOffset.UtcNow;
                using var transitionCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                var transitionTask = ObserveActionTransitionAsync(
                    source, observer, marker.FrameId,
                    transitionCancellation.Token);
                AppraisalCarouselAdvanceResult advance;
                try
                {
                    advance = await named.AdvanceToNextPokemonInAppraisalAsync(records[^1].EvidenceHashes[0], new CleanupProofAppraisalCapture { Status = "Partial", StableFingerprintSha256 = records[^1].EvidenceHashes[0] }, cancellationToken);
                }
                finally
                {
                    transitionCancellation.Cancel();
                }
                actionTransitionObserved = await transitionTask;
                actionCompleted = DateTimeOffset.UtcNow;
                swipes += named.LastAppraisalCarouselSwipeInputCount;
                if (advance == AppraisalCarouselAdvanceResult.UNKNOWN_STOP && named.LastAppraisalCarouselSwipeInputCount == 0) { runStatus = StreamProofRunStatus.SafeStopped; stopReason = "UNKNOWN_STOP"; break; }
                if (advance == AppraisalCarouselAdvanceResult.NO_EFFECT_OR_FILTER_END && named.LastAppraisalCarouselSwipeInputCount == 0) { runStatus = StreamProofRunStatus.FilterExhausted; stopReason = "NO_EFFECT_OR_FILTER_END"; break; }
            }
        }
        catch (Exception error)
        {
            runStatus = StreamProofRunStatus.Failed;
            stopReason = source.LastError?.Message ?? error.Message;
        }
        finally
        {
            await observer.DisposeAsync();
            await source.StopAsync(CancellationToken.None);
            var shutdown = source.LastError is null &&
                RetainedFrame.ActiveReferences - leaseBaseline == 0
                ? "Clean" : "Faulted";
            integrity = await StreamPokemonProofReporter.WriteFinalAsync(
                options.Output,
                BuildContext(runId, commit, options, runStarted, DateTimeOffset.UtcNow,
                    runStatus, stopReason, CountSetupInputs(options.Output, swipes), swipes,
                    source, decoder, transport, leaseBaseline, shutdown),
                records, handoffs, CancellationToken.None);
        }
        return runStatus == StreamProofRunStatus.CompletedRequestedItems &&
            (options.Items < 100 || integrity?.IntegrityStatus == "PASS") ? 0 : 2;
    }

    private static async Task<StreamProofRecord> AnalyzeItemAsync(
        string runId,
        int ordinal,
        IReadOnlyList<SettledFrame> frames,
        string itemFingerprint,
        PokemonHeaderAnalyzer headerAnalyzer,
        PokemonHeaderAnalyzer binarizedHeaderAnalyzer,
        AppraisalVisualProfile appraisalProfile,
        GateProfile gateProfile,
        SpeciesReferenceData speciesReferenceData,
        StreamProofHandoff handoff,
        long started,
        DateTimeOffset actionStarted,
        string output,
        CancellationToken cancellationToken)
    {
        var evidence = new List<PokemonEvidenceFrame>(); var visualFrames = new List<ProtectionVisualFrame>(); var species = new List<SemanticObservation<string>>(); var cpRaw = new List<SemanticObservation<int?>>(); var cpBinarized = new List<SemanticObservation<int?>>(); var iv = new List<SemanticObservation<(int, int, int)>>(); var progressionOnlyIv = new List<SemanticObservation<(int, int, int)>>(); var raw = new List<string>(); var ocrMs = 0d; var ivMs = 0d;
        var directory = Path.Combine(output, "items", $"item-{ordinal:000}");
        Directory.CreateDirectory(directory);
        var evidenceFiles = new List<string>();
        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            var frame = frames[frameIndex];
            var isPrimarySemanticFrame =
                frameIndex >= Math.Max(0, frames.Count - 3);
            var relative = $"items/item-{ordinal:000}/frame-{frame.Id:000000}.png";
            await File.WriteAllBytesAsync(Path.Combine(output, FromRelative(relative)), frame.Png, cancellationToken);
            evidenceFiles.Add(relative);
            var hash = Convert.ToHexString(SHA256.HashData(frame.Png)).ToLowerInvariant();
            evidence.Add(new PokemonEvidenceFrame(frame.Id, frame.CapturedAtUtc, hash, "AppraisalBars", "scrcpy-stream"));
            var pixelImage = PngDecoder.Decode(frame.Png);
            visualFrames.Add(new ProtectionVisualFrame(
                frame.Id, hash, pixelImage.Width, pixelImage.Height,
                pixelImage.RgbaBytes.ToArray()));
            var ocrStart = Stopwatch.GetTimestamp(); var header = await headerAnalyzer.AnalyzeAsync(frame.Png, HeaderScreenType.AppraisalBars, cancellationToken); var binarizedHeader = await binarizedHeaderAnalyzer.AnalyzeAsync(frame.Png, HeaderScreenType.AppraisalBars, cancellationToken); ocrMs += ElapsedMs(ocrStart); raw.AddRange(header.RawLines.Select(x => $"raw:{x.Text}")); raw.AddRange(binarizedHeader.RawLines.Select(x => $"binary:{x.Text}"));
            if (isPrimarySemanticFrame && header.Species is not null) species.Add(new(header.Species, header.SpeciesConfidence, frame.Id, hash)); if (header.Cp is not null) cpRaw.Add(new(header.Cp, header.CpConfidence, frame.Id, hash)); if (binarizedHeader.Cp is not null) cpBinarized.Add(new(binarizedHeader.Cp, binarizedHeader.CpConfidence, frame.Id, hash));
            var ivStart = Stopwatch.GetTimestamp(); var analysis = new AppraisalAnalyzer().Analyze(pixelImage, appraisalProfile, allowComplete: false); ivMs += ElapsedMs(ivStart);
            if (isPrimarySemanticFrame &&
                analysis.AttackIv is not null &&
                analysis.DefenseIv is not null &&
                analysis.HpIv is not null &&
                analysis.Bars.All(bar =>
                    IsTrustedIvBar(bar, appraisalProfile)))
            {
                iv.Add(new(
                    (analysis.AttackIv.Value, analysis.DefenseIv.Value,
                        analysis.HpIv.Value),
                    analysis.Confidence, frame.Id, hash));
            }
            if (analysis.AttackIv is not null &&
                analysis.DefenseIv is not null &&
                analysis.HpIv is not null &&
                analysis.Bars.All(IsProgressionOnlyTrustedIvBar))
            {
                progressionOnlyIv.Add(new(
                    (analysis.AttackIv.Value, analysis.DefenseIv.Value,
                        analysis.HpIv.Value),
                    analysis.Confidence, frame.Id, hash));
            }
        }
        var semanticAnalyzer = new PokemonItemSemanticAnalyzer();
        var evidenceSet = new PokemonItemEvidenceSet(
            $"stream:{ordinal:D6}", evidence, evidence);
        var rawResult = semanticAnalyzer.Analyze(
            evidenceSet, species, cpRaw, iv);
        var binarizedResult = semanticAnalyzer.Analyze(
            evidenceSet, species, cpBinarized, iv);
        var progressionOnlyResult = semanticAnalyzer.Analyze(
            evidenceSet, species, cpRaw, progressionOnlyIv);
        var resolvedCp =
            PokemonItemSemanticAnalyzer.ResolveCpPreprocessingVariants(
                rawResult.Cp, binarizedResult.Cp);
        var result = rawResult with
        {
            Cp = resolvedCp,
            IsComplete =
                rawResult.Species.Status == SemanticFieldStatus.Known &&
                resolvedCp.Status == SemanticFieldStatus.Known &&
                rawResult.AttackIv.Status == SemanticFieldStatus.Known &&
                rawResult.DefenseIv.Status == SemanticFieldStatus.Known &&
                rawResult.HpIv.Status == SemanticFieldStatus.Known
        };
        result = result with
        {
            Protection = ProtectionEnrichment.Analyze(
                evidenceSet, visualFrames, result.Species, speciesReferenceData)
        };
        var progressionResult = BuildProgressionResult(
            result, progressionOnlyResult, frames.Count);
        var decoded = PngDecoder.Decode(frames[0].Png);
        var headerCrop = await WriteCropAsync(output, ordinal, "header.png", decoded, gateProfile, "Header", cancellationToken);
        var panelCrop = await WriteCropAsync(output, ordinal, "appraisal-panel.png", decoded, gateProfile, "AppraisalPanel", cancellationToken);
        var attackCrop = await WriteCropAsync(output, ordinal, "attack-bar.png", decoded, gateProfile, "AttackBar", cancellationToken);
        var defenseCrop = await WriteCropAsync(output, ordinal, "defense-bar.png", decoded, gateProfile, "DefenseBar", cancellationToken);
        var hpCrop = await WriteCropAsync(output, ordinal, "hp-bar.png", decoded, gateProfile, "HpBar", cancellationToken);
        return new(
            runId,
            ordinal,
            frames[^1].CapturedAtUtc,
            itemFingerprint,
            result,
            progressionResult,
            raw,
            ocrMs,
            ivMs,
            ElapsedMs(started),
            frames.Select(x => x.Id).ToArray(),
            frames.Select(x => x.CapturedAtUtc).ToArray(),
            evidence.Select(x => x.EvidenceHash).ToArray(),
            evidenceFiles,
            evidenceFiles[0],
            headerCrop,
            panelCrop,
            attackCrop,
            defenseCrop,
            hpCrop,
            handoff.ElapsedMilliseconds,
            ordinal == 1 ? null : Math.Max(0, (frames[^1].CapturedAtUtc - actionStarted).TotalMilliseconds),
            handoff.ReasonCounts);
    }

    private static bool IsTrustedIvBar(
        AppraisalBarMeasurement bar,
        AppraisalVisualProfile profile)
    {
        if (!bar.TrackDetected || bar.EstimatedIv is null)
        {
            return false;
        }

        var regularMinimum = Math.Min(
            profile.CompleteBarConfidenceMinimum,
            CalibratedIvBarConfidenceMinimum);
        return bar.Confidence >= regularMinimum ||
            bar.EstimatedIv == 0 &&
            !bar.OrangeDetected &&
            bar.FillFraction <= .02 &&
            bar.Confidence >= ZeroIvBarConfidenceMinimum;
    }

    // This lower, fixed threshold is evidence only for a high-similarity
    // carousel handoff. It requires the same measured IV tuple in all five
    // settled frames and is never used to mark an inventory record complete.
    private static bool IsProgressionOnlyTrustedIvBar(
        AppraisalBarMeasurement bar) =>
        bar.TrackDetected &&
        bar.EstimatedIv is not null &&
        bar.Confidence >= ProgressionOnlyIvBarConfidenceMinimum;

    private static PokemonItemSemanticResult BuildProgressionResult(
        PokemonItemSemanticResult completeResult,
        PokemonItemSemanticResult progressionOnlyResult,
        int frameCount)
    {
        SemanticFieldResult<int?> Select(
            SemanticFieldResult<int?> standard,
            SemanticFieldResult<int?> progressionOnly) =>
            standard.Status == SemanticFieldStatus.Known ||
            progressionOnly.Status != SemanticFieldStatus.Known ||
            progressionOnly.FrameIds.Distinct().Count() != frameCount
                ? standard
                : progressionOnly with
                {
                    Reasons = progressionOnly.Reasons
                        .Append("PROGRESSION_ONLY_FIVE_FRAME_MODERATE_IV_EVIDENCE")
                        .ToArray()
                };

        return completeResult with
        {
            AttackIv = Select(completeResult.AttackIv, progressionOnlyResult.AttackIv),
            DefenseIv = Select(completeResult.DefenseIv, progressionOnlyResult.DefenseIv),
            HpIv = Select(completeResult.HpIv, progressionOnlyResult.HpIv),
            IsComplete = completeResult.IsComplete
        };
    }

    private static async Task<SettlingHandoff> WaitForSettledAppraisalHandoffAsync(StreamingFrameSource source, MultiRegionTemporalObserver observer, GateProfile profile, AppraisalVisualProfile appraisalProfile, FrameMarker marker, DateTimeOffset actionStarted, DateTimeOffset actionCompleted, string? previousFingerprint, bool actionTransitionObserved, string evidenceDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(evidenceDirectory);
        var barrier = new FrameBarrier(marker.FrameId, actionCompleted, TimeSpan.FromSeconds(2), "AppraisalBars");
        var evaluator = new AppraisalHandoffEvaluator(
            profile.Stable, profile.Regions, previousFingerprint,
            actionTransitionObserved);
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
                var selected = before.QualifiedFrameIds
                    .Select(id => frames[id.Value])
                    .TakeLast(profile.Stable.MinimumStableFrames)
                    .ToArray();
                var report = new StreamProofHandoff(
                    0, observed, stale, wrong, before.ReasonCounts, selected.Length,
                    ElapsedMs(started), before.PreviousFingerprint,
                    before.CurrentFingerprint, before.NewFingerprint,
                    marker.FrameId, actionStarted, actionCompleted, null);
                return new SettlingHandoff(before.Status, selected, report);
            }
        }
        var timeout = evaluator.CompleteTimeout();
        if (lastPng is not null) { await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "best-header-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "best-panel-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "lowest-motion-frame.png"), lastPng, cancellationToken); await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, "highest-sharpness-frame.png"), lastPng, cancellationToken); }
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "reason-counts.json"), JsonSerializer.Serialize(timeout.ReasonCounts, JsonOptions), cancellationToken);
        var timeoutReport = new StreamProofHandoff(
            0, observed, stale, wrong, timeout.ReasonCounts,
            timeout.QualifiedFrameIds.Count, ElapsedMs(started),
            timeout.PreviousFingerprint, timeout.CurrentFingerprint,
            timeout.NewFingerprint, marker.FrameId, actionStarted,
            actionCompleted, "APPRAISAL_SETTLING_TIMEOUT");
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "settling-summary.json"), JsonSerializer.Serialize(timeoutReport, JsonOptions), cancellationToken);
        return new SettlingHandoff(timeout.Status, Array.Empty<SettledFrame>(), timeoutReport);
    }

    private static async Task<bool> ObserveActionTransitionAsync(
        StreamingFrameSource source,
        MultiRegionTemporalObserver observer,
        long afterFrameId,
        CancellationToken cancellationToken)
    {
        var last = afterFrameId;
        var observed = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lease = await source.GetLatestAsync(
                    new FrameQuery
                    {
                        AfterFrameId = new FrameId(last),
                        MaximumAge = TimeSpan.FromSeconds(2),
                        MinimumCompositeQuality = 0,
                        SearchWindow = TimeSpan.FromMilliseconds(100)
                    },
                    cancellationToken);
                if (lease is null)
                {
                    await Task.Delay(25, cancellationToken);
                    continue;
                }

                last = lease.Metadata.Id.Value;
                using var retained = new RetainedFrame(lease);
                var observation = await observer.AnalyzeAsync(
                    retained, cancellationToken);
                if ((observation.QualityFlags &
                        TemporalQualityFlags.MissingPreviousFrame) == 0 &&
                    observation.IsLikelyTransitioning)
                {
                    observed = true;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }

        return observed;
    }

    private static async Task<FrameMarker> LatestMarkerAsync(StreamingFrameSource source, CancellationToken cancellationToken)
    {
        using var lease = await source.GetLatestAsync(new FrameQuery { MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0 }, cancellationToken);
        return lease is null ? new FrameMarker(-1, DateTimeOffset.UtcNow) : new FrameMarker(lease.Metadata.Id.Value, lease.Metadata.Timestamp.CapturedAtUtc);
    }

    private static double ElapsedMs(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private static async Task<PokemonGoGameState> WaitForStreamStateAsync(StreamingFrameSource source, AppraisalVisualProfile profile, CancellationToken ct)
    { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(8)); while (true) { using var lease = await source.GetLatestAsync(new FrameQuery { MaximumAge = TimeSpan.FromSeconds(2), MinimumCompositeQuality = 0 }, timeout.Token); if (lease is not null) { var rgba = BgraPixelBridge.ToTightlyPackedRgba32(lease.Pixels.Span, lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, lease.Metadata.Descriptor.Stride); var state = new PokemonGoGameStateDetector().Detect(PngEncoder.Encode(new PixelImage(lease.Metadata.Descriptor.Width, lease.Metadata.Descriptor.Height, rgba)), profile).State; if (state != PokemonGoGameState.Unknown) return state; } await Task.Delay(50, timeout.Token); } }

    private static StreamProofContext BuildContext(
        string runId,
        string commit,
        Options options,
        DateTimeOffset started,
        DateTimeOffset? ended,
        StreamProofRunStatus status,
        string? stop,
        int setupInputs,
        int swipes,
        StreamingFrameSource source,
        FfmpegBgraVideoFrameDecoder decoder,
        ScrcpyReadOnlyVideoTransport transport,
        long leaseBaseline,
        string shutdown)
    {
        return new(
            runId,
            commit,
            options.Device,
            options.InventoryQuery,
            started,
            ended,
            options.Items,
            status,
            stop,
            setupInputs,
            swipes,
            0,
            0,
            new(
                source.FramesPublished,
                decoder.CompleteBgraFramesAssembled,
                source.FramesEvicted,
                source.PeakBufferDepth,
                transport.TcpBytesReceived,
                transport.EncodedPacketsPublished,
                transport.Metadata?.Width,
                transport.Metadata?.Height,
                RetainedFrame.ActiveReferences - leaseBaseline,
                source.IsRunning,
                transport.Lifecycle.ToString(),
                decoder.FfmpegExitCode,
                shutdown,
                source.LastError?.Message));
    }

    private static int CountSetupInputs(string output, int swipes)
    {
        var directory = Path.Combine(output, "named-evidence");
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var actualInputs = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                if (document.RootElement.TryGetProperty("InputSent", out var inputSent) &&
                    inputSent.ValueKind == JsonValueKind.True)
                {
                    actualInputs++;
                }
            }
            catch (JsonException)
            {
                // An incomplete audit file cannot inflate the input count.
            }
        }
        return Math.Max(0, actualInputs - swipes);
    }

    private static async Task<string> WriteCropAsync(
        string output,
        int ordinal,
        string fileName,
        PixelImage image,
        GateProfile profile,
        string regionName,
        CancellationToken cancellationToken)
    {
        var configured = profile.Regions.Single(x =>
            string.Equals(x.Name, regionName, StringComparison.Ordinal));
        var region = new PogoInventory.Vision.Models.NormalizedRegion
        {
            X = configured.Region.X,
            Y = configured.Region.Y,
            Width = configured.Region.Width,
            Height = configured.Region.Height
        };
        var crop = AppraisalImageDiagnostics.Crop(image, region);
        var relative = $"items/item-{ordinal:000}/{fileName}";
        await File.WriteAllBytesAsync(
            Path.Combine(output, FromRelative(relative)),
            PngEncoder.Encode(crop),
            cancellationToken);
        return relative;
    }

    private static string FromRelative(string relative) =>
        relative.Replace('/', Path.DirectorySeparatorChar);

    private static string ReadGitCommit()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return "Unknown";
            }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return "Unknown";
            }

            var commit = output.Trim();
            using var status = Process.Start(new ProcessStartInfo(
                "git", "status --porcelain=v1 --untracked-files=normal")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (status is null)
            {
                return commit + "+dirty-unknown";
            }
            var changes = status.StandardOutput.ReadToEnd();
            status.WaitForExit();
            return status.ExitCode == 0 && string.IsNullOrWhiteSpace(changes)
                ? commit
                : commit + "+dirty";
        }
        catch
        {
            return "Unknown";
        }
    }

    private sealed record Options(
        string Device,
        string Profile,
        string AppraisalProfile,
        int Items,
        string Output,
        string Adb,
        string Ffmpeg,
        string ScrcpyServer,
        string AutomationProfile,
        string InventoryQuery)
    {
        public static Options Parse(string[] args)
        {
            string Get(string name, string? fallback = null)
            {
                var index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length
                    ? args[index + 1]
                    : fallback ?? throw new ArgumentException($"{name} is required.");
            }
            var root = Directory.GetCurrentDirectory();
            var items = int.Parse(Get("--items"));
            if (items is < 1 or > 1000)
            {
                throw new ArgumentOutOfRangeException("--items", "Items must be from 1 through 1000.");
            }
            return new(
                Get("--device"),
                Get("--profile"),
                Get("--appraisal-profile"),
                items,
                Path.GetFullPath(Get("--out")),
                Get("--adb", Path.Combine(root, "tools", "platform-tools", "adb.exe")),
                Get("--ffmpeg", Path.Combine(root, "tools", "local", "ffmpeg", "ffmpeg-8.1.2-essentials_build", "bin", "ffmpeg.exe")),
                Get("--scrcpy-server", Path.Combine(root, "tools", "local", "scrcpy", "scrcpy-win64-v4.0", "scrcpy-server")),
                Get("--automation-profile", Path.Combine(root, "local-data", "automation-profile.local.json")),
                InventorySearchQuery.Validate(Get("--query", "age0-9999")));
        }
    }
    private sealed record FrameMarker(long FrameId, DateTimeOffset CapturedAtUtc);
    private sealed record SettledFrame(long Id, DateTimeOffset CapturedAtUtc, byte[] Bgra, byte[] Png);
    private sealed record SettlingHandoff(AppraisalHandoffStatus Status, IReadOnlyList<SettledFrame> Frames, StreamProofHandoff Report);
}
