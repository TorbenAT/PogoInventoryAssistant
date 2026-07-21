using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Errors;

namespace PogoInventory.Exploration.Services;

public enum CursorProgressionOutcome
{
    Success,
    SuccessChangedIdentity,
    NoEffectOrEndOfFilter
}

/// <summary>
/// Real-device adapter for the verified sequence. All input is expressed as a
/// named operation and every input has a bounded, visual postcondition check.
/// The adapter never builds or executes an ADB command.
/// </summary>
public sealed class AndroidVerifiedInventoryNamedOperations : IVerifiedInventoryNamedOperations
{
    private readonly IAndroidAutomationTransport _transport;
    private readonly string _serial;
    private readonly InventoryAutomationProfile _automationProfile;
    private readonly AppraisalVisualProfile? _appraisalProfile;
    private readonly string _evidenceDirectory;
    private readonly PokemonGoGameStateDetector _detector = new();
    private readonly VisualControlLocator _locator = new();
    private readonly UnsafeConfirmationSurfaceDetector _unsafeSurfaceDetector = new();
    private readonly InventorySearchVisualAnalyzer _searchAnalyzer = new();
    private readonly PokemonDetailsIdentityAnalyzer _identityAnalyzer;
    private readonly GuardedInventoryRecovery _recovery = new();
    private readonly NavigationSafetyTraceRecorder? _navigationTrace;
    private AndroidDeviceMetadata? _metadata;
    private PokemonIdentityConsensus? _lastIdentity;
    private byte[]? _lastScreenshot;
    private int _evidenceOrdinal;

    public AndroidVerifiedInventoryNamedOperations(
        IAndroidAutomationTransport transport,
        string serial,
        InventoryAutomationProfile automationProfile,
        string evidenceDirectory,
        AppraisalVisualProfile? appraisalProfile = null,
        PokemonIdentityFingerprintProfile? identityProfile = null,
        NavigationSafetyTraceRecorder? navigationTrace = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        _serial = serial;
        _automationProfile = automationProfile ?? throw new ArgumentNullException(nameof(automationProfile));
        _automationProfile.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        _evidenceDirectory = Path.GetFullPath(evidenceDirectory);
        _evidenceOrdinal = NextEvidenceOrdinal(_evidenceDirectory);
        _appraisalProfile = appraisalProfile;
        _identityAnalyzer = new PokemonDetailsIdentityAnalyzer(identityProfile);
        _navigationTrace = navigationTrace;
    }

    public async Task<VerifiedSequenceState> OpenInventoryAsync(CancellationToken cancellationToken)
    {
        await EnsureMetadataAsync(cancellationToken);
        var state = await WaitForStateAsync(
            new[] { PokemonGoGameState.Inventory, PokemonGoGameState.GameplayMap,
                PokemonGoGameState.MainMenu, PokemonGoGameState.PokemonDetails,
                PokemonGoGameState.PokemonMenu, PokemonGoGameState.Appraisal },
            cancellationToken,
            allowVisualDetailsFallback: true);
        if (state.State is PokemonGoGameState.PokemonDetails or PokemonGoGameState.PokemonMenu or
            PokemonGoGameState.Appraisal)
        {
            var recovered = await ReturnToInventoryAsync(cancellationToken);
            if (recovered != VerifiedSequenceState.Inventory)
                return VerifiedSequenceState.Unknown;
            state = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        }
        if (state.State == PokemonGoGameState.GameplayMap)
        {
            var menu = _locator.LocateMainMenuPokeball(state.Screenshot);
            if (menu is null) return VerifiedSequenceState.Unknown;
            await TapAndVerifyAsync(menu.Target, PokemonGoGameState.MainMenu, "open-main-menu", cancellationToken);
            state = await WaitForStateAsync(new[] { PokemonGoGameState.MainMenu }, cancellationToken);
        }
        if (state.State == PokemonGoGameState.MainMenu)
        {
            var precondition = await CaptureVerifiedMainMenuPreconditionAsync(cancellationToken);
            if (precondition is null) return VerifiedSequenceState.Unknown;
            var fresh = await RevalidateMainMenuPreconditionAsync(precondition, cancellationToken);
            if (fresh is null) return VerifiedSequenceState.Unknown;
            await TapAndVerifyAsync(
                fresh.Value.Target,
                PokemonGoGameState.Inventory,
                "open-inventory",
                cancellationToken,
                fresh.Value.Authorization);
        }

        var current = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        return current.State == PokemonGoGameState.Inventory
            ? VerifiedSequenceState.Inventory
            : VerifiedSequenceState.Unknown;
    }

