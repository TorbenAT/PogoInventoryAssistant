using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Appraisal.Services;
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
public sealed class AndroidVerifiedInventoryNamedOperations : ICleanupProofNamedOperations
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
    private readonly List<string> _lastCleanupAppraisalEvidence = new();
    private byte[]? _lastCleanupAppraisalScreenshot;
    public int LastCleanupRecoveryInputCount { get; private set; }

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

    public async Task<CleanupStateObservation> CaptureStableCleanupStateAsync(
        string label,
        CancellationToken cancellationToken)
    {
        var frames = await CaptureRecoveryFramesAsync(label, cancellationToken);
        for (var index = 0; index < frames.Count; index++)
        {
            await SaveEvidenceAsync(
                $"{label}-recovery-{index + 1}",
                frames[index].Screenshot,
                cancellationToken);
        }
        if (!GuardedInventoryRecovery.TryGetStableFrame(frames, out var stable) || stable is null)
        {
            if (!TryGetThreeStateStableFrame(frames, out stable) || stable is null)
            {
                return new CleanupStateObservation
                {
                    State = PokemonGoGameState.Unknown,
                    AppraisalKind = null,
                    ScreenshotSha256 = string.Empty,
                    Evidence = new[] { "NoThreeStableFrames" }
                };
            }
        }

        var state = stable.Detection.State;
        if (state == PokemonGoGameState.Inventory)
        {
            var search = _searchAnalyzer.Analyze(stable.Screenshot);
            state = search.SearchFieldVisible && search.KeyboardVisible
                ? PokemonGoGameState.InventorySearchOpen
                : search.SearchFieldVisible && search.QueryVisible
                    ? PokemonGoGameState.InventoryFiltered
                    : PokemonGoGameState.Inventory;
        }

        return new CleanupStateObservation
        {
            State = state,
            AppraisalKind = stable.Kind is RecoveryFrameKind.AppraisalIntro or RecoveryFrameKind.AppraisalBars
                ? stable.Kind
                : null,
            ScreenshotSha256 = stable.Detection.ScreenshotSha256,
            Evidence = stable.Detection.Evidence
        };
    }

    private static bool TryGetThreeStateStableFrame(
        IReadOnlyList<RecoveryFrame> frames,
        out RecoveryFrame? stable)
    {
        stable = null;
        var window = frames.TakeLast(3).ToArray();
        if (window.Length != 3 || window.Any(frame =>
                frame.Detection.State == PokemonGoGameState.Unknown ||
                frame.Kind is RecoveryFrameKind.Unknown or RecoveryFrameKind.Conflicting ||
                frame.HasConflictingAnchor))
        {
            return false;
        }

        if (window.Any(frame => frame.Detection.State != window[0].Detection.State ||
                frame.Kind != window[0].Kind))
        {
            return false;
        }

        if (window[0].Kind is RecoveryFrameKind.AppraisalIntro or RecoveryFrameKind.AppraisalBars)
        {
            return false;
        }

        stable = window[^1];
        return true;
    }

    public async Task<CanonicalCloseOperationResult> CloseCanonicalScreenAsync(
        CancellationToken cancellationToken)
    {
        var precondition = await CaptureCanonicalClosePreconditionAsync(cancellationToken);
        if (precondition is null)
        {
            return new CanonicalCloseOperationResult
            {
                Succeeded = false,
                StateBefore = PokemonGoGameState.Unknown,
                StateAfter = PokemonGoGameState.Unknown,
                InputCount = 0,
                UnsafeSurfacePresent = false,
                CanonicalCloseVerified = false,
                Result = "CANONICAL_CLOSE_NOT_FOUND",
                Blocker = "No compatible canonical close target was visually verified."
            };
        }

        var fresh = await CaptureAsync("canonical-close-fresh-pre-input", cancellationToken);
        var freshDetection = _detector.Detect(fresh, _appraisalProfile);
        var freshState = EffectiveState(fresh, freshDetection);
        var freshClose = _locator.LocateCanonicalCloseControl(fresh);
        var freshUnsafe = _unsafeSurfaceDetector.Detect(fresh, "canonical-close-screen");
        var freshTargetValid = freshClose is not null &&
            Distance(freshClose.Target, precondition.Target) <= 0.035;
        if (!freshTargetValid ||
            (precondition.StateBefore != PokemonGoGameState.Unknown &&
             freshState != precondition.StateBefore))
        {
            await WriteAuditAsync("close-canonical-screen", new
            {
                StateBefore = precondition.StateBefore,
                LocatorEvidence = precondition.Locator.Evidence,
                Target = precondition.Target,
                PreconditionHashes = precondition.Hashes,
                FreshFrameHash = freshDetection.ScreenshotSha256,
                InputCount = 0,
                StateAfter = freshState,
                PostconditionResult = "DENIED_FRESH_CANONICAL_CLOSE_CHANGED",
                UnsafeSurfacePresent = freshUnsafe.IsUnsafe,
                CanonicalCloseVerified = false
            }, cancellationToken);
            return new CanonicalCloseOperationResult
            {
                Succeeded = false,
                StateBefore = precondition.StateBefore,
                StateAfter = freshState,
                InputCount = 0,
                UnsafeSurfacePresent = freshUnsafe.IsUnsafe,
                CanonicalCloseVerified = false,
                Result = "CANONICAL_CLOSE_NOT_FOUND",
                Blocker = "Fresh canonical close target or state changed before input."
            };
        }

        var visualFallback = freshDetection.State == PokemonGoGameState.Unknown &&
            freshState != PokemonGoGameState.Unknown ? freshState : (PokemonGoGameState?)null;
        if (_navigationTrace is not null)
        {
            await _navigationTrace.AuthorizeAsync(
                "close-canonical-screen",
                "changed-known-state-or-gameplay-map",
                freshState.ToString(),
                "AUTHORIZED",
                fresh,
                freshDetection,
                visualFallback,
                freshUnsafe.IsUnsafe ? freshUnsafe.Kind : null,
                cancellationToken);
        }

        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var (x, y) = freshClose!.Target.ToPixels(width, height);
        await _transport.TapAsync(_serial, x, y, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync("Tap", $"({x},{y})", cancellationToken);
        await WriteAuditAsync("close-canonical-screen", new
        {
            StateBefore = precondition.StateBefore,
            LocatorEvidence = freshClose.Evidence,
            Target = freshClose.Target,
            Bounds = freshClose.Bounds,
            PreconditionHashes = precondition.Hashes,
            FreshFrameHash = freshDetection.ScreenshotSha256,
            InputCount = 1,
            UnsafeSurfacePresent = freshUnsafe.IsUnsafe,
            CanonicalCloseVerified = true,
            OnlyCloseInputAuthorized = true,
            ConfirmationCancelled = freshUnsafe.IsUnsafe,
            InputSent = true
        }, cancellationToken);

        var post = await CaptureStableChangedStateAsync(
            "canonical-close-post", precondition.StateBefore, cancellationToken);
        await CompleteTraceAsync(post.State.ToString(), post.Succeeded ? "PASS" : post.Result,
            cancellationToken);
        await WriteAuditAsync("close-canonical-screen-postcondition", new
        {
            StateBefore = precondition.StateBefore,
            StateAfter = post.State,
            InputCount = 1,
            PostconditionResult = post.Result,
            PostFrameCount = post.FrameCount
        }, cancellationToken);
        return new CanonicalCloseOperationResult
        {
            Succeeded = post.Succeeded,
            StateBefore = precondition.StateBefore,
            StateAfter = post.State,
            InputCount = 1,
            UnsafeSurfacePresent = freshUnsafe.IsUnsafe,
            CanonicalCloseVerified = true,
            Result = post.Result,
            Blocker = post.Succeeded ? null : "Canonical close postcondition did not establish a changed stable state."
        };
    }

    private async Task<CanonicalClosePrecondition?> CaptureCanonicalClosePreconditionAsync(
        CancellationToken cancellationToken)
    {
        var frames = new List<CanonicalCloseFrame>(5);
        for (var index = 0; index < 5; index++)
        {
            var screenshot = await CaptureAsync($"canonical-close-pre-{index + 1}", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var state = EffectiveState(screenshot, detection);
            var locator = _locator.LocateCanonicalCloseControl(screenshot);
            var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, "canonical-close-precondition");
            await SaveEvidenceAsync($"canonical-close-pre-{index + 1}", screenshot, cancellationToken);
            if (locator is not null)
            {
                frames.Add(new CanonicalCloseFrame(state, locator, detection.ScreenshotSha256));
            }

            var last = frames.TakeLast(3).ToArray();
            if (last.Length == 3 &&
                last.All(frame => frame.State == last[0].State) &&
                last.All(frame => Distance(frame.Locator.Target, last[0].Locator.Target) <= 0.025))
            {
                return new CanonicalClosePrecondition
                {
                    StateBefore = last[^1].State,
                    Locator = last[^1].Locator,
                    Hashes = last.Select(frame => frame.Hash).ToArray()
                };
            }
            if (index < 4)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        return null;
    }

    private async Task<StableChangedState> CaptureStableChangedStateAsync(
        string label,
        PokemonGoGameState before,
        CancellationToken cancellationToken)
    {
        var states = new List<PokemonGoGameState>(5);
        for (var index = 0; index < 5; index++)
        {
            var screenshot = await CaptureAsync($"{label}-{index + 1}", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            states.Add(EffectiveState(screenshot, detection));
            await SaveEvidenceAsync($"{label}-{index + 1}", screenshot, cancellationToken);
            var last = states.TakeLast(3).ToArray();
            if (last.Length == 3 && last.All(state => state == last[0]) &&
                last[0] != PokemonGoGameState.Unknown && last[0] != before)
            {
                return new StableChangedState(true, last[0], "CHANGED_STABLE_STATE", 3);
            }
            if (index < 4)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        return new StableChangedState(false, states.LastOrDefault(),
            "NO_CHANGED_STABLE_STATE", states.Count);
    }

    private PokemonGoGameState EffectiveState(
        byte[] screenshot,
        PokemonGoGameStateDetection detection) =>
        detection.State == PokemonGoGameState.Unknown &&
        _locator.LocateDetailsPageTopology(screenshot) is not null
            ? PokemonGoGameState.PokemonDetails
            : detection.State;

    private sealed record CanonicalCloseFrame(
        PokemonGoGameState State,
        LocatedCanonicalCloseControl Locator,
        string Hash);

    private sealed record CanonicalClosePrecondition
    {
        public required PokemonGoGameState StateBefore { get; init; }
        public required LocatedCanonicalCloseControl Locator { get; init; }
        public required IReadOnlyList<string> Hashes { get; init; }
        public NormalizedPoint Target => Locator.Target;
    }

    private sealed record StableChangedState(
        bool Succeeded,
        PokemonGoGameState State,
        string Result,
        int FrameCount);

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

    public async Task<CleanupProofIdentityCapture> CaptureCleanupIdentityAsync(
        int maximumFrames,
        int minimumCompleteFrames,
        int minimumPartialFrames,
        CancellationToken cancellationToken)
    {
        if (maximumFrames < 1 || maximumFrames > 8)
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));
        if (minimumCompleteFrames < 3 || minimumCompleteFrames > maximumFrames)
            throw new ArgumentOutOfRangeException(nameof(minimumCompleteFrames));
        if (minimumPartialFrames < 2 || minimumPartialFrames > minimumCompleteFrames)
            throw new ArgumentOutOfRangeException(nameof(minimumPartialFrames));

        var frames = new List<PokemonIdentityFrame>(maximumFrames);
        var paths = new List<string>(maximumFrames);
        var failureReasons = new List<string>();
        for (var index = 0; index < maximumFrames; index++)
        {
            var screenshot = await CaptureAsync("cleanup-identity", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var topology = _locator.LocateDetailsPageTopology(screenshot);
            var details = detection.State == PokemonGoGameState.PokemonDetails || topology is not null;
            var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, "cleanup-proof-observation");
            if (unsafeSurface.IsUnsafe)
            {
                failureReasons.Add($"UnsafeConfirmation:{unsafeSurface.Kind}");
                break;
            }
            if (!details)
            {
                failureReasons.Add($"ExpectedPokemonDetails:{detection.State}");
            }
            else
            {
                var path = await SaveEvidenceAsync("cleanup-identity", screenshot, cancellationToken);
                paths.Add(path);
                frames.Add(new PokemonIdentityFrame { ScreenshotPng = screenshot });
            }

            if (frames.Count >= minimumCompleteFrames)
            {
                var complete = _identityAnalyzer.Consensus(frames);
                if (complete.Status == PokemonIdentityObservationStatus.Complete)
                    break;
            }
            if (index < maximumFrames - 1)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }

        if (frames.Count == 0)
        {
            return new CleanupProofIdentityCapture
            {
                Consensus = new PokemonIdentityConsensus
                {
                    Status = PokemonIdentityObservationStatus.Unavailable,
                    StableFingerprintSha256 = string.Empty,
                    StableFingerprintBase64 = string.Empty,
                    Confidence = 0,
                    Frames = Array.Empty<PokemonIdentityFingerprintObservation>(),
                    EvidenceHashes = Array.Empty<string>(),
                    Tags = new PokemonIdentityTagObservation
                    {
                        TagCount = 0,
                        Section = null,
                        IsSeparateFromIdentity = true
                    },
                    IgnoredFrameCount = 0
                },
                Status = CleanupProofObservationStatus.Unresolved,
                ScreenshotPaths = paths,
                ScreenshotHashes = Array.Empty<string>(),
                FailureReasons = failureReasons
            };
        }

        var consensus = _identityAnalyzer.Consensus(frames);
        var status = consensus.Status == PokemonIdentityObservationStatus.Complete
            ? CleanupProofObservationStatus.Complete
            : consensus.Status == PokemonIdentityObservationStatus.Partial &&
              consensus.EvidenceHashes.Count >= minimumPartialFrames
                ? CleanupProofObservationStatus.Partial
                : CleanupProofObservationStatus.Unresolved;
        if (status == CleanupProofObservationStatus.Unresolved)
            failureReasons.Add("FewerThanTwoCompatibleDetailsFrames");
        _lastIdentity = consensus;
        return new CleanupProofIdentityCapture
        {
            Consensus = consensus,
            Status = status,
            ScreenshotPaths = paths,
            ScreenshotHashes = consensus.EvidenceHashes,
            FailureReasons = failureReasons
        };
    }

    public async Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken)
    {
        _lastCleanupAppraisalEvidence.Clear();
        _lastCleanupAppraisalScreenshot = null;
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
                _lastCleanupAppraisalScreenshot = stable.Screenshot;
                _lastCleanupAppraisalEvidence.Add(
                    await SaveEvidenceAsync("appraisal-bars", stable.Screenshot, cancellationToken));
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

    public async Task<CleanupProofAppraisalCapture> CaptureCleanupAppraisalAsync(
        CancellationToken cancellationToken)
    {
        var status = await CaptureAppraisalAsync(cancellationToken);
        if (status != "AppraisalBarsObserved")
        {
            return new CleanupProofAppraisalCapture
            {
                Status = "Unavailable",
                FailureReasons = new[] { status },
                EvidencePaths = _lastCleanupAppraisalEvidence.ToArray()
            };
        }

        if (_lastCleanupAppraisalScreenshot is null || _appraisalProfile is null)
        {
            return new CleanupProofAppraisalCapture
            {
                Status = "Partial",
                FailureReasons = new[] { "VerifiedAppraisalProfileUnavailable" },
                EvidencePaths = _lastCleanupAppraisalEvidence.ToArray()
            };
        }

        var analysis = new AppraisalAnalyzer().Analyze(
            PngDecoder.Decode(_lastCleanupAppraisalScreenshot),
            _appraisalProfile,
            allowComplete: true);
        return new CleanupProofAppraisalCapture
        {
            Status = analysis.Status == AppraisalAnalysisStatus.Complete &&
                analysis.AttackIv is not null && analysis.DefenseIv is not null && analysis.HpIv is not null
                ? "Complete"
                : "Partial",
            AttackIv = analysis.AttackIv,
            DefenseIv = analysis.DefenseIv,
            HpIv = analysis.HpIv,
            Confidence = analysis.Confidence,
            EvidencePaths = _lastCleanupAppraisalEvidence.ToArray(),
            FailureReasons = analysis.Status == AppraisalAnalysisStatus.Complete
                ? Array.Empty<string>()
                : new[] { analysis.Detail },
            Frames = AnalyzeEvidenceFrames(_lastCleanupAppraisalEvidence, _appraisalProfile)
        };
    }

    /// <summary>
    /// Analyzes every saved appraisal evidence screenshot independently (not
    /// just the single "stable" frame) so a caller can require multi-frame
    /// agreement on the IV triple before trusting it as Complete. Uses the
    /// same bar-confidence definition <c>AppraisalAnalyzer</c> uses internally
    /// to gate Complete, without touching that gate itself.
    /// </summary>
    private static IReadOnlyList<AppraisalFrameIv> AnalyzeEvidenceFrames(
        IReadOnlyList<string> evidencePaths,
        AppraisalVisualProfile profile)
    {
        var frames = new List<AppraisalFrameIv>();
        foreach (var path in evidencePaths)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                continue;
            }

            AppraisalAnalysisResult analysis;
            try
            {
                analysis = new AppraisalAnalyzer().Analyze(PngDecoder.Decode(bytes), profile, allowComplete: false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ScreenVisionException)
            {
                continue;
            }

            var barsConfident = analysis.Bars.Count == 3 && analysis.Bars.All(bar =>
                bar.TrackDetected &&
                bar.EstimatedIv is not null &&
                bar.Confidence >= profile.CompleteBarConfidenceMinimum);
            frames.Add(new AppraisalFrameIv
            {
                AttackIv = analysis.AttackIv,
                DefenseIv = analysis.DefenseIv,
                HpIv = analysis.HpIv,
                BarsConfident = barsConfident
            });
        }
        return frames;
    }

    public async Task<CleanupProofIdentityCapture> CaptureCleanupAppraisalIdentityAsync(
        CancellationToken cancellationToken)
    {
        var frames = await CaptureRecoveryFramesAsync("carousel-appraisal-identity", cancellationToken);
        if (!GuardedInventoryRecovery.TryGetStableFrame(frames, out var stable) ||
            stable is null || stable.Kind != RecoveryFrameKind.AppraisalBars)
        {
            return new CleanupProofIdentityCapture
            {
                Consensus = UnavailableIdentity("AppraisalBarsNotStable"),
                Status = CleanupProofObservationStatus.Unresolved,
                ScreenshotPaths = Array.Empty<string>(),
                ScreenshotHashes = Array.Empty<string>(),
                FailureReasons = new[] { "AppraisalBarsNotStable" }
            };
        }

        var paths = new List<string>();
        foreach (var frame in frames.Where(frame => frame.Kind == RecoveryFrameKind.AppraisalBars).Take(3))
            paths.Add(await SaveEvidenceAsync("carousel-appraisal-identity", frame.Screenshot, cancellationToken));
        var fingerprint = Convert.ToHexString(SHA256.HashData(stable.Screenshot)).ToLowerInvariant();
        var consensus = AppraisalIdentity(fingerprint, paths);
        _lastIdentity = consensus;
        return new CleanupProofIdentityCapture
        {
            Consensus = consensus,
            Status = CleanupProofObservationStatus.Complete,
            ScreenshotPaths = paths,
            ScreenshotHashes = paths.Select(path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()).ToArray()
        };
    }

    public async Task<CleanupProofAppraisalCapture> CaptureCurrentCleanupAppraisalAsync(
        CancellationToken cancellationToken)
    {
        var frames = await CaptureRecoveryFramesAsync("carousel-appraisal-observation", cancellationToken);
        if (!GuardedInventoryRecovery.TryGetStableFrame(frames, out var stable) ||
            stable is null || stable.Kind != RecoveryFrameKind.AppraisalBars)
        {
            return new CleanupProofAppraisalCapture
            {
                Status = "Unavailable",
                FailureReasons = new[] { "AppraisalBarsNotStable" }
            };
        }
        _lastCleanupAppraisalEvidence.Clear();
        _lastCleanupAppraisalScreenshot = stable.Screenshot;
        foreach (var frame in frames.Where(frame => frame.Kind == RecoveryFrameKind.AppraisalBars).Take(3))
            _lastCleanupAppraisalEvidence.Add(await SaveEvidenceAsync("carousel-appraisal-bars", frame.Screenshot, cancellationToken));
        return AnalyzeLastCleanupAppraisal();
    }

    public async Task<AppraisalCarouselAdvanceResult> AdvanceToNextPokemonInAppraisalAsync(
        string previousAppraisalFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previousAppraisalFingerprint);
        var before = await CaptureRecoveryFramesAsync("carousel-appraisal-pre-swipe", cancellationToken);
        if (!GuardedInventoryRecovery.TryGetStableFrame(before, out var stableBefore) ||
            stableBefore is null || stableBefore.Kind != RecoveryFrameKind.AppraisalBars)
            return AppraisalCarouselAdvanceResult.UNKNOWN_STOP;

        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var start = _automationProfile.NextPokemonSwipe.Start.ToPixels(width, height);
        var end = _automationProfile.NextPokemonSwipe.End.ToPixels(width, height);
        await AuthorizeNonTapInputAsync(
            "appraisal-next-pokemon-swipe", cancellationToken, PokemonGoGameState.Appraisal.ToString());
        await _transport.SwipeAsync(_serial, start.X, start.Y, end.X, end.Y,
            _automationProfile.NextPokemonSwipe.DurationMilliseconds, cancellationToken);
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync(
                "Swipe", $"({start.X},{start.Y})->({end.X},{end.Y})", cancellationToken);

        var post = await CaptureRecoveryFramesAsync("carousel-appraisal-post-swipe", cancellationToken);
        if (!GuardedInventoryRecovery.TryGetStableFrame(post, out var stablePost) ||
            stablePost is null || stablePost.Kind != RecoveryFrameKind.AppraisalBars)
        {
            // One bounded observation window is allowed for a transient Unknown;
            // it never sends another swipe.
            post = await CaptureRecoveryFramesAsync("carousel-appraisal-recovery", cancellationToken);
            if (!GuardedInventoryRecovery.TryGetStableFrame(post, out stablePost) ||
                stablePost is null || stablePost.Kind != RecoveryFrameKind.AppraisalBars)
                return AppraisalCarouselAdvanceResult.UNKNOWN_STOP;
            return AppraisalCarouselAdvanceResult.TRANSIENT_UNKNOWN_RECOVERED;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(stablePost.Screenshot)).ToLowerInvariant();
        var result = string.Equals(previousAppraisalFingerprint, fingerprint, StringComparison.Ordinal)
            ? AppraisalCarouselAdvanceResult.NO_EFFECT_OR_FILTER_END
            : AppraisalCarouselAdvanceResult.SUCCESS_CHANGED_POKEMON;
        await WriteAuditAsync("appraisal-carousel-advance", new
        {
            AuthorizedAction = "NextPokemonSwipe",
            RequiredState = PokemonGoGameState.Appraisal,
            InputCount = 1,
            PreFingerprint = previousAppraisalFingerprint,
            PostFingerprint = fingerprint,
            PostFrameCount = post.Count,
            Result = result.ToString()
        }, cancellationToken);
        return result;
    }

    private CleanupProofAppraisalCapture AnalyzeLastCleanupAppraisal()
    {
        if (_lastCleanupAppraisalScreenshot is null || _appraisalProfile is null)
            return new CleanupProofAppraisalCapture { Status = "Partial", EvidencePaths = _lastCleanupAppraisalEvidence.ToArray() };
        var analysis = new AppraisalAnalyzer().Analyze(
            PngDecoder.Decode(_lastCleanupAppraisalScreenshot), _appraisalProfile, allowComplete: true);
        return new CleanupProofAppraisalCapture
        {
            Status = analysis.Status == AppraisalAnalysisStatus.Complete &&
                analysis.AttackIv is not null && analysis.DefenseIv is not null && analysis.HpIv is not null
                ? "Complete" : "Partial",
            AttackIv = analysis.AttackIv, DefenseIv = analysis.DefenseIv, HpIv = analysis.HpIv,
            Confidence = analysis.Confidence,
            EvidencePaths = _lastCleanupAppraisalEvidence.ToArray(),
            FailureReasons = analysis.Status == AppraisalAnalysisStatus.Complete ? Array.Empty<string>() : new[] { analysis.Detail },
            Frames = AnalyzeEvidenceFrames(_lastCleanupAppraisalEvidence, _appraisalProfile)
        };
    }

    private static PokemonIdentityConsensus AppraisalIdentity(string fingerprint, IReadOnlyList<string> paths) => new()
    {
        Status = PokemonIdentityObservationStatus.Complete,
        StableFingerprintSha256 = fingerprint,
        StableFingerprintBase64 = fingerprint,
        Confidence = 0.80,
        Frames = Array.Empty<PokemonIdentityFingerprintObservation>(),
        EvidenceHashes = paths.Select(path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()).ToArray(),
        Tags = new PokemonIdentityTagObservation { TagCount = 0, Section = null, IsSeparateFromIdentity = true },
        IgnoredFrameCount = 0
    };

    private static PokemonIdentityConsensus UnavailableIdentity(string reason) => new()
    {
        Status = PokemonIdentityObservationStatus.Unavailable,
        StableFingerprintSha256 = string.Empty, StableFingerprintBase64 = string.Empty,
        Confidence = 0, Frames = Array.Empty<PokemonIdentityFingerprintObservation>(),
        EvidenceHashes = new[] { reason },
        Tags = new PokemonIdentityTagObservation { TagCount = 0, Section = null, IsSeparateFromIdentity = true },
        IgnoredFrameCount = 0
    };

    public async Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken)
    {
        LastCleanupRecoveryInputCount = 0;
        var frames = await CaptureRecoveryFramesAsync("exit-appraisal", cancellationToken);
        _recovery.Begin(frames);
        for (var attempt = 0; attempt < GuardedInventoryRecovery.MaxAppraisalTotalActions; attempt++)
        {
            var authorization = _recovery.AuthorizeNextAction();
            if (authorization is null) return VerifiedSequenceState.Unknown;
            if (authorization.Action != RecoveryInputAction.ExitAppraisal || authorization.Target is null)
                return VerifiedSequenceState.Unknown;
            await ExecuteRecoveryActionAsync(authorization, cancellationToken);
            LastCleanupRecoveryInputCount++;
            await WriteRecoveryAuditAsync(authorization, "PokemonDetails", cancellationToken);
            frames = await CaptureRecoveryFramesAsync("exit-appraisal-post", cancellationToken);
            var outcome = _recovery.ObservePostAction(frames);
            if (outcome is RecoveryOutcome.UNKNOWN_STOP or RecoveryOutcome.UNEXPECTED_STOP or
                RecoveryOutcome.ACTION_NOT_OBSERVED or RecoveryOutcome.STABILITY_TIMEOUT)
            {
                // This fallback is intentionally scoped to the one authorized
                // appraisal-exit tap. It accepts only three compatible visual
                // Details frames with positive topology and no appraisal,
                // menu, main-menu or unsafe-surface evidence. No second input
                // is sent here.
                var recovered = await CapturePostExitDetailsFramesAsync(
                    "post-exit-details-fallback", cancellationToken);
                if (recovered.Count == 3)
                    return VerifiedSequenceState.PokemonDetails;
                return VerifiedSequenceState.Unknown;
            }
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
        LastCleanupRecoveryInputCount = 0;
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
            LastCleanupRecoveryInputCount++;
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

    public async Task<string> CloseInventoryAsync(CancellationToken cancellationToken)
    {
        LastCleanupRecoveryInputCount = 0;
        var before = await WaitForStateAsync(
            new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var detection = _detector.Detect(before.Screenshot, _appraisalProfile);
        if (!GuardedInventoryClose.CanAct(detection))
            return PokemonGoGameState.Unknown.ToString();

        await AuthorizeNonTapInputAsync(
            "close-inventory", cancellationToken, PokemonGoGameState.GameplayMap.ToString(),
            PokemonGoGameState.Inventory);
        await _transport.PressBackAsync(_serial, cancellationToken);
        LastCleanupRecoveryInputCount = 1;
        if (_navigationTrace is not null)
            await _navigationTrace.RecordInputSentAsync("PressBack", "Back", cancellationToken);
        var after = await WaitForStateAsync(
            new[] { PokemonGoGameState.GameplayMap }, cancellationToken);
        await CompleteTraceAsync(
            PokemonGoGameState.GameplayMap.ToString(),
            after.State == PokemonGoGameState.GameplayMap ? "PASS" : "FAIL",
            cancellationToken);
        return after.State.ToString();
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

    private async Task<IReadOnlyList<PokemonIdentityFrame>> CapturePostExitDetailsFramesAsync(
        string label,
        CancellationToken cancellationToken)
    {
        var frames = new List<PokemonIdentityFrame>(3);
        for (var index = 0; index < 3; index++)
        {
            var screenshot = await CaptureAsync($"{label}-{index + 1}", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            var topology = _locator.LocateDetailsPageTopology(screenshot);
            var unsafeSurface = _unsafeSurfaceDetector.Detect(screenshot, label);
            var recoveryFrame = _recovery.Observe(screenshot, _appraisalProfile);
            var hasConflict = detection.State is PokemonGoGameState.MainMenu or
                PokemonGoGameState.PokemonMenu or PokemonGoGameState.Appraisal ||
                recoveryFrame.Kind is RecoveryFrameKind.AppraisalIntro or
                RecoveryFrameKind.AppraisalBars or RecoveryFrameKind.Conflicting;
            if (topology is null || unsafeSurface.IsUnsafe || hasConflict)
                return Array.Empty<PokemonIdentityFrame>();
            frames.Add(new PokemonIdentityFrame { ScreenshotPng = screenshot });
            await SaveEvidenceAsync($"{label}-{index + 1}", screenshot, cancellationToken);
            if (index < 2)
                await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }

        return _identityAnalyzer.Consensus(frames).Status ==
            PokemonIdentityObservationStatus.Complete
            ? frames
            : Array.Empty<PokemonIdentityFrame>();
    }

    private async Task ExecuteRecoveryActionAsync(
        RecoveryActionAuthorization authorization,
        CancellationToken cancellationToken)
    {
        if (authorization.Action == RecoveryInputAction.ExitAppraisal)
        {
            if (authorization.Target is null)
                throw new InvalidOperationException("Guarded appraisal action had no visual target.");
            var fresh = await CaptureAsync("pre-guarded-exit-appraisal", cancellationToken);
            var detection = _detector.Detect(fresh, _appraisalProfile);
            if (detection.State != PokemonGoGameState.Appraisal)
                throw new InvalidOperationException("Guarded appraisal action lost the Appraisal state before input.");
            await TapNamedAsync(
                authorization.Target,
                "guarded-exit-appraisal",
                cancellationToken,
                new NamedInputAuthorization
                {
                    RequiredState = PokemonGoGameState.Appraisal,
                    PreconditionScreenshotSha256 = detection.ScreenshotSha256,
                    FreshPreTapScreenshotSha256 = detection.ScreenshotSha256,
                    FreshScreenshot = fresh
                });
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
        string? expectedPostcondition = null,
        PokemonGoGameState? requiredState = null)
    {
        var screenshot = await CaptureAsync($"pre-{name}", cancellationToken);
        await EnsureNoUnsafeConfirmationAsync(name, screenshot, cancellationToken);
        var detection = _detector.Detect(screenshot, _appraisalProfile);
        var visualFallbackState = detection.State == PokemonGoGameState.Unknown &&
            _locator.LocateDetailsPageTopology(screenshot) is not null
                ? PokemonGoGameState.PokemonDetails
                : (PokemonGoGameState?)null;
        if (requiredState is { } required && detection.State != required &&
            visualFallbackState != required)
        {
            await WriteAuditAsync($"{name}-authorization", new
            {
                Action = name,
                RequiredState = required,
                StrictDetectedState = detection.State,
                VisualFallbackState = visualFallbackState,
                AuthorizationResult = "DENIED_REQUIRED_STATE_CHANGED",
                InputSent = false
            }, cancellationToken);
            throw new InvalidOperationException($"Input '{name}' was not authorized from {required}.");
        }
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

    private async Task<string> SaveEvidenceAsync(string label, byte[] screenshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_evidenceDirectory);
        var safe = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(_evidenceDirectory, $"{++_evidenceOrdinal:D4}-{safe}.png");
        await File.WriteAllBytesAsync(path, screenshot, cancellationToken);
        return path;
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
