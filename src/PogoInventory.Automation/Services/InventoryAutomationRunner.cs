using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PogoInventory.Automation.Errors;
using PogoInventory.Automation.Models;
using PogoInventory.Device;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Observations.Models;
using PogoInventory.Observations.Providers;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;

namespace PogoInventory.Automation.Services;

public sealed class InventoryAutomationRunner
{
    private readonly IAndroidAutomationTransport _transport;
    private readonly ScreenStateDetector _detector;
    private readonly IDeviceLog _log;
    private readonly ICalcyObservationProvider _observationProvider;

    public InventoryAutomationRunner(
        IAndroidAutomationTransport transport,
        ScreenStateDetector? detector = null,
        IDeviceLog? log = null,
        ICalcyObservationProvider? observationProvider = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _detector = detector ?? new ScreenStateDetector();
        _log = log ?? NullDeviceLog.Instance;
        _observationProvider = observationProvider ??
            new UnavailableCalcyObservationProvider();
    }

    public async Task<InventoryAutomationResult> RunAsync(
        string outputDirectory,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        string? requestedSerial = null,
        int? maximumItems = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(automationProfile);
        ArgumentNullException.ThrowIfNull(screenProfile);
        automationProfile.Validate();
        ScreenProfileLoader.Validate(screenProfile);

        var automationProfileHash = Hash(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(
                automationProfile,
                AutomationJson.CreateOptions(writeIndented: false))));
        var screenProfileHash = Hash(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(
                screenProfile,
                ScreenProfileLoader.CreateJsonOptions(writeIndented: false))));

        var maxItems = maximumItems ?? automationProfile.DefaultMaximumItems;
        if (maxItems <= 0 || maxItems > 50000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumItems),
                "Maximum items must be between 1 and 50000.");
        }

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var captureDirectory = Path.Combine(fullOutputDirectory, "captures");
        Directory.CreateDirectory(captureDirectory);

        var devices = await _transport.ListDevicesAsync(cancellationToken);
        var selected = DeviceSnapshotService.SelectDevice(devices, requestedSerial);
        var metadata = await _transport.ReadMetadataAsync(selected.Serial, cancellationToken);
        var width = metadata.Screen.EffectiveWidth ??
            throw new AutomationException(
                AutomationErrorCode.GeometryMismatch,
                "Android device did not report an effective screen width.");
        var height = metadata.Screen.EffectiveHeight ??
            throw new AutomationException(
                AutomationErrorCode.GeometryMismatch,
                "Android device did not report an effective screen height.");

        ValidateBattery(metadata, automationProfile);
        _log.Write(
            DeviceLogLevel.Information,
            "inventory.automation.start",
            "Automatic inventory navigation started.",
            new Dictionary<string, string>
            {
                ["serial"] = selected.Serial,
                ["geometry"] = $"{width}x{height}",
                ["maximumItems"] = maxItems.ToString()
            });

        var existing = await InventoryScanCheckpointRepository.LoadAsync(
            fullOutputDirectory,
            cancellationToken);
        var checkpoint = existing ?? new InventoryScanCheckpoint
        {
            RunId = $"scan-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..34],
            AutomationProfileName = automationProfile.Name,
            AutomationProfileSha256 = automationProfileHash,
            ScreenProfileSha256 = screenProfileHash,
            DeviceSerial = selected.Serial,
            ScreenWidth = width,
            ScreenHeight = height,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = AutomationRunStatus.Running
        };

        ValidateCheckpointContext(
            checkpoint,
            selected.Serial,
            width,
            height,
            automationProfile,
            automationProfileHash,
            screenProfileHash);
        if (checkpoint.Status is AutomationRunStatus.Completed or AutomationRunStatus.Stopped)
        {
            var existingPath = Path.Combine(
                fullOutputDirectory,
                InventoryScanCheckpointRepository.FileName);
            return new InventoryAutomationResult
            {
                Checkpoint = checkpoint,
                CheckpointPath = existingPath,
                CaptureDirectory = captureDirectory
            };
        }

        var items = checkpoint.Items.OrderBy(x => x.SequenceNumber).ToList();
        var actions = checkpoint.Actions.OrderBy(x => x.SequenceNumber).ToList();

        try
        {
            var current = await ProbeAsync(
                selected.Serial,
                automationProfile,
                screenProfile,
                cancellationToken);

            if (items.Count > 0)
            {
                var resumeResult = await PrepareResumeAsync(
                    selected.Serial,
                    current,
                    items[^1],
                    width,
                    height,
                    automationProfile,
                    screenProfile,
                    actions,
                    cancellationToken);

                if (resumeResult.StopReason != AutomationStopReason.None)
                {
                    return await FinishAsync(
                        fullOutputDirectory,
                        captureDirectory,
                        checkpoint,
                        items,
                        actions,
                        AutomationRunStatus.Stopped,
                        resumeResult.StopReason,
                        resumeResult.Detail,
                        cancellationToken);
                }

                current = resumeResult.Probe ?? throw new AutomationException(
                    AutomationErrorCode.ResumeMismatch,
                    "Resume preparation did not return a current screen probe.");
            }
            else
            {
                var startResult = await ReachAppraisalAsync(
                    selected.Serial,
                    current,
                    width,
                    height,
                    automationProfile,
                    screenProfile,
                    actions,
                    cancellationToken);

                if (startResult.StopReason != AutomationStopReason.None)
                {
                    return await FinishAsync(
                        fullOutputDirectory,
                        captureDirectory,
                        checkpoint,
                        items,
                        actions,
                        AutomationRunStatus.Stopped,
                        startResult.StopReason,
                        startResult.Detail,
                        cancellationToken);
                }

                current = startResult.Probe ?? throw new AutomationException(
                    AutomationErrorCode.InvalidStartingState,
                    "Automatic navigation did not reach the appraisal screen.");
            }

            while (items.Count < maxItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (current.Detection.State != ScreenState.AppraisalOpen)
                {
                    var stop = StopForState(current.Detection.State);
                    return await FinishAsync(
                        fullOutputDirectory,
                        captureDirectory,
                        checkpoint,
                        items,
                        actions,
                        AutomationRunStatus.Stopped,
                        stop.Reason,
                        stop.Detail,
                        cancellationToken);
                }

                var item = await CaptureItemAsync(
                    selected.Serial,
                    captureDirectory,
                    items.Count + 1,
                    current,
                    cancellationToken);
                items.Add(item);
                _log.Write(
                    DeviceLogLevel.Information,
                    "inventory.automation.item-captured",
                    "Inventory evidence captured.",
                    new Dictionary<string, string>
                    {
                        ["sequence"] = item.SequenceNumber.ToString(),
                        ["sha256"] = item.ScreenshotSha256
                    });
                actions.Add(new AutomationActionRecord
                {
                    SequenceNumber = actions.Count + 1,
                    Kind = AutomationActionKind.CaptureEvidence,
                    StartedAtUtc = current.CapturedAtUtc,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    StateBefore = ScreenState.AppraisalOpen,
                    StateAfter = ScreenState.AppraisalOpen,
                    Detail = item.ScreenshotFileName
                });
                actions.Add(new AutomationActionRecord
                {
                    SequenceNumber = actions.Count + 1,
                    Kind = AutomationActionKind.CaptureObservation,
                    StartedAtUtc = current.CapturedAtUtc,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    StateBefore = ScreenState.AppraisalOpen,
                    StateAfter = ScreenState.AppraisalOpen,
                    Detail = $"{item.Observation.ProviderName}:{item.Observation.Status}"
                });

                checkpoint = await SaveRunningAsync(
                    fullOutputDirectory,
                    checkpoint,
                    items,
                    actions,
                    cancellationToken);

                if (items.Count >= maxItems)
                {
                    return await FinishAsync(
                        fullOutputDirectory,
                        captureDirectory,
                        checkpoint,
                        items,
                        actions,
                        AutomationRunStatus.Completed,
                        AutomationStopReason.MaximumItemsReached,
                        $"Configured maximum of {maxItems} items reached.",
                        cancellationToken);
                }

                var next = await MoveToNextAsync(
                    selected.Serial,
                    current,
                    width,
                    height,
                    automationProfile,
                    screenProfile,
                    actions,
                    cancellationToken);

                if (next.StopReason != AutomationStopReason.None)
                {
                    var status = next.StopReason == AutomationStopReason.EndOfInventoryDetected
                        ? AutomationRunStatus.Completed
                        : AutomationRunStatus.Stopped;
                    return await FinishAsync(
                        fullOutputDirectory,
                        captureDirectory,
                        checkpoint,
                        items,
                        actions,
                        status,
                        next.StopReason,
                        next.Detail,
                        cancellationToken);
                }

                current = next.Probe ?? throw new AutomationException(
                    AutomationErrorCode.StateTimeout,
                    "Next-Pokémon navigation returned no screen probe.");

                if (items.Count % 100 == 0)
                {
                    metadata = await _transport.ReadMetadataAsync(
                        selected.Serial,
                        cancellationToken);
                    ValidateBattery(metadata, automationProfile);
                }
            }

            throw new InvalidOperationException("Inventory scan loop ended unexpectedly.");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync(
                fullOutputDirectory,
                captureDirectory,
                checkpoint,
                items,
                actions,
                AutomationRunStatus.Stopped,
                AutomationStopReason.Cancelled,
                "Operation was cancelled.",
                CancellationToken.None);
            throw;
        }
        catch (AutomationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await FinishAsync(
                fullOutputDirectory,
                captureDirectory,
                checkpoint,
                items,
                actions,
                AutomationRunStatus.Faulted,
                AutomationStopReason.Failure,
                exception.Message,
                CancellationToken.None);
            throw new AutomationException(
                AutomationErrorCode.TransportFailure,
                "Automatic inventory scan failed.",
                exception);
        }
    }

    private async Task<NavigationResult> ReachAppraisalAsync(
        string serial,
        ScreenProbe current,
        int width,
        int height,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        List<AutomationActionRecord> actions,
        CancellationToken cancellationToken)
    {
        for (var step = 0; step < 4; step++)
        {
            switch (current.Detection.State)
            {
                case ScreenState.InventoryList:
                {
                    var transition = await TapAndWaitAsync(
                        serial,
                        automationProfile.FirstInventoryCard,
                        ScreenState.PokemonDetails,
                        AutomationActionKind.TapFirstInventoryCard,
                        current,
                        width,
                        height,
                        automationProfile,
                        screenProfile,
                        actions,
                        cancellationToken);
                    if (transition.StopReason != AutomationStopReason.None)
                    {
                        return transition;
                    }

                    current = transition.Probe!;
                    break;
                }
                case ScreenState.PokemonDetails:
                {
                    var transition = await TapAndWaitAsync(
                        serial,
                        automationProfile.DetailsMenuButton,
                        ScreenState.PokemonMenuOpen,
                        AutomationActionKind.TapDetailsMenu,
                        current,
                        width,
                        height,
                        automationProfile,
                        screenProfile,
                        actions,
                        cancellationToken);
                    if (transition.StopReason != AutomationStopReason.None)
                    {
                        return transition;
                    }

                    current = transition.Probe!;
                    break;
                }
                case ScreenState.PokemonMenuOpen:
                {
                    var transition = await TapAndWaitAsync(
                        serial,
                        automationProfile.AppraiseMenuItem,
                        ScreenState.AppraisalOpen,
                        AutomationActionKind.TapAppraise,
                        current,
                        width,
                        height,
                        automationProfile,
                        screenProfile,
                        actions,
                        cancellationToken);
                    if (transition.StopReason != AutomationStopReason.None)
                    {
                        return transition;
                    }

                    current = transition.Probe!;
                    break;
                }
                case ScreenState.AppraisalOpen:
                    return NavigationResult.Success(current);
                default:
                    var stop = StopForState(current.Detection.State);
                    return NavigationResult.Stop(stop.Reason, stop.Detail);
            }
        }

        return NavigationResult.Stop(
            AutomationStopReason.UnexpectedScreen,
            "Appraisal screen was not reached within the allowed navigation steps.");
    }

    private async Task<NavigationResult> PrepareResumeAsync(
        string serial,
        ScreenProbe current,
        InventoryScanItem lastItem,
        int width,
        int height,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        List<AutomationActionRecord> actions,
        CancellationToken cancellationToken)
    {
        if (current.Detection.State != ScreenState.AppraisalOpen)
        {
            return NavigationResult.Stop(
                AutomationStopReason.ResumeMismatch,
                $"Resume requires AppraisalOpen, but current state is {current.Detection.State}.");
        }

        var previousFingerprint = Convert.FromBase64String(lastItem.IdentityFingerprintBase64);
        var similarity = FingerprintComparer.Similarity(
            previousFingerprint,
            current.IdentityFingerprint);
        if (similarity < automationProfile.SamePokemonSimilarityThreshold)
        {
            return NavigationResult.Stop(
                AutomationStopReason.ResumeMismatch,
                $"Current Pokémon does not match the last checkpoint item. Similarity={similarity:F6}.");
        }

        return await MoveToNextAsync(
            serial,
            current,
            width,
            height,
            automationProfile,
            screenProfile,
            actions,
            cancellationToken);
    }

    private async Task<NavigationResult> MoveToNextAsync(
        string serial,
        ScreenProbe current,
        int width,
        int height,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        List<AutomationActionRecord> actions,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= automationProfile.MaxSwipeAttemptsAtEnd; attempt++)
        {
            var started = DateTimeOffset.UtcNow;
            var start = automationProfile.NextPokemonSwipe.Start.ToPixels(width, height);
            var end = automationProfile.NextPokemonSwipe.End.ToPixels(width, height);
            await _transport.SwipeAsync(
                serial,
                start.X,
                start.Y,
                end.X,
                end.Y,
                automationProfile.NextPokemonSwipe.DurationMilliseconds,
                cancellationToken);
            await DelayAsync(automationProfile.PostActionSettleMilliseconds, cancellationToken);

            var wait = await WaitForChangedAppraisalAsync(
                serial,
                current.IdentityFingerprint,
                automationProfile,
                screenProfile,
                cancellationToken);
            actions.Add(new AutomationActionRecord
            {
                SequenceNumber = actions.Count + 1,
                Kind = AutomationActionKind.SwipeNextPokemon,
                StartedAtUtc = started,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StateBefore = current.Detection.State,
                StateAfter = wait.Probe?.Detection.State,
                Detail = $"attempt={attempt};{wait.Detail}"
            });

            if (wait.StopReason == AutomationStopReason.None)
            {
                return wait;
            }

            if (wait.StopReason != AutomationStopReason.StateTimeout)
            {
                return wait;
            }
        }

        return NavigationResult.Stop(
            AutomationStopReason.EndOfInventoryDetected,
            $"Identity did not change after {automationProfile.MaxSwipeAttemptsAtEnd} verified swipes.");
    }

    private async Task<NavigationResult> TapAndWaitAsync(
        string serial,
        NormalizedPoint point,
        ScreenState expectedState,
        AutomationActionKind actionKind,
        ScreenProbe before,
        int width,
        int height,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        List<AutomationActionRecord> actions,
        CancellationToken cancellationToken)
    {
        var pixel = point.ToPixels(width, height);
        var started = DateTimeOffset.UtcNow;
        await _transport.TapAsync(serial, pixel.X, pixel.Y, cancellationToken);
        await DelayAsync(automationProfile.PostActionSettleMilliseconds, cancellationToken);
        var wait = await WaitForStateAsync(
            serial,
            expectedState,
            automationProfile,
            screenProfile,
            cancellationToken);

        actions.Add(new AutomationActionRecord
        {
            SequenceNumber = actions.Count + 1,
            Kind = actionKind,
            StartedAtUtc = started,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            StateBefore = before.Detection.State,
            StateAfter = wait.Probe?.Detection.State,
            Detail = wait.Detail
        });

        return wait;
    }

    private async Task<NavigationResult> WaitForStateAsync(
        string serial,
        ScreenState expectedState,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(automationProfile.StateTimeoutSeconds);
        ScreenProbe? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await ProbeAsync(serial, automationProfile, screenProfile, cancellationToken);
            if (last.Detection.State == expectedState)
            {
                return NavigationResult.Success(last);
            }

            if (last.Detection.State is ScreenState.Popup or ScreenState.NetworkError or ScreenState.Unknown)
            {
                var stop = StopForState(last.Detection.State);
                return NavigationResult.Stop(stop.Reason, stop.Detail, last);
            }

            await DelayAsync(automationProfile.StatePollMilliseconds, cancellationToken);
        }

        return NavigationResult.Stop(
            AutomationStopReason.StateTimeout,
            $"Timed out waiting for {expectedState}; last state was {last?.Detection.State.ToString() ?? "none"}.",
            last);
    }

    private async Task<NavigationResult> WaitForChangedAppraisalAsync(
        string serial,
        byte[] previousFingerprint,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(automationProfile.StateTimeoutSeconds);
        ScreenProbe? last = null;
        var lastSimilarity = 1d;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await ProbeAsync(serial, automationProfile, screenProfile, cancellationToken);

            if (last.Detection.State is ScreenState.Popup or ScreenState.NetworkError or ScreenState.Unknown)
            {
                var stop = StopForState(last.Detection.State);
                return NavigationResult.Stop(stop.Reason, stop.Detail, last);
            }

            if (last.Detection.State == ScreenState.AppraisalOpen)
            {
                lastSimilarity = FingerprintComparer.Similarity(
                    previousFingerprint,
                    last.IdentityFingerprint);
                if (lastSimilarity < automationProfile.SamePokemonSimilarityThreshold)
                {
                    return NavigationResult.Success(
                        last,
                        $"identitySimilarity={lastSimilarity:F6}");
                }
            }

            await DelayAsync(automationProfile.StatePollMilliseconds, cancellationToken);
        }

        return NavigationResult.Stop(
            AutomationStopReason.StateTimeout,
            $"Appraisal remained on the same Pokémon. Last identity similarity={lastSimilarity:F6}.",
            last);
    }

    private async Task<ScreenProbe> ProbeAsync(
        string serial,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        CancellationToken cancellationToken)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var png = await _transport.CaptureScreenshotPngAsync(serial, cancellationToken);
        var image = PngDecoder.Decode(png);
        var detection = _detector.Detect(image, screenProfile, capturedAt);
        var identity = FingerprintExtractor.Extract(
            image,
            automationProfile.IdentityRegion,
            automationProfile.IdentityFingerprintMode,
            automationProfile.IdentityFingerprintWidth,
            automationProfile.IdentityFingerprintHeight);

        return new ScreenProbe
        {
            ScreenshotPng = png,
            Detection = detection,
            IdentityFingerprint = identity,
            CapturedAtUtc = capturedAt
        };
    }

    private async Task<InventoryScanItem> CaptureItemAsync(
        string serial,
        string captureDirectory,
        int sequenceNumber,
        ScreenProbe probe,
        CancellationToken cancellationToken)
    {
        var fileName = $"{sequenceNumber:D6}.png";
        var path = Path.Combine(captureDirectory, fileName);
        await AutomationAtomicFile.WriteBytesAsync(
            path,
            probe.ScreenshotPng,
            cancellationToken);

        var screenshotSha256 = Hash(probe.ScreenshotPng);
        CalcyObservation observation;
        try
        {
            observation = await _observationProvider.ObserveAsync(
                new CalcyObservationRequest
                {
                    SequenceNumber = sequenceNumber,
                    DeviceSerial = serial,
                    CapturedAtUtc = probe.CapturedAtUtc,
                    ScreenshotPng = probe.ScreenshotPng.ToArray(),
                    ScreenshotSha256 = screenshotSha256
                },
                cancellationToken);

            if (observation is null)
            {
                observation = CalcyObservation.Failed(
                    _observationProvider.Name,
                    "NullObservation",
                    "The observation provider returned null.");
            }
            else
            {
                observation = CalcyObservation.WithRawOutput(observation);
                observation.Validate();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            observation = CalcyObservation.Failed(
                _observationProvider.Name,
                exception.GetType().Name,
                exception.Message);
        }

        _log.Write(
            DeviceLogLevel.Information,
            "inventory.automation.observation",
            "Pokémon observation captured.",
            new Dictionary<string, string>
            {
                ["sequence"] = sequenceNumber.ToString(),
                ["provider"] = observation.ProviderName,
                ["status"] = observation.Status.ToString(),
                ["confidence"] = observation.Confidence.ToString("F4")
            });

        return new InventoryScanItem
        {
            SequenceNumber = sequenceNumber,
            CapturedAtUtc = probe.CapturedAtUtc,
            ScreenshotFileName = Path.Combine("captures", fileName).Replace('\\', '/'),
            ScreenshotSha256 = screenshotSha256,
            IdentityFingerprintBase64 = Convert.ToBase64String(probe.IdentityFingerprint),
            IdentityFingerprintSha256 = Hash(probe.IdentityFingerprint),
            ScreenStateConfidence = probe.Detection.Confidence,
            Observation = observation
        };
    }

    private static async Task<InventoryScanCheckpoint> SaveRunningAsync(
        string outputDirectory,
        InventoryScanCheckpoint original,
        IReadOnlyList<InventoryScanItem> items,
        IReadOnlyList<AutomationActionRecord> actions,
        CancellationToken cancellationToken)
    {
        var checkpoint = original with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = AutomationRunStatus.Running,
            StopReason = AutomationStopReason.None,
            StopDetail = null,
            Items = items.ToArray(),
            Actions = actions.ToArray()
        };
        await InventoryScanCheckpointRepository.SaveAsync(
            outputDirectory,
            checkpoint,
            cancellationToken);
        return checkpoint;
    }

    private static async Task<InventoryAutomationResult> FinishAsync(
        string outputDirectory,
        string captureDirectory,
        InventoryScanCheckpoint original,
        IReadOnlyList<InventoryScanItem> items,
        IReadOnlyList<AutomationActionRecord> actions,
        AutomationRunStatus status,
        AutomationStopReason stopReason,
        string? detail,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? completedAt = status == AutomationRunStatus.Completed
            ? DateTimeOffset.UtcNow
            : null;
        var checkpoint = original with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = completedAt,
            Status = status,
            StopReason = stopReason,
            StopDetail = detail,
            Items = items.ToArray(),
            Actions = actions.ToArray()
        };
        var checkpointPath = await InventoryScanCheckpointRepository.SaveAsync(
            outputDirectory,
            checkpoint,
            cancellationToken);
        return new InventoryAutomationResult
        {
            Checkpoint = checkpoint,
            CheckpointPath = checkpointPath,
            CaptureDirectory = captureDirectory
        };
    }

    private static void ValidateCheckpointContext(
        InventoryScanCheckpoint checkpoint,
        string serial,
        int width,
        int height,
        InventoryAutomationProfile profile,
        string automationProfileHash,
        string screenProfileHash)
    {
        if (!string.Equals(checkpoint.DeviceSerial, serial, StringComparison.Ordinal))
        {
            throw new AutomationException(
                AutomationErrorCode.DeviceMismatch,
                $"Checkpoint device '{checkpoint.DeviceSerial}' does not match '{serial}'.");
        }

        if (checkpoint.ScreenWidth != width || checkpoint.ScreenHeight != height)
        {
            throw new AutomationException(
                AutomationErrorCode.GeometryMismatch,
                $"Checkpoint geometry {checkpoint.ScreenWidth}x{checkpoint.ScreenHeight} " +
                $"does not match current geometry {width}x{height}.");
        }

        if (!string.Equals(
                checkpoint.AutomationProfileName,
                profile.Name,
                StringComparison.Ordinal))
        {
            throw new AutomationException(
                AutomationErrorCode.InvalidProfile,
                "Checkpoint was created with a different automation profile.");
        }

        if (!string.Equals(
                checkpoint.AutomationProfileSha256,
                automationProfileHash,
                StringComparison.Ordinal) ||
            !string.Equals(
                checkpoint.ScreenProfileSha256,
                screenProfileHash,
                StringComparison.Ordinal))
        {
            throw new AutomationException(
                AutomationErrorCode.InvalidProfile,
                "Checkpoint profile hashes do not match the current automation and screen profiles.");
        }
    }

    private static void ValidateBattery(
        AndroidDeviceMetadata metadata,
        InventoryAutomationProfile profile)
    {
        if (metadata.Battery.TemperatureCelsius is { } temperature &&
            temperature > profile.MaximumBatteryTemperatureCelsius)
        {
            throw new AutomationException(
                AutomationErrorCode.TransportFailure,
                $"Battery temperature {temperature:F1} C exceeds " +
                $"the configured limit {profile.MaximumBatteryTemperatureCelsius:F1} C.");
        }

        if (metadata.Battery.LevelPercent is { } level &&
            level < profile.MinimumBatteryPercent &&
            metadata.Battery.UsbPowered != true)
        {
            throw new AutomationException(
                AutomationErrorCode.TransportFailure,
                $"Battery level {level}% is below the configured minimum " +
                $"{profile.MinimumBatteryPercent}% and the device is not USB powered.");
        }
    }

    private static (AutomationStopReason Reason, string Detail) StopForState(ScreenState state) =>
        state switch
        {
            ScreenState.Unknown =>
                (AutomationStopReason.UnknownScreen, "Screen detector returned Unknown."),
            ScreenState.Popup =>
                (AutomationStopReason.PopupDetected, "A popup was detected; no input was sent."),
            ScreenState.NetworkError =>
                (AutomationStopReason.NetworkErrorDetected, "A network error was detected; no input was sent."),
            _ =>
                (AutomationStopReason.UnexpectedScreen, $"Unexpected screen state {state}.")
        };

    private static string Hash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static Task DelayAsync(int milliseconds, CancellationToken cancellationToken) =>
        milliseconds <= 0
            ? Task.CompletedTask
            : Task.Delay(milliseconds, cancellationToken);

    private sealed record NavigationResult
    {
        public ScreenProbe? Probe { get; init; }
        public AutomationStopReason StopReason { get; init; }
        public string? Detail { get; init; }

        public static NavigationResult Success(ScreenProbe probe, string? detail = null) =>
            new()
            {
                Probe = probe,
                StopReason = AutomationStopReason.None,
                Detail = detail
            };

        public static NavigationResult Stop(
            AutomationStopReason reason,
            string detail,
            ScreenProbe? probe = null) =>
            new()
            {
                Probe = probe,
                StopReason = reason,
                Detail = detail
            };
    }
}