    public async Task<VerifiedSequenceState> EnsureFilteredInventoryAsync(
        string query, CancellationToken cancellationToken)
    {
        var opened = await OpenInventoryAsync(cancellationToken);
        if (opened != VerifiedSequenceState.Inventory)
            return VerifiedSequenceState.Unknown;

        var current = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var workflow = new GuardedInventorySearch();
        var evidence = _searchAnalyzer.Analyze(current.Screenshot);
        var begin = workflow.Begin(evidence, query);
        if (begin == InventorySearchOutcome.UnsafePreState) return VerifiedSequenceState.Unknown;
        for (var actionCount = 0; actionCount < 4; actionCount++)
        {
            var authorization = workflow.AuthorizeNextAction();
            if (authorization is null) break;
            switch (authorization.Action)
            {
                case InventorySearchAction.OpenSearch:
                    await TapNamedAsync(new NormalizedPoint { X = 0.5005, Y = 0.1881 },
                        "open-search", cancellationToken);
                    break;
                case InventorySearchAction.ClearSearch:
                    await TapNamedAsync(new NormalizedPoint { X = 0.9175, Y = 0.1881 }, "clear-search", cancellationToken);
                    break;
                case InventorySearchAction.EnterQuery:
                    await AuthorizeNonTapInputAsync(
                        "enter-inventory-search-query", cancellationToken, "InventorySearch");
                    await _transport.EnterInventorySearchQueryAsync(_serial, query, cancellationToken);
                    if (_navigationTrace is not null)
                        await _navigationTrace.RecordInputSentAsync(
                            "EnterInventorySearchQuery", "query supplied", cancellationToken);
                    break;
                case InventorySearchAction.SubmitQuery:
                    await AuthorizeNonTapInputAsync(
                        "submit-inventory-search-query", cancellationToken, "InventorySearch");
                    await _transport.SubmitInventorySearchQueryAsync(_serial, cancellationToken);
                    if (_navigationTrace is not null)
                        await _navigationTrace.RecordInputSentAsync(
                            "SubmitInventorySearchQuery", "submit", cancellationToken);
                    break;
            }
            var after = await CaptureAsync($"search-{authorization.Sequence}", cancellationToken);
            var outcome = workflow.ObservePostAction(_searchAnalyzer.Analyze(after));
            await CompleteTraceAsync(
                PokemonGoGameState.Inventory.ToString(),
                outcome.ToString(),
                cancellationToken);
            if (outcome is InventorySearchOutcome.ActionNotObserved or InventorySearchOutcome.UnexpectedState)
                return VerifiedSequenceState.Unknown;
            if (outcome == InventorySearchOutcome.Succeeded) break;
        }
        var filtered = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        return filtered.State == PokemonGoGameState.Inventory
            ? VerifiedSequenceState.Inventory
            : VerifiedSequenceState.Unknown;
    }

    public async Task<VerifiedSequenceState> OpenFirstPokemonAsync(CancellationToken cancellationToken)
    {
        var inventory = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var located = _locator.LocateInventoryCard(inventory.Screenshot);
        if (located is null) return VerifiedSequenceState.Unknown;
        await TapNamedAsync(
            located.Target,
            "open-first-pokemon",
            cancellationToken,
            expectedPostcondition: PokemonGoGameState.PokemonDetails);
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        await CompleteTraceAsync(
            PokemonGoGameState.PokemonDetails.ToString(),
            details.State == PokemonGoGameState.PokemonDetails ? "PASS" : "FAIL",
            cancellationToken);
        return details.State == PokemonGoGameState.PokemonDetails
            ? VerifiedSequenceState.PokemonDetails
            : VerifiedSequenceState.Unknown;
    }

