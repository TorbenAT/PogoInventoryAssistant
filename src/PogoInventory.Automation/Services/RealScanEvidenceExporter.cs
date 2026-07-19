using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Automation.Models;
using PogoInventory.Core.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Services;

public static class RealScanEvidenceExporter
{

    public static async Task<RealScanExportResult> ExportAsync(
        string checkpointPath,
        string appraisalProfilePath,
        string outputDirectory,
        string calibrationDirectory,
        RealScanExportOptions? exportOptions = null,
        CancellationToken cancellationToken = default)
    {
        var options = exportOptions ?? new RealScanExportOptions();
        options.Validate();
        var fullCheckpointPath = Path.GetFullPath(checkpointPath);
        var sourceDirectory = Path.GetDirectoryName(fullCheckpointPath) ??
            throw new InvalidOperationException("Checkpoint path has no parent directory.");
        var checkpoint = await InventoryScanCheckpointRepository.LoadAsync(
            sourceDirectory,
            cancellationToken) ??
            throw new InvalidOperationException($"Checkpoint not found: {fullCheckpointPath}");
        if (!Path.GetFullPath(Path.Combine(sourceDirectory, InventoryScanCheckpointRepository.FileName))
            .Equals(fullCheckpointPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Checkpoint must be named {InventoryScanCheckpointRepository.FileName}.");
        }

        var captureStates = ValidateRun(checkpoint, options);
        var profile = await AppraisalProfileLoader.LoadAsync(
            appraisalProfilePath,
            cancellationToken);
        if (profile.Verified)
        {
            throw new InvalidOperationException(
                "The real-phone evidence export expects an unverified Candidate-only profile.");
        }

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var fullCalibrationDirectory = Path.GetFullPath(calibrationDirectory);
        var screenshotsDirectory = Path.Combine(fullOutputDirectory, "screenshots");
        var overlaysDirectory = Path.Combine(fullOutputDirectory, "overlays");
        var checkpointsDirectory = Path.Combine(fullOutputDirectory, "checkpoints");
        Directory.CreateDirectory(fullOutputDirectory);
        if (options.CopyScreenshots)
        {
            Directory.CreateDirectory(screenshotsDirectory);
        }
        if (options.GenerateOverlays)
        {
            Directory.CreateDirectory(overlaysDirectory);
        }
        if (options.GenerateCheckpointEvidence)
        {
            Directory.CreateDirectory(checkpointsDirectory);
        }
        Directory.CreateDirectory(fullCalibrationDirectory);

        var checkpointHash = await HashFileAsync(fullCheckpointPath, cancellationToken);
        var deviceProfileHash = await HashFileAsync(
            Path.GetFullPath(appraisalProfilePath),
            cancellationToken);
        var analyzer = new AppraisalAnalyzer();
        var analyses = new List<(InventoryScanItem Item, AppraisalAnalysisResult Analysis)>();

        foreach (var item in checkpoint.Items.OrderBy(item => item.SequenceNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceScreenshot = ResolveEvidencePath(
                sourceDirectory,
                item.ScreenshotFileName);
            var actualHash = await HashFileAsync(sourceScreenshot, cancellationToken);
            if (!actualHash.Equals(item.ScreenshotSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Screenshot hash mismatch for sequence {item.SequenceNumber}.");
            }

            var bytes = await File.ReadAllBytesAsync(sourceScreenshot, cancellationToken);
            var image = PngDecoder.Decode(bytes);
            var analysis = analyzer.Analyze(image, profile, allowComplete: false);
            analyses.Add((item, analysis));

            var fileName = $"{item.SequenceNumber:D6}.png";
            if (options.CopyScreenshots)
            {
                File.Copy(sourceScreenshot, Path.Combine(screenshotsDirectory, fileName), overwrite: true);
            }
            if (options.GenerateOverlays)
            {
                var overlay = AppraisalImageDiagnostics.DrawOverlay(image, analysis);
                await File.WriteAllBytesAsync(
                    Path.Combine(overlaysDirectory, $"{item.SequenceNumber:D6}-overlay.png"),
                    PngEncoder.Encode(overlay),
                    cancellationToken);
            }
        }

        var observations = BuildObservations(
            checkpoint,
            analyses,
            deviceProfileHash,
            captureStates,
            sourceDirectory,
            fullOutputDirectory,
            options);
        var decisions = observations.Select(BuildReviewDecision).ToArray();
        var auditPath = Path.Combine(fullOutputDirectory, "navigation-audit.jsonl");
        await WriteJsonLinesAsync(auditPath, checkpoint.Actions, cancellationToken);
        await WriteJsonLinesAsync(
            Path.Combine(fullOutputDirectory, "observations.jsonl"),
            observations,
            cancellationToken);
        await WriteObservationCsvAsync(
            Path.Combine(fullOutputDirectory, "observations.csv"),
            observations,
            cancellationToken);
        await WriteDecisionCsvAsync(
            Path.Combine(fullOutputDirectory, "decision-plan.csv"),
            decisions,
            cancellationToken);
        var decisionPlanPath = Path.Combine(fullOutputDirectory, "decision-plan.md");
        await WriteDecisionMarkdownAsync(decisionPlanPath, decisions, cancellationToken);
        if (options.GenerateCheckpointEvidence)
        {
            await WriteCheckpointEvidenceAsync(
                checkpointsDirectory,
                checkpoint,
                checkpointHash,
                cancellationToken);
        }

        var calibration = await WriteCalibrationAsync(
            analyses,
            fullCalibrationDirectory,
            overlaysDirectory,
            options.GenerateOverlays,
            cancellationToken);
        var swipes = checkpoint.Actions.Count(action =>
            action.Kind == AutomationActionKind.SwipeNextPokemon &&
            action.StateBefore == ScreenState.AppraisalOpen &&
            action.StateAfter == ScreenState.AppraisalOpen);
        var unknownStops = checkpoint.Actions.Count(action =>
            action.StateBefore == ScreenState.Unknown ||
            action.StateAfter == ScreenState.Unknown);
        var candidateCount = observations.Count(item =>
            item.ObservationStatus == "Candidate");
        var incompleteCount = observations.Count(item =>
            item.ObservationStatus == "Incomplete");
        var uniqueFrames = observations
            .Select(item => item.ScreenshotSha256)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var uniqueFingerprints = observations
            .Select(item => item.IdentityFingerprintSha256)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var expectedCount = options.ExpectedItems ?? checkpoint.Items.Count;
        var passed =
            checkpoint.Items.Count == expectedCount &&
            uniqueFrames == checkpoint.Items.Count &&
            uniqueFingerprints == checkpoint.Items.Count &&
            swipes == Math.Max(0, checkpoint.Items.Count - 1) &&
            unknownStops == 0 &&
            candidateCount + incompleteCount == checkpoint.Items.Count &&
            calibration.Stable;

        var manifest = new RealScanRunManifest
        {
            RunId = checkpoint.RunId,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            StartedAtUtc = checkpoint.StartedAtUtc,
            EndedAtUtc = checkpoint.CompletedAtUtc ?? checkpoint.UpdatedAtUtc,
            Duration = ((checkpoint.CompletedAtUtc ?? checkpoint.UpdatedAtUtc) -
                checkpoint.StartedAtUtc).ToString("c", CultureInfo.InvariantCulture),
            SourceCheckpointSha256 = checkpointHash,
            DeviceSerial = checkpoint.DeviceSerial,
            DeviceProfileHash = deviceProfileHash,
            AppraisalProfileHash = deviceProfileHash,
            AutomationProfileHash = checkpoint.AutomationProfileSha256,
            ScreenProfileHash = checkpoint.ScreenProfileSha256,
            Scanned = observations.Count,
            RequestedMaximumItems = options.RequestedMaximumItems ?? options.ExpectedItems,
            ActualItemCount = observations.Count,
            UniqueScreenshotCount = uniqueFrames,
            UniqueFingerprintCount = uniqueFingerprints,
            UniqueChangedFrames = Math.Min(uniqueFrames, uniqueFingerprints),
            SwipesSucceeded = swipes,
            UnknownStops = unknownStops,
            CandidateObservations = candidateCount,
            IncompleteObservations = incompleteCount,
            CompleteObservations = analyses.Count(item => item.Analysis.IsComplete),
            TransferActions = 0,
            StopReason = checkpoint.StopReason.ToString(),
            OutputByteCount = 0,
            SafeEvidenceOnlyOutput = true,
            VariantSchemaReady = true,
            ExactVariantIdentities = observations.Count(item =>
                item.VariantIdentity.VariantKey is not null),
            Keep = decisions.Count(item => item.Recommendation == DecisionCategory.Keep),
            Review = decisions.Count(item => item.Recommendation == DecisionCategory.Review),
            Delete = decisions.Count(item => item.Recommendation == DecisionCategory.Delete),
            RealPhoneDemoPassed = passed
        };
        manifest = manifest with
        {
            OutputByteCount = Directory.EnumerateFiles(
                    fullOutputDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length)
        };
        var manifestPath = Path.Combine(fullOutputDirectory, "run-manifest.json");
        await WriteJsonAsync(manifestPath, manifest, cancellationToken);
        var reportPath = Path.Combine(fullOutputDirectory, "night-report.md");
        await WriteReportAsync(
            reportPath,
            checkpoint,
            manifest,
            calibration,
            cancellationToken);

        return new RealScanExportResult
        {
            Manifest = manifest,
            ManifestPath = manifestPath,
            ReportPath = reportPath,
            DecisionPlanPath = decisionPlanPath,
            CalibrationJsonPath = calibration.JsonPath,
            CalibrationMarkdownPath = calibration.MarkdownPath,
            CalibrationCases = calibration.CaseCount,
            CalibrationStable = calibration.Stable
        };
    }

    private static IReadOnlyDictionary<int, ScreenState> ValidateRun(
        InventoryScanCheckpoint checkpoint,
        RealScanExportOptions options)
    {
        var itemCountAccepted = options.ExpectedItems is { } expected
            ? checkpoint.Items.Count == expected
            : checkpoint.Items.Count >= (options.MinimumItems ?? 1);
        if (!itemCountAccepted ||
            checkpoint.Status == AutomationRunStatus.Running)
        {
            throw new InvalidOperationException(
                "Evidence export requires a finished checkpoint with the configured item count.");
        }

        var ordered = checkpoint.Items.OrderBy(item => item.SequenceNumber).ToArray();
        if (ordered.Select(item => item.SequenceNumber)
            .SequenceEqual(Enumerable.Range(1, ordered.Length)) is false)
        {
            throw new InvalidOperationException(
                "Checkpoint item sequences must be continuous and unique.");
        }

        if (checkpoint.Items.Select(item => item.ScreenshotSha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != checkpoint.Items.Count ||
            checkpoint.Items.Select(item => item.IdentityFingerprintSha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != checkpoint.Items.Count)
        {
            throw new InvalidOperationException(
                "All screenshot and identity fingerprint hashes must be unique.");
        }

        var swipes = checkpoint.Actions.Where(action =>
            action.Kind == AutomationActionKind.SwipeNextPokemon).ToArray();
        var verifiedSwipes = swipes.Where(action =>
            action.StateBefore == ScreenState.AppraisalOpen &&
            action.StateAfter == ScreenState.AppraisalOpen).ToArray();
        var invalidSwipes = swipes.Except(verifiedSwipes).ToArray();
        var terminalUnknown = checkpoint.StopReason == AutomationStopReason.UnknownScreen &&
            invalidSwipes.Length == 1 &&
            invalidSwipes[0] == swipes[^1] &&
            invalidSwipes[0].StateAfter == ScreenState.Unknown;
        var swipeCountValid = terminalUnknown
            ? verifiedSwipes.Length >= Math.Max(0, checkpoint.Items.Count - 1)
            : verifiedSwipes.Length == Math.Max(0, checkpoint.Items.Count - 1) &&
              invalidSwipes.Length == 0;
        if (!swipeCountValid)
        {
            throw new InvalidOperationException(
                "The checkpoint must contain one verified AppraisalOpen swipe between each item.");
        }

        var states = new Dictionary<int, ScreenState>();
        foreach (var item in ordered)
        {
            var captures = checkpoint.Actions.Where(action =>
                action.Kind == AutomationActionKind.CaptureEvidence &&
                string.Equals(action.Detail, item.ScreenshotFileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (captures.Length != 1 ||
                captures[0].StateBefore != ScreenState.AppraisalOpen ||
                captures[0].StateAfter != ScreenState.AppraisalOpen)
            {
                throw new InvalidOperationException(
                    $"Sequence {item.SequenceNumber} requires exactly one proven AppraisalOpen capture action.");
            }
            states[item.SequenceNumber] = captures[0].StateAfter!.Value;
        }

        return states;
    }

    private static IReadOnlyList<RealScanObservationRecord> BuildObservations(
        InventoryScanCheckpoint checkpoint,
        IReadOnlyList<(InventoryScanItem Item, AppraisalAnalysisResult Analysis)> analyses,
        string deviceProfileHash,
        IReadOnlyDictionary<int, ScreenState> captureStates,
        string sourceDirectory,
        string outputDirectory,
        RealScanExportOptions options)
    {
        var ordered = analyses.OrderBy(value => value.Item.SequenceNumber).ToArray();
        return ordered.Select((value, index) =>
        {
            var item = value.Item;
            var analysis = value.Analysis;
            var variant = new PokemonVariantIdentity
            {
                VariantIdentityConfidence = IdentityConfidence.Unknown,
                EvidenceReferences = EvidenceReferences(
                    item,
                    sourceDirectory,
                    outputDirectory,
                    options)
            };
            var instanceKey = $"{checkpoint.RunId}:{item.SequenceNumber:D6}:{item.ScreenshotSha256[..12]}";
            var captureAction = checkpoint.Actions.Single(action =>
                action.Kind == AutomationActionKind.CaptureEvidence &&
                string.Equals(
                    action.Detail,
                    item.ScreenshotFileName,
                    StringComparison.OrdinalIgnoreCase));
            var instance = new PokemonInstanceEvidence
            {
                ScanRunId = checkpoint.RunId,
                Sequence = item.SequenceNumber,
                InstanceEvidenceKey = instanceKey,
                ScreenshotSha256 = item.ScreenshotSha256,
                IdentityFingerprintSha256 = item.IdentityFingerprintSha256,
                PreviousIdentityFingerprintSha256 = index == 0
                    ? null
                    : ordered[index - 1].Item.IdentityFingerprintSha256,
                NextIdentityFingerprintSha256 = index == ordered.Length - 1
                    ? null
                    : ordered[index + 1].Item.IdentityFingerprintSha256,
                CaptureTimestampUtc = item.CapturedAtUtc,
                DeviceProfileHash = deviceProfileHash,
                NavigationAuditReference =
                    $"navigation-audit.jsonl#action-{captureAction.SequenceNumber:D6}"
            };
            var isCandidate = analysis.Status == AppraisalAnalysisStatus.Candidate;
            return new RealScanObservationRecord
            {
                Sequence = item.SequenceNumber,
                TimestampUtc = item.CapturedAtUtc,
                DetectedState = captureStates[item.SequenceNumber],
                StateConfidence = item.ScreenStateConfidence,
                ScreenshotSha256 = item.ScreenshotSha256,
                IdentityFingerprintSha256 = item.IdentityFingerprintSha256,
                ProviderName = item.Observation.ProviderName,
                ProviderVersion = item.Observation.ProviderVersion,
                AttackIv = analysis.AttackIv,
                DefenseIv = analysis.DefenseIv,
                HpIv = analysis.HpIv,
                AppraisalConfidence = analysis.Confidence,
                AppraisalStatus = analysis.Status.ToString(),
                ProviderStatus = item.Observation.Status,
                RawProviderOutput = item.Observation.RawProviderOutput,
                RawProviderOutputSha256 = item.Observation.RawProviderOutputSha256,
                VariantIdentity = variant,
                Cp = item.Observation.Cp,
                CatchLocation = item.Observation.CatchLocation,
                ObservationStatus = isCandidate ? "Candidate" : "Incomplete",
                ErrorCode = isCandidate ? null : "AppraisalCandidateNotConfirmed",
                ErrorDetail = isCandidate ? null : analysis.Detail,
                InstanceEvidence = instance,
                EvidenceReferences = variant.EvidenceReferences
            };
        }).ToArray();
    }

    private static RealScanDecisionRow BuildReviewDecision(RealScanObservationRecord observation)
    {
        var missing = observation.MissingVariantFields
            .Concat(new[]
            {
                "Cp",
                "IsFavorite",
                "HasSpecialMove",
                "IsXxl",
                "IsXxs",
                "CatchDate"
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new RealScanDecisionRow
        {
            Sequence = observation.Sequence,
            InstanceEvidenceKey = observation.InstanceEvidence.InstanceEvidenceKey,
            VariantKey = observation.VariantIdentity.VariantKey,
            Species = observation.SpeciesName,
            Form = observation.FormName,
            Costume = observation.CostumeName,
            Background = observation.BackgroundName,
            Shiny = observation.IsShiny,
            ShadowState = observation.ShadowState,
            DynamaxState = observation.DynamaxState,
            Cp = observation.Cp,
            AttackIv = observation.AttackIv,
            DefenseIv = observation.DefenseIv,
            HpIv = observation.HpIv,
            VariantIdentityConfidence = observation.VariantIdentityConfidence,
            ProtectionDataConfidence = IdentityConfidence.Unknown,
            Recommendation = DecisionCategory.Review,
            Confidence = 1,
            Reason = "Exact semantic variant identity and protection data are unknown; DELETE is forbidden.",
            MissingEvidence = missing,
            BetterDuplicateSequence = null
        };
    }

    private static async Task<CalibrationResult> WriteCalibrationAsync(
        IReadOnlyList<(InventoryScanItem Item, AppraisalAnalysisResult Analysis)> analyses,
        string calibrationDirectory,
        string overlayDirectory,
        bool overlaysGenerated,
        CancellationToken cancellationToken)
    {
        var candidates = analyses
            .Where(item =>
                item.Analysis.Status == AppraisalAnalysisStatus.Candidate &&
                item.Analysis.Bars.All(bar => bar.TrackDetected))
            .ToArray();
        var selected = SelectDiverseCalibrationCases(candidates, 3);
        var scales = selected.Select(item => item.Analysis.Transform.Scale).ToArray();
        var xOffsets = selected.Select(item => item.Analysis.Transform.XOffset).ToArray();
        var yOffsets = selected.Select(item => item.Analysis.Transform.YOffset).ToArray();
        var scaleSpread = scales.Length == 0
            ? double.PositiveInfinity
            : (scales.Max() - scales.Min()) / Math.Max(scales.Min(), 0.000001);
        var translationSpread = selected.Length == 0
            ? double.PositiveInfinity
            : Math.Max(xOffsets.Max() - xOffsets.Min(), yOffsets.Max() - yOffsets.Min());
        var stable =
            selected.Length == 3 &&
            scaleSpread <= 0.02 &&
            translationSpread <= 0.015 &&
            selected.All(item => !item.Analysis.IsComplete);

        for (var index = 0; overlaysGenerated && index < selected.Length; index++)
        {
            File.Copy(
                Path.Combine(overlayDirectory, $"{selected[index].Item.SequenceNumber:D6}-overlay.png"),
                Path.Combine(calibrationDirectory, $"case-{index + 1:D2}-overlay.png"),
                overwrite: true);
        }

        var payload = new
        {
            schemaVersion = "1.0",
            generatedAtUtc = DateTimeOffset.UtcNow,
            caseCount = selected.Length,
            stable,
            scaleSpread,
            normalizedTranslationSpread = translationSpread,
            completeCount = selected.Count(item => item.Analysis.IsComplete),
            cases = selected.Select((item, index) => new
            {
                caseNumber = index + 1,
                sequence = item.Item.SequenceNumber,
                screenshotSha256 = item.Item.ScreenshotSha256,
                status = item.Analysis.Status,
                confidence = item.Analysis.Confidence,
                transform = item.Analysis.Transform,
                attackIv = item.Analysis.AttackIv,
                defenseIv = item.Analysis.DefenseIv,
                hpIv = item.Analysis.HpIv,
                bars = item.Analysis.Bars,
                overlayFile = overlaysGenerated
                    ? $"case-{index + 1:D2}-overlay.png"
                    : null,
                selectionReason = "Greedy maximum minimum Euclidean distance between IV vectors."
            })
        };
        var jsonPath = Path.Combine(calibrationDirectory, "phone-calibration-stability.json");
        await WriteJsonAsync(jsonPath, payload, cancellationToken);
        var markdownPath = Path.Combine(calibrationDirectory, "phone-calibration-stability.md");
        var markdown = new StringBuilder()
            .AppendLine("# Phone calibration stability")
            .AppendLine()
            .AppendLine($"- Cases: {selected.Length}/3")
            .AppendLine($"- Stable: {stable}")
            .AppendLine($"- Scale spread: {scaleSpread:P2}")
            .AppendLine($"- Normalized translation spread: {translationSpread:P2}")
            .AppendLine("- Complete results: 0")
            .AppendLine("- Selection: distinct IV triplets chosen by maximum-distance sampling")
            .AppendLine()
            .AppendLine("| Case | Source sequence | Attack | Defense | HP | Confidence |")
            .AppendLine("|---:|---:|---:|---:|---:|---:|");
        for (var index = 0; index < selected.Length; index++)
        {
            var item = selected[index];
            markdown.AppendLine(
                $"| {index + 1} | {item.Item.SequenceNumber} | {Value(item.Analysis.AttackIv)} | " +
                $"{Value(item.Analysis.DefenseIv)} | {Value(item.Analysis.HpIv)} | " +
                $"{item.Analysis.Confidence:F4} |");
        }
        await File.WriteAllTextAsync(markdownPath, markdown.ToString(), cancellationToken);
        return new CalibrationResult(selected.Length, stable, jsonPath, markdownPath);
    }

    private static (InventoryScanItem Item, AppraisalAnalysisResult Analysis)[]
        SelectDiverseCalibrationCases(
            IReadOnlyCollection<(InventoryScanItem Item, AppraisalAnalysisResult Analysis)> candidates,
            int count)
    {
        var unique = candidates
            .GroupBy(item => (
                item.Analysis.AttackIv,
                item.Analysis.DefenseIv,
                item.Analysis.HpIv))
            .Select(group => group.OrderBy(item => item.Item.SequenceNumber).First())
            .OrderBy(item => item.Item.SequenceNumber)
            .ToList();
        if (unique.Count <= count)
        {
            return unique.ToArray();
        }

        var selected = new List<(InventoryScanItem Item, AppraisalAnalysisResult Analysis)>
        {
            unique[0]
        };
        while (selected.Count < count)
        {
            var next = unique
                .Where(candidate => !selected.Contains(candidate))
                .OrderByDescending(candidate => selected.Min(chosen =>
                    IvDistance(candidate.Analysis, chosen.Analysis)))
                .ThenBy(candidate => candidate.Item.SequenceNumber)
                .First();
            selected.Add(next);
        }
        return selected.ToArray();
    }

    private static double IvDistance(
        AppraisalAnalysisResult left,
        AppraisalAnalysisResult right)
    {
        var attack = left.AttackIv!.Value - right.AttackIv!.Value;
        var defense = left.DefenseIv!.Value - right.DefenseIv!.Value;
        var hp = left.HpIv!.Value - right.HpIv!.Value;
        return Math.Sqrt(attack * attack + defense * defense + hp * hp);
    }

    private static async Task WriteCheckpointEvidenceAsync(
        string directory,
        InventoryScanCheckpoint checkpoint,
        string sourceCheckpointHash,
        CancellationToken cancellationToken)
    {
        foreach (var item in checkpoint.Items.OrderBy(item => item.SequenceNumber))
        {
            var payload = new
            {
                schemaVersion = "1.0",
                checkpointSequence = item.SequenceNumber,
                checkpointPersistedByRunner = true,
                sourceFinalCheckpointSha256 = sourceCheckpointHash,
                runId = checkpoint.RunId,
                item,
                actionsThroughItem = checkpoint.Actions
                    .Where(action => action.CompletedAtUtc <= item.CapturedAtUtc)
                    .ToArray()
            };
            await WriteJsonAsync(
                Path.Combine(directory, $"checkpoint-{item.SequenceNumber:D6}.json"),
                payload,
                cancellationToken);
        }
    }

    private static async Task WriteObservationCsvAsync(
        string path,
        IReadOnlyList<RealScanObservationRecord> observations,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            "sequence,timestampUtc,detectedState,stateConfidence,screenshotSha256,identityFingerprintSha256,providerName,attackIv,defenseIv,hpIv,appraisalConfidence,speciesId,speciesName,formId,formName,costumeId,costumeName,backgroundId,backgroundName,shiny,shadowState,luckyState,dynamaxState,variantIdentityConfidence,missingVariantFields,observationStatus,errorCode,errorDetail,evidenceReferences"
        };
        lines.AddRange(observations.Select(item => Csv(
            item.Sequence,
            item.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            item.DetectedState,
            item.StateConfidence,
            item.ScreenshotSha256,
            item.IdentityFingerprintSha256,
            item.ProviderName,
            item.AttackIv,
            item.DefenseIv,
            item.HpIv,
            item.AppraisalConfidence,
            item.SpeciesId,
            item.SpeciesName,
            item.FormId,
            item.FormName,
            item.CostumeId,
            item.CostumeName,
            item.BackgroundId,
            item.BackgroundName,
            item.IsShiny,
            item.ShadowState,
            item.LuckyState,
            item.DynamaxState,
            item.VariantIdentityConfidence,
            string.Join(';', item.MissingVariantFields),
            item.ObservationStatus,
            item.ErrorCode,
            item.ErrorDetail,
            string.Join(';', item.EvidenceReferences))));
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static async Task WriteDecisionCsvAsync(
        string path,
        IReadOnlyList<RealScanDecisionRow> decisions,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            "sequence,instanceEvidenceKey,variantKey,species,form,costume,background,shiny,shadowState,dynamaxState,cp,attackIv,defenseIv,hpIv,variantIdentityConfidence,protectionDataConfidence,recommendation,confidence,reason,missingEvidence,betterDuplicateSequence"
        };
        lines.AddRange(decisions.Select(item => Csv(
            item.Sequence,
            item.InstanceEvidenceKey,
            item.VariantKey,
            item.Species,
            item.Form,
            item.Costume,
            item.Background,
            item.Shiny,
            item.ShadowState,
            item.DynamaxState,
            item.Cp,
            item.AttackIv,
            item.DefenseIv,
            item.HpIv,
            item.VariantIdentityConfidence,
            item.ProtectionDataConfidence,
            item.Recommendation,
            item.Confidence,
            item.Reason,
            string.Join(';', item.MissingEvidence),
            item.BetterDuplicateSequence)));
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static async Task WriteDecisionMarkdownAsync(
        string path,
        IReadOnlyList<RealScanDecisionRow> decisions,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder()
            .AppendLine("# Conservative decision plan")
            .AppendLine()
            .AppendLine("All scanned items remain REVIEW because exact semantic variant identity and protection data are not available. No transfer action is generated.")
            .AppendLine()
            .AppendLine("| Seq | Variant | Candidate IV | Decision | Reason |")
            .AppendLine("|---:|---|---|---|---|");
        foreach (var item in decisions)
        {
            text.AppendLine(
                $"| {item.Sequence} | {item.VariantKey ?? "Unknown"} | " +
                $"{Value(item.AttackIv)}/{Value(item.DefenseIv)}/{Value(item.HpIv)} | " +
                $"{item.Recommendation} | {item.Reason} |");
        }
        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
    }

    private static async Task WriteReportAsync(
        string path,
        InventoryScanCheckpoint checkpoint,
        RealScanRunManifest manifest,
        CalibrationResult calibration,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder()
            .AppendLine("# Real phone scan report")
            .AppendLine()
            .AppendLine($"- Run: {manifest.RunId}")
            .AppendLine($"- Source status: {checkpoint.Status} / {checkpoint.StopReason}")
            .AppendLine($"- Items: {manifest.Scanned}/{manifest.RequestedMaximumItems?.ToString(CultureInfo.InvariantCulture) ?? "unbounded"}")
            .AppendLine($"- Unique changed frames: {manifest.UniqueChangedFrames}/{manifest.Scanned}")
            .AppendLine($"- Verified swipes: {manifest.SwipesSucceeded}/{Math.Max(0, manifest.Scanned - 1)}")
            .AppendLine($"- Unknown states: {manifest.UnknownStops}")
            .AppendLine($"- Candidate observations: {manifest.CandidateObservations}")
            .AppendLine($"- Incomplete observations: {manifest.IncompleteObservations}")
            .AppendLine($"- Complete observations: {manifest.CompleteObservations}")
            .AppendLine($"- Calibration: {calibration.CaseCount}/3, stable={calibration.Stable}")
            .AppendLine($"- Decisions: KEEP {manifest.Keep}, REVIEW {manifest.Review}, DELETE {manifest.Delete}")
            .AppendLine("- Transfer actions: 0")
            .AppendLine($"- Real phone demo: {(manifest.RealPhoneDemoPassed ? "PASS" : "FAIL")}")
            .AppendLine()
            .AppendLine("The fingerprint hashes are run evidence only. They are not semantic or permanent Pokemon identifiers.");
        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
    }

    private static async Task WriteJsonLinesAsync<T>(
        string path,
        IEnumerable<T> values,
        CancellationToken cancellationToken)
    {
        var options = AutomationJson.CreateOptions(writeIndented: false);
        var lines = values.Select(value => JsonSerializer.Serialize(value, options));
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, AutomationJson.CreateOptions(writeIndented: true)),
            cancellationToken);

    private static IReadOnlyCollection<string> EvidenceReferences(
        InventoryScanItem item,
        string sourceDirectory,
        string outputDirectory,
        RealScanExportOptions options)
    {
        var references = new List<string>();
        if (options.CopyScreenshots)
        {
            references.Add($"screenshots/{item.SequenceNumber:D6}.png");
        }
        else
        {
            var source = ResolveEvidencePath(sourceDirectory, item.ScreenshotFileName);
            references.Add(Path.GetRelativePath(outputDirectory, source).Replace('\\', '/'));
        }
        if (options.GenerateOverlays)
        {
            references.Add($"overlays/{item.SequenceNumber:D6}-overlay.png");
        }
        return references;
    }

    private static string ResolveEvidencePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Evidence paths must be relative to the scan directory.");
        }

        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Evidence path escapes the scan directory.");
        }
        return fullPath;
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Csv(params object?[] values) =>
        string.Join(',', values.Select(value =>
        {
            var text = value switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
            return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? $"\"{text.Replace("\"", "\"\"")}\""
                : text;
        }));

    private static string Value(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

    private sealed record CalibrationResult(
        int CaseCount,
        bool Stable,
        string JsonPath,
        string MarkdownPath);
}