    public async Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken)
    {
        var frames = new List<PokemonIdentityFrame>(3);
        for (var index = 0; index < 3; index++)
        {
            var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
            frames.Add(new PokemonIdentityFrame { ScreenshotPng = details.Screenshot });
            await SaveEvidenceAsync($"identity-{index + 1}", details.Screenshot, cancellationToken);
            if (index < 2) await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        _lastIdentity = _identityAnalyzer.Consensus(frames);
        return _lastIdentity;
    }

    public async Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken)
    {
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var menu = _locator.LocateDetailsMenu(details.Screenshot);
        if (menu is null) return "Partial";
        await TapNamedAsync(menu.Target, "open-details-menu", cancellationToken);
        var menuState = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonMenu }, cancellationToken);
        var appraise = _locator.LocateAppraiseMenuItem(menuState.Screenshot);
        if (appraise is null) return "Partial";
        await TapNamedAsync(appraise.Target, "open-appraisal", cancellationToken);
        var introTapActions = 0;
        var tapWasSent = false;
        for (var attempt = 0; attempt < GuardedInventoryRecovery.MaxAppraisalTotalActions + 1; attempt++)
        {
            var frames = await CaptureRecoveryFramesAsync("appraisal", cancellationToken);
            var decision = GuardedInventoryRecovery.DecideAppraisalContinuation(
                frames, introTapActions, tapWasSent);
            if (decision is AppraisalContinuationOutcome.SUCCESS_ALREADY_ADVANCED or
                AppraisalContinuationOutcome.SUCCESS_TAPPED)
            {
                var stable = frames.Last(frame => frame.Kind == RecoveryFrameKind.AppraisalBars);
                await SaveEvidenceAsync("appraisal-bars", stable.Screenshot, cancellationToken);
                return "AppraisalBarsObserved";
            }
            if (decision != AppraisalContinuationOutcome.TAP_INTRO_ONCE)
                return "Partial";

            _recovery.Begin(frames);
            var authorization = _recovery.AuthorizeNextAction();
            if (authorization is null || authorization.Action != RecoveryInputAction.ExitAppraisal ||
                authorization.Target is null)
                return "Partial";
            await ExecuteRecoveryActionAsync(authorization, cancellationToken);
            introTapActions++;
            tapWasSent = true;
            await WriteRecoveryAuditAsync(authorization, "Appraisal", cancellationToken);
        }
        return "Partial";
    }

    public async Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken)
    {
        var frames = await CaptureRecoveryFramesAsync("exit-appraisal", cancellationToken);
        _recovery.Begin(frames);
        for (var attempt = 0; attempt < GuardedInventoryRecovery.MaxAppraisalTotalActions; attempt++)
        {
            var authorization = _recovery.AuthorizeNextAction();
            if (authorization is null) return VerifiedSequenceState.Unknown;
            if (authorization.Action != RecoveryInputAction.ExitAppraisal || authorization.Target is null)
                return VerifiedSequenceState.Unknown;
            await ExecuteRecoveryActionAsync(authorization, cancellationToken);
            await WriteRecoveryAuditAsync(authorization, "PokemonDetails", cancellationToken);
            frames = await CaptureRecoveryFramesAsync("exit-appraisal-post", cancellationToken);
            var outcome = _recovery.ObservePostAction(frames);
            if (outcome is RecoveryOutcome.UNKNOWN_STOP or RecoveryOutcome.UNEXPECTED_STOP or
                RecoveryOutcome.ACTION_NOT_OBSERVED or RecoveryOutcome.STABILITY_TIMEOUT)
                return VerifiedSequenceState.Unknown;
            if (_recovery.Current?.Detection.State == PokemonGoGameState.PokemonDetails)
            {
                var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
                return details.State == PokemonGoGameState.PokemonDetails
                    ? VerifiedSequenceState.PokemonDetails
                    : VerifiedSequenceState.Unknown;
            }
        }
        return VerifiedSequenceState.Unknown;
    }

    public async Task<VerifiedTagObservation> ReadTagObservationAsync(CancellationToken cancellationToken)
    {
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var identity = _identityAnalyzer.Analyze(details.Screenshot);
        return new VerifiedTagObservation
        {
            TagCount = identity.Tags.TagCount,
            KnownTagNames = identity.Tags.TagNames,
            NamesComplete = identity.Tags.TagNames.Count >= identity.Tags.TagCount,
            Section = identity.Tags.Section,
            Evidence = new[] { identity.EvidenceSha256, "details-tag-pills" }
        };
    }

    public async Task<VerifiedSequenceState> AdvanceToNextPokemonAsync(
        PokemonIdentityConsensus previous, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var before = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var beforeDetection = _detector.Detect(before.Screenshot, _appraisalProfile);
        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var start = _automationProfile.NextPokemonSwipe.Start.ToPixels(width, height);
        var end = _automationProfile.NextPokemonSwipe.End.ToPixels(width, height);
        await AuthorizeNonTapInputAsync(
            "next-pokemon-swipe", cancellationToken, PokemonGoGameState.PokemonDetails.ToString());
        await _transport.SwipeAsync(_serial, start.X, start.Y, end.X, end.Y,
            _automationProfile.NextPokemonSwipe.DurationMilliseconds, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync(
                "Swipe", $"({start.X},{start.Y})->({end.X},{end.Y})", cancellationToken);
        var transition = await ObserveSwipeTransitionAsync(before.Screenshot, beforeDetection, cancellationToken);
        var postFrames = await CaptureIndependentDetailsFramesAsync(
            "post-swipe-identity",
            allowVisualDetailsFallback: !transition.Observed,
            cancellationToken: cancellationToken);
        if (postFrames.Count != 3)
            return VerifiedSequenceState.Unknown;
        var postIdentity = _identityAnalyzer.Consensus(postFrames);
        var outcome = ClassifySwipeProgression(
            transition.Observed,
            previous.StableFingerprintSha256,
            postIdentity.StableFingerprintSha256);
        await CompleteTraceAsync(
            PokemonGoGameState.PokemonDetails.ToString(),
            outcome.ToString(),
            cancellationToken);
        if (outcome == CursorProgressionOutcome.NoEffectOrEndOfFilter)
        {
            await WriteAuditAsync("advance-no-effect", new
            {
                StateBefore = beforeDetection.State,
                AuthorizedAction = "NextPokemonSwipe",
                LocatorTarget = _automationProfile.NextPokemonSwipe,
                InputCount = 1,
                ExpectedState = PokemonGoGameState.PokemonDetails,
                ObservedState = transition.State,
                Before = previous.StableFingerprintSha256,
                TransitionEvidence = transition.Evidence,
                PostFrameCount = postFrames.Count,
                PostFingerprint = postIdentity.StableFingerprintSha256,
                Result = "NO_EFFECT_OR_END_OF_FILTER"
            }, cancellationToken);
            return VerifiedSequenceState.NoEffectOrEndOfFilter;
        }
        await WriteAuditAsync("advance", new
        {
            StateBefore = beforeDetection.State,
            AppraisalSubstate = (string?)null,
            AuthorizedAction = "NextPokemonSwipe",
            LocatorTarget = _automationProfile.NextPokemonSwipe,
            InputCount = 1,
            ExpectedState = PokemonGoGameState.PokemonDetails,
            ObservedState = transition.State,
            Before = Convert.ToHexString(SHA256.HashData(before.Screenshot)).ToLowerInvariant(),
            TransitionEvidence = transition.Evidence,
            PostFrameCount = postFrames.Count,
            PostFingerprint = postIdentity.StableFingerprintSha256,
            Result = outcome == CursorProgressionOutcome.SuccessChangedIdentity
                ? "SUCCESS_CHANGED_IDENTITY"
                : "SUCCESS"
        }, cancellationToken);
        _lastIdentity = postIdentity;
        _lastScreenshot = postFrames[^1].ScreenshotPng;
        return VerifiedSequenceState.PokemonDetails;
    }

    public static CursorProgressionOutcome ClassifySwipeProgression(
        bool transientTransitionObserved,
        string beforeFingerprint,
        string postFingerprint)
    {
        if (transientTransitionObserved)
            return CursorProgressionOutcome.Success;
        if (!string.IsNullOrWhiteSpace(beforeFingerprint) &&
            !string.IsNullOrWhiteSpace(postFingerprint) &&
            !string.Equals(beforeFingerprint, postFingerprint, StringComparison.Ordinal))
            return CursorProgressionOutcome.SuccessChangedIdentity;
        return CursorProgressionOutcome.NoEffectOrEndOfFilter;
    }

    public async Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken)
    {
        var frames = await CaptureRecoveryFramesAsync("return-to-inventory", cancellationToken);
        var begin = _recovery.Begin(frames);
        if (begin == RecoveryOutcome.SUCCEEDED) return VerifiedSequenceState.Inventory;
        if (begin is RecoveryOutcome.UNKNOWN_STOP or RecoveryOutcome.UNEXPECTED_STOP or RecoveryOutcome.STABILITY_TIMEOUT)
            return VerifiedSequenceState.Unknown;
        for (var actionCount = 0; actionCount < GuardedInventoryRecovery.MaxAppraisalTotalActions; actionCount++)
        {
            var authorization = _recovery.AuthorizeNextAction();
            if (authorization is null) return VerifiedSequenceState.Unknown;
            await ExecuteRecoveryActionAsync(authorization, cancellationToken);
            await WriteRecoveryAuditAsync(authorization, "Inventory", cancellationToken);
            frames = await CaptureRecoveryFramesAsync("return-to-inventory-post", cancellationToken);
            var outcome = _recovery.ObservePostAction(frames);
            await CompleteTraceAsync(
                _recovery.Current?.Detection.State.ToString() ?? "Unknown",
                outcome.ToString(),
                cancellationToken);
            if (outcome is RecoveryOutcome.UNKNOWN_STOP or RecoveryOutcome.UNEXPECTED_STOP or
                RecoveryOutcome.ACTION_NOT_OBSERVED or RecoveryOutcome.STABILITY_TIMEOUT)
                return VerifiedSequenceState.Unknown;
            if (outcome == RecoveryOutcome.SUCCEEDED) return VerifiedSequenceState.Inventory;
        }
        return VerifiedSequenceState.Unknown;
    }

    public async Task<PokemonGoGameState> CloseInventoryAsync(CancellationToken cancellationToken)
    {
        var before = await WaitForStateAsync(
            new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var detection = _detector.Detect(before.Screenshot, _appraisalProfile);
        if (!GuardedInventoryClose.CanAct(detection))
            return PokemonGoGameState.Unknown;

        await AuthorizeNonTapInputAsync(
            "close-inventory", cancellationToken, PokemonGoGameState.GameplayMap.ToString());
        await _transport.PressBackAsync(_serial, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync("PressBack", "Back", cancellationToken);
        var after = await WaitForStateAsync(
            new[] { PokemonGoGameState.GameplayMap }, cancellationToken);
        await CompleteTraceAsync(
            PokemonGoGameState.GameplayMap.ToString(),
            after.State == PokemonGoGameState.GameplayMap ? "PASS" : "FAIL",
            cancellationToken);
        return after.State;
    }

    public Task<IReadOnlyList<string>> ApplyIndexTagAsync(string tagName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Tag mutation is disabled for the read-only first acceptance.");

    public Task<IReadOnlyList<string>> ApplyClassificationTagAsync(string tagName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Tag mutation is disabled for the read-only first acceptance.");

    private async Task<IReadOnlyList<RecoveryFrame>> CaptureRecoveryFramesAsync(
        string label, CancellationToken cancellationToken)
    {
        var frames = new List<RecoveryFrame>(GuardedInventoryRecovery.ConsensusWindow);
        for (var index = 0; index < GuardedInventoryRecovery.ConsensusWindow; index++)
        {
            var screenshot = await CaptureAsync($"{label}-{index + 1}", cancellationToken);
            var frame = _recovery.Observe(screenshot, _appraisalProfile);
            frames.Add(frame);
            if (GuardedInventoryRecovery.TryGetStableFrame(frames, out _))
                return frames;
            await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        return frames;
    }

    private async Task<IReadOnlyList<PokemonIdentityFrame>> CaptureIndependentDetailsFramesAsync(
        string label,
        bool allowVisualDetailsFallback,
        CancellationToken cancellationToken)
    {
        var frames = new List<PokemonIdentityFrame>(3);
        for (var index = 0; index < 3; index++)
        {
            var screenshot = allowVisualDetailsFallback
                ? await CaptureAsync($"{label}-{index + 1}", cancellationToken)
                : (await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken)).Screenshot;
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var detailsTopology = _locator.LocateDetailsPageTopology(screenshot);
            var isDetails = detection.State == PokemonGoGameState.PokemonDetails ||
                (allowVisualDetailsFallback && detailsTopology is not null);
            if (!isDetails)
                return Array.Empty<PokemonIdentityFrame>();
            frames.Add(new PokemonIdentityFrame { ScreenshotPng = screenshot });
            await SaveEvidenceAsync($"{label}-{index + 1}", screenshot, cancellationToken);
            if (index < 2)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        return frames;
    }

    private async Task ExecuteRecoveryActionAsync(
        RecoveryActionAuthorization authorization,
        CancellationToken cancellationToken)
    {
        if (authorization.Action == RecoveryInputAction.ExitAppraisal)
        {
            if (authorization.Target is null)
                throw new InvalidOperationException("Guarded appraisal action had no visual target.");
            await TapNamedAsync(authorization.Target, "guarded-exit-appraisal", cancellationToken);
            return;
        }

        if (authorization.Action != RecoveryInputAction.PressBack ||
            authorization.StateBefore is not (PokemonGoGameState.PokemonDetails or PokemonGoGameState.PokemonMenu))
            throw new InvalidOperationException("Android Back was not authorized for the observed state.");
        await AuthorizeNonTapInputAsync(
            "guarded-back", cancellationToken, PokemonGoGameState.Inventory.ToString());
        await _transport.PressBackAsync(_serial, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync("PressBack", "Back", cancellationToken);
    }

    private Task WriteRecoveryAuditAsync(
        RecoveryActionAuthorization authorization,
        string expectedState,
        CancellationToken cancellationToken) => WriteAuditAsync("recovery-action", new
        {
            StateBefore = authorization.StateBefore,
            AppraisalSubstate = authorization.ExpectedFrameKind,
            AuthorizedAction = authorization.Action,
            LocatorTarget = authorization.Target,
            InputCount = authorization.Sequence,
            ExpectedState = expectedState,
            ObservedState = "pending-postcondition",
            Result = authorization.Detail
        }, cancellationToken);

    private async Task<(bool Observed, PokemonGoGameState State, IReadOnlyList<string> Evidence)>
        ObserveSwipeTransitionAsync(
            byte[] beforeScreenshot,
            PokemonGoGameStateDetection beforeDetection,
            CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_automationProfile.StateTimeoutSeconds);
        var beforeTopology = _locator.LocateDetailsPageTopology(beforeScreenshot);
        var evidence = new List<string>();
        var observedState = PokemonGoGameState.Unknown;
        while (DateTime.UtcNow < deadline)
        {
            var screenshot = await CaptureAsync("swipe-transition", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            observedState = detection.State;
            var topology = _locator.LocateDetailsPageTopology(screenshot);
            if (detection.State != beforeDetection.State)
                evidence.Add("game-state-changed");
            if (!string.Equals(
                    GuardedInventoryRecovery.EvidenceSignature(detection),
                    GuardedInventoryRecovery.EvidenceSignature(beforeDetection),
                    StringComparison.Ordinal))
                evidence.Add("state-evidence-changed");
            if ((beforeTopology is null) != (topology is null))
                evidence.Add("details-anchor-transition");
            if (TransitionRoiChanged(beforeScreenshot, screenshot))
                evidence.Add("carousel-transition-roi-changed");
            if (evidence.Count > 0)
                return (true, observedState, evidence.Distinct(StringComparer.Ordinal).ToArray());
            await Task.Delay(_automationProfile.StatePollMilliseconds, cancellationToken);
        }
        return (false, observedState, new[] { "no-observed-transition" });
    }

    private static bool TransitionRoiChanged(byte[] beforePng, byte[] afterPng)
    {
        try
        {
            var before = PngDecoder.Decode(beforePng);
            var after = PngDecoder.Decode(afterPng);
            if (before.Width != after.Width || before.Height != after.Height)
                return true;
            var difference = 0d;
            var samples = 0;
            for (var row = 0; row < 12; row++)
            for (var column = 0; column < 16; column++)
            {
                var x = (int)(before.Width * (0.08 + column * 0.84 / 15));
                var y = (int)(before.Height * (0.12 + row * 0.60 / 11));
                var left = before.GetPixel(x, y);
                var right = after.GetPixel(x, y);
                difference += Math.Abs(left.R - right.R) / 255d;
                difference += Math.Abs(left.G - right.G) / 255d;
                difference += Math.Abs(left.B - right.B) / 255d;
                samples += 3;
            }
            return samples > 0 && difference / samples >= 0.035;
        }
        catch (Exception exception) when (exception is ScreenVisionException or ArgumentException)
        {
            return false;
        }
    }

    private async Task<(PokemonGoGameState State, byte[] Screenshot)> WaitForStateAsync(
        IReadOnlyCollection<PokemonGoGameState> expected,
        CancellationToken cancellationToken,
        bool allowVisualDetailsFallback = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_automationProfile.StateTimeoutSeconds);
        PokemonGoGameState? last = null;
        var consecutive = 0;
        byte[]? screenshot = null;
        while (DateTime.UtcNow < deadline)
        {
            screenshot = await CaptureAsync("state", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var observedState = detection.State;
            if (allowVisualDetailsFallback && observedState == PokemonGoGameState.Unknown &&
                _locator.LocateDetailsPageTopology(screenshot) is not null)
            {
                observedState = PokemonGoGameState.PokemonDetails;
            }
            if (expected.Contains(observedState))
            {
                consecutive = last == observedState ? consecutive + 1 : 1;
                last = observedState;
                if (consecutive >= 3) return (observedState, screenshot);
            }
            else
            {
                last = detection.State;
                consecutive = 0;
                if (detection.State == PokemonGoGameState.Unknown)
                    await WriteAuditAsync("unknown", detection, cancellationToken);
            }
            await Task.Delay(_automationProfile.StatePollMilliseconds, cancellationToken);
        }
        return (last ?? PokemonGoGameState.Unknown, screenshot ?? Array.Empty<byte>());
    }

    private async Task TapAndVerifyAsync(NormalizedPoint point, PokemonGoGameState expected,
        string name, CancellationToken cancellationToken,
        NamedInputAuthorization? authorization = null)
    {
        await TapNamedAsync(point, name, cancellationToken, authorization, expected);
        var result = await WaitForStateAsync(new[] { expected }, cancellationToken);
        await CompleteTraceAsync(
            expected.ToString(),
            result.State == expected ? "PASS" : "FAIL",
            cancellationToken);
        if (result.State != expected) throw new InvalidOperationException($"{name} postcondition was not verified.");
    }

    private async Task TapNamedAsync(
        NormalizedPoint point,
        string name,
        CancellationToken cancellationToken,
        NamedInputAuthorization? authorization = null,
        PokemonGoGameState? expectedPostcondition = null)
    {
        var screenshot = authorization?.FreshScreenshot ??
            await CaptureAsync($"pre-{name}", cancellationToken);
        await EnsureNoUnsafeConfirmationAsync(name, screenshot, cancellationToken);
        var detection = _detector.Detect(screenshot, _appraisalProfile);
        var visualFallbackState = detection.State == PokemonGoGameState.Unknown &&
            _locator.LocateDetailsPageTopology(screenshot) is not null
            ? PokemonGoGameState.PokemonDetails
            : (PokemonGoGameState?)null;
        var traceExpectedState = expectedPostcondition?.ToString() ?? "validated-named-operation";
        if (authorization?.RequiredState is { } required && detection.State != required)
        {
            if (_navigationTrace is not null)
                await _navigationTrace.AuthorizeAsync(
                    name,
                    traceExpectedState,
                    required.ToString(),
                    "DENIED_REQUIRED_STATE_CHANGED",
                    screenshot,
                    detection,
                    visualFallbackState,
                    null,
                    cancellationToken);
            await WriteAuditAsync($"{name}-authorization", new
            {
                Action = "named-tap",
                RequiredState = required,
                StrictDetectedState = detection.State,
                VisualFallbackState = visualFallbackState,
                ConflictingStates = new[] { visualFallbackState ?? PokemonGoGameState.Unknown },
                Target = point,
                PreconditionScreenshotHash = authorization.PreconditionScreenshotSha256,
                FreshPreTapScreenshotHash = authorization.FreshPreTapScreenshotSha256,
                AuthorizationResult = "DENIED_REQUIRED_STATE_CHANGED",
                InputSent = false
            }, cancellationToken);
            return;
        }
        if (_navigationTrace is not null)
            await _navigationTrace.AuthorizeAsync(
                name,
                traceExpectedState,
                authorization?.RequiredState.ToString() ?? "validated-named-operation",
                "AUTHORIZED",
                screenshot,
                detection,
                visualFallbackState,
                null,
                cancellationToken);
        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var (x, y) = point.ToPixels(width, height);
        await _transport.TapAsync(_serial, x, y, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync("Tap", $"({x},{y})", cancellationToken);
        await WriteAuditAsync(name, new
        {
            Action = "named-tap",
            X = x,
            Y = y,
            RequiredState = authorization?.RequiredState.ToString() ?? "validated-named-operation",
            StrictDetectedState = detection.State,
            VisualFallbackState = visualFallbackState,
            ConflictingStates = Array.Empty<PokemonGoGameState>(),
            Target = point,
            PreconditionScreenshotHash = authorization?.PreconditionScreenshotSha256 ?? detection.ScreenshotSha256,
            FreshPreTapScreenshotHash = authorization?.FreshPreTapScreenshotSha256 ?? detection.ScreenshotSha256,
            AuthorizationResult = "AUTHORIZED",
            InputSent = true
        }, cancellationToken);
    }

    private async Task<byte[]> AuthorizeNonTapInputAsync(
        string name,
        CancellationToken cancellationToken,
        string? expectedPostcondition = null)
    {
        var screenshot = await CaptureAsync($"pre-{name}", cancellationToken);
        await EnsureNoUnsafeConfirmationAsync(name, screenshot, cancellationToken);
        var detection = _detector.Detect(screenshot, _appraisalProfile);
        var visualFallbackState = detection.State == PokemonGoGameState.Unknown &&
            _locator.LocateDetailsPageTopology(screenshot) is not null
                ? PokemonGoGameState.PokemonDetails
                : (PokemonGoGameState?)null;
        if (_navigationTrace is not null)
            await _navigationTrace.AuthorizeAsync(
                name,
                expectedPostcondition ?? "validated-named-operation",
                detection.State.ToString(),
                "AUTHORIZED",
                screenshot,
                detection,
                visualFallbackState,
                null,
                cancellationToken);
        await WriteAuditAsync($"{name}-authorization", new
        {
            Action = name,
            RequiredState = "validated-named-operation",
            StrictDetectedState = detection.State,
            VisualFallbackState = visualFallbackState,
            ConflictingStates = Array.Empty<PokemonGoGameState>(),
            Target = (object?)null,
            PreconditionScreenshotHash = detection.ScreenshotSha256,
            FreshPreTapScreenshotHash = detection.ScreenshotSha256,
            AuthorizationResult = "AUTHORIZED",
            InputSent = true
        }, cancellationToken);
        return screenshot;
    }

    private async Task EnsureNoUnsafeConfirmationAsync(
        string action, byte[] screenshot, CancellationToken cancellationToken)
    {
        var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, action);
        if (!unsafeSurface.IsUnsafe) return;
        if (_navigationTrace is not null)
        {
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var visualFallbackState = detection.State == PokemonGoGameState.Unknown &&
                _locator.LocateDetailsPageTopology(screenshot) is not null
                    ? PokemonGoGameState.PokemonDetails
                    : (PokemonGoGameState?)null;
            await _navigationTrace.RecordDeniedAsync(
                action,
                "unsafe-confirmation",
                "DENIED_UNSAFE_CONFIRMATION",
                screenshot,
                detection,
                visualFallbackState,
                unsafeSurface.Kind,
                cancellationToken);
        }
        await SaveEvidenceAsync($"UnsafeConfirmation-{unsafeSurface.Kind}", screenshot, cancellationToken);
        await WriteAuditAsync("UnsafeConfirmation", new
        {
            Action = action,
            UnsafeConfirmation = unsafeSurface.Kind.ToString(),
            ScreenshotSha256 = unsafeSurface.ScreenshotSha256,
            Evidence = unsafeSurface.Evidence,
            AuthorizationResult = "DENIED_UNSAFE_CONFIRMATION",
            InputSent = false,
            AutoCancel = false
        }, cancellationToken);
        throw new UnsafeConfirmationSurfaceException(action, unsafeSurface.Kind);
    }

    private async Task<VerifiedMainMenuPrecondition?> CaptureVerifiedMainMenuPreconditionAsync(
        CancellationToken cancellationToken)
    {
        var frames = new List<MainMenuFrameObservation>(MainMenuPreconditionValidator.RequiredStableFrames);
        for (var index = 0; index < MainMenuPreconditionValidator.RequiredStableFrames; index++)
        {
            var screenshot = await CaptureAsync($"main-menu-precondition-{index + 1}", cancellationToken);
            var observation = ObserveMainMenuFrame(screenshot);
            frames.Add(observation);
            if (observation.HasUnsafeConfirmation)
                await SaveEvidenceAsync("UnsafeConfirmation-MainMenuPrecondition", screenshot, cancellationToken);
            if (index < MainMenuPreconditionValidator.RequiredStableFrames - 1)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        var precondition = MainMenuPreconditionValidator.TryCreate(frames);
        await WriteAuditAsync("open-inventory-precondition", new
        {
            Action = "open-inventory",
            RequiredState = PokemonGoGameState.MainMenu,
            StrictDetectedState = frames[^1].StrictDetectedState,
            VisualFallbackState = frames[^1].VisualFallbackState,
            ConflictingStates = frames.SelectMany(frame => frame.ConflictingStates).Distinct().ToArray(),
            Target = frames[^1].InventoryTarget,
            PreconditionScreenshotHash = frames[^1].ScreenshotSha256,
            FreshPreTapScreenshotHash = (string?)null,
            AuthorizationResult = precondition is null ? "DENIED_PRECONDITION" : "PRECONDITION_VERIFIED",
            InputSent = false,
            StableFrameCount = frames.Count
        }, cancellationToken);
        return precondition;
    }

    private async Task<(NormalizedPoint Target, NamedInputAuthorization Authorization)? >
        RevalidateMainMenuPreconditionAsync(
            VerifiedMainMenuPrecondition precondition,
            CancellationToken cancellationToken)
    {
        var screenshot = await CaptureAsync("open-inventory-fresh-pre-tap", cancellationToken);
        var observation = ObserveMainMenuFrame(screenshot);
        var valid = MainMenuPreconditionValidator.IsSafeMainMenuFrame(observation) &&
            observation.InventoryTarget is not null &&
            Distance(observation.InventoryTarget, precondition.Target) <= 0.025;
        if (!valid)
        {
            await WriteAuditAsync("open-inventory-fresh-precondition", new
            {
                Action = "open-inventory",
                RequiredState = precondition.RequiredState,
                StrictDetectedState = observation.StrictDetectedState,
                VisualFallbackState = observation.VisualFallbackState,
                ConflictingStates = observation.ConflictingStates,
                Target = observation.InventoryTarget,
                PreconditionScreenshotHash = precondition.PreconditionScreenshotSha256,
                FreshPreTapScreenshotHash = observation.ScreenshotSha256,
                AuthorizationResult = "DENIED_FRESH_PRECONDITION_CHANGED",
                InputSent = false
            }, cancellationToken);
            return null;
        }
        return (observation.InventoryTarget!, new NamedInputAuthorization
        {
            RequiredState = PokemonGoGameState.MainMenu,
            PreconditionScreenshotSha256 = precondition.PreconditionScreenshotSha256,
            FreshPreTapScreenshotSha256 = observation.ScreenshotSha256,
            FreshScreenshot = screenshot
        });
    }

    private MainMenuFrameObservation ObserveMainMenuFrame(byte[] screenshot)
    {
        var detection = _detector.Detect(screenshot, _appraisalProfile);
        var details = _locator.LocateDetailsPageTopology(screenshot);
        var menu = _locator.LocateAppraiseMenuItem(screenshot);
        var inventory = _locator.LocatePokemonInventory(screenshot);
        var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, "open-inventory");
        var visualFallback = detection.State == PokemonGoGameState.Unknown && details is not null
            ? PokemonGoGameState.PokemonDetails
            : (PokemonGoGameState?)null;
        var conflicts = new List<PokemonGoGameState>();
        if (details is not null) conflicts.Add(PokemonGoGameState.PokemonDetails);
        if (menu is not null || detection.State == PokemonGoGameState.PokemonMenu)
            conflicts.Add(PokemonGoGameState.PokemonMenu);
        if (detection.State == PokemonGoGameState.Appraisal)
            conflicts.Add(PokemonGoGameState.Appraisal);
        return new MainMenuFrameObservation
        {
            StrictDetectedState = detection.State,
            VisualFallbackState = visualFallback,
            HasMainMenuTopology = inventory is not null,
            HasInventoryLocator = inventory is not null,
            HasPokemonDetailsTopology = details is not null,
            HasPokemonMenu = menu is not null || detection.State == PokemonGoGameState.PokemonMenu,
            HasAppraisal = detection.State == PokemonGoGameState.Appraisal,
            HasUnsafeConfirmation = unsafeSurface.IsUnsafe,
            ConflictingStates = conflicts.Distinct().ToArray(),
            InventoryTarget = inventory?.Target,
            ScreenshotSha256 = detection.ScreenshotSha256
        };
    }

    private static double Distance(NormalizedPoint left, NormalizedPoint right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private sealed record NamedInputAuthorization
    {
        public required PokemonGoGameState RequiredState { get; init; }
        public required string PreconditionScreenshotSha256 { get; init; }
        public required string FreshPreTapScreenshotSha256 { get; init; }
        public required byte[] FreshScreenshot { get; init; }
    }

    private sealed class UnsafeConfirmationSurfaceException : InvalidOperationException
    {
        public UnsafeConfirmationSurfaceException(string action, UnsafeConfirmationKind kind)
            : base($"Input '{action}' denied by unsafe {kind} confirmation surface.") { }
    }

    private async Task<byte[]> CaptureAsync(string label, CancellationToken cancellationToken)
    {
        var screenshot = await _transport.CaptureScreenshotPngAsync(_serial, cancellationToken);
        _lastScreenshot = screenshot;
        if (_navigationTrace is not null)
        {
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var details = _locator.LocateDetailsPageTopology(screenshot);
            var fallback = detection.State == PokemonGoGameState.Unknown && details is not null
                ? PokemonGoGameState.PokemonDetails
                : (PokemonGoGameState?)null;
            var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, label);
            await _navigationTrace.ObserveFrameAsync(
                screenshot,
                detection,
                fallback,
                unsafeSurface.IsUnsafe ? unsafeSurface.Kind : null,
                cancellationToken);
        }
        return screenshot;
    }

    private async Task CompleteTraceAsync(
        string actualState,
        string result,
        CancellationToken cancellationToken)
    {
        if (_navigationTrace is null)
            return;
        _navigationTrace.RecordPostcondition(actualState, result);
        await _navigationTrace.CompletePostFramesAsync(
            _transport, _serial, cancellationToken);
    }

    private async Task<AndroidDeviceMetadata> EnsureMetadataAsync(CancellationToken cancellationToken) =>
        _metadata ??= await _transport.ReadMetadataAsync(_serial, cancellationToken);

    private async Task<(int Width, int Height)> ScreenSizeAsync(CancellationToken cancellationToken)
    {
        var metadata = await EnsureMetadataAsync(cancellationToken);
        var width = metadata.Screen.EffectiveWidth ?? throw new InvalidOperationException("Android width unavailable.");
        var height = metadata.Screen.EffectiveHeight ?? throw new InvalidOperationException("Android height unavailable.");
        return (width, height);
    }

    private async Task SaveEvidenceAsync(string label, byte[] screenshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_evidenceDirectory);
        var safe = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(_evidenceDirectory, $"{++_evidenceOrdinal:D4}-{safe}.png");
        await File.WriteAllBytesAsync(path, screenshot, cancellationToken);
    }

    private static int NextEvidenceOrdinal(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        var maximum = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                     .Concat(Directory.EnumerateFiles(directory, "*.png")))
        {
            var name = Path.GetFileName(path);
            if (name.Length >= 4 && int.TryParse(name[..4], out var ordinal))
                maximum = Math.Max(maximum, ordinal);
        }
        return maximum;
    }

    private async Task WriteAuditAsync(string label, object value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_evidenceDirectory);
        var safe = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(_evidenceDirectory, $"{++_evidenceOrdinal:D4}-{safe}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
}
