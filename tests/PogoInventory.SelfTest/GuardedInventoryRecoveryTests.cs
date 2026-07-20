using PogoInventory.Appraisal.Models;
using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Models;

internal static class GuardedInventoryRecoveryTests
{
    public static Task RunRoiConsensusAsync()
    {
        AnimatedFullScreenWithStableIntroRoiPasses();
        AnimatedFullScreenWithStableBarsRoiPasses();
        RoiAndBarThresholdsRejectDrift();
        ThreeOfFiveConsensusPasses();
        UnknownAndConflictingFramesFailClosed();
        AppraisalIntroActionIsExactlyOnce();
        AppraisalAlreadyAtBarsUsesNoTap();
        IntroTapMustReachStableBars();
        return Task.CompletedTask;
    }

    public static Task RunStateMachineAsync()
    {
        UnexpectedStatesStop();
        RecoveryCommandUsesOnlyTheRecoveryServiceForRules();
        return Task.CompletedTask;
    }

    public static Task RunExitActionsAsync()
    {
        RecoveryUsesTapForEachAppraisalSubstateThenBack();
        BarsNeverAuthorizeBackAndNoBlindRetryIsAllowed();
        return Task.CompletedTask;
    }

    private static void AnimatedFullScreenWithStableIntroRoiPasses()
    {
        var frames = Enumerable.Range(0, 3)
            .Select(index => IntroFrame(new byte[]
                { (byte)index, (byte)(200 - index), (byte)(index * 37) }))
            .ToArray();
        Assert(GuardedInventoryRecovery.IsStable(frames),
            "animated full-screen pixels do not invalidate stable intro ROIs");
    }

    private static void AnimatedFullScreenWithStableBarsRoiPasses()
    {
        var frames = Enumerable.Range(0, 3)
            .Select(index => BarsFrame(new byte[]
                { (byte)(index * 61), (byte)(255 - index), (byte)index }))
            .ToArray();
        Assert(GuardedInventoryRecovery.IsStable(frames),
            "animated full-screen pixels do not invalidate stable bar ROIs");
    }

    private static void RoiAndBarThresholdsRejectDrift()
    {
        var introDrift = IntroFrame(new byte[] { 3 }) with
        {
            StableRegions = new[] { Roi(0.20), Roi(0.50) }
        };
        Assert(!GuardedInventoryRecovery.IsStable(new[]
            { IntroFrame(new byte[] { 1 }), IntroFrame(new byte[] { 2 }), introDrift }),
            "intro ROI drift above the locked real-capture threshold is rejected");

        var barsDrift = BarsFrame(new byte[] { 3 });
        barsDrift = barsDrift with
        {
            Bars = barsDrift.Bars.Select((bar, index) => index == 0
                ? bar with { FillFraction = bar.FillFraction + 0.10 }
                : bar).ToArray()
        };
        Assert(!GuardedInventoryRecovery.IsStable(new[]
            { BarsFrame(new byte[] { 1 }), BarsFrame(new byte[] { 2 }), barsDrift }),
            "bar fill drift above the locked real-capture threshold is rejected");
    }

    private static void ThreeOfFiveConsensusPasses()
    {
        var frames = new[]
        {
            BarsFrame(new byte[] { 1 }),
            OtherFrame(PokemonGoGameState.PokemonDetails, "transition-a"),
            BarsFrame(new byte[] { 2 }),
            OtherFrame(PokemonGoGameState.PokemonDetails, "transition-b"),
            BarsFrame(new byte[] { 3 })
        };
        Assert(GuardedInventoryRecovery.IsStable(frames),
            "three matching frames among the latest five pass consensus");
    }

    private static void UnknownAndConflictingFramesFailClosed()
    {
        var unknownWindow = new[]
        {
            BarsFrame(new byte[] { 1 }), BarsFrame(new byte[] { 2 }),
            UnknownFrame(), BarsFrame(new byte[] { 3 }), BarsFrame(new byte[] { 4 })
        };
        Assert(!GuardedInventoryRecovery.IsStable(unknownWindow),
            "Unknown in the active ROI window fails closed");

        var conflictWindow = new[]
        {
            BarsFrame(new byte[] { 1 }), BarsFrame(new byte[] { 2 }),
            ConflictingFrame(), BarsFrame(new byte[] { 3 }), BarsFrame(new byte[] { 4 })
        };
        Assert(!GuardedInventoryRecovery.IsStable(conflictWindow),
            "conflicting ROI anchors in the active window fail closed");
    }

    private static void AppraisalIntroActionIsExactlyOnce()
    {
        var intro = Enumerable.Range(0, 3)
            .Select(index => IntroFrame(new byte[] { (byte)index })).ToArray();
        Assert(GuardedInventoryRecovery.DecideAppraisalContinuation(intro, 0, false) ==
            AppraisalContinuationOutcome.TAP_INTRO_ONCE,
            "stable intro authorizes exactly one locator-grounded tap");
        var weak = intro.Select(frame => frame with { LocatorConfidence = 0.59 }).ToArray();
        Assert(!GuardedInventoryRecovery.IsStable(weak),
            "intro locator confidence below the real-device threshold is rejected");
        Assert(GuardedInventoryRecovery.DecideAppraisalContinuation(intro, 1, true) ==
            AppraisalContinuationOutcome.FAIL_CLOSED,
            "a second intro tap is never authorized");
    }

    private static void AppraisalAlreadyAtBarsUsesNoTap()
    {
        var bars = Enumerable.Range(0, 3)
            .Select(index => BarsFrame(new byte[] { (byte)index })).ToArray();
        Assert(GuardedInventoryRecovery.DecideAppraisalContinuation(bars, 0, false) ==
            AppraisalContinuationOutcome.SUCCESS_ALREADY_ADVANCED,
            "stable bars require zero intro taps");
    }

    private static void IntroTapMustReachStableBars()
    {
        var bars = Enumerable.Range(0, 3)
            .Select(index => BarsFrame(new byte[] { (byte)index })).ToArray();
        Assert(GuardedInventoryRecovery.DecideAppraisalContinuation(bars, 1, true) ==
            AppraisalContinuationOutcome.SUCCESS_TAPPED,
            "one intro tap succeeds only after stable bars");
    }

    private static void RecoveryUsesTapForEachAppraisalSubstateThenBack()
    {
        var service = new GuardedInventoryRecovery();
        var intro = Three(() => IntroFrame(new byte[] { 1 }));
        var bars = Three(() => BarsFrame(new byte[] { 2 }));
        Assert(service.Begin(intro) == RecoveryOutcome.PROGRESSED,
            "stable appraisal intro starts recovery");
        var introExit = service.AuthorizeNextAction();
        Assert(introExit is
            {
                Sequence: 1,
                Action: RecoveryInputAction.ExitAppraisal,
                ExpectedState: PokemonGoGameState.Appraisal,
                ExpectedFrameKind: RecoveryFrameKind.AppraisalBars
            }, "AppraisalIntro tap expects stable bars");
        Assert(service.ObservePostAction(bars) == RecoveryOutcome.PROGRESSED,
            "intro tap progresses to bars");

        var barsExit = service.AuthorizeNextAction();
        Assert(barsExit is
            {
                Sequence: 2,
                Action: RecoveryInputAction.ExitAppraisal,
                ExpectedState: PokemonGoGameState.PokemonDetails
            }, "AppraisalBars tap expects Details");
        Assert(service.ObservePostAction(Three(() => OtherFrame(
                PokemonGoGameState.PokemonDetails, "details"))) == RecoveryOutcome.PROGRESSED,
            "bars tap progresses to Details");

        var final = service.AuthorizeNextAction();
        Assert(final is
            {
                Sequence: 3,
                Action: RecoveryInputAction.PressBack,
                ExpectedState: PokemonGoGameState.Inventory
            }, "only Details authorizes Back to Inventory");
        Assert(service.ObservePostAction(Three(() => OtherFrame(
                PokemonGoGameState.Inventory, "inventory"))) == RecoveryOutcome.SUCCEEDED,
            "Inventory completes recovery");
        Assert(service.AuthorizeNextAction() is null && service.InputActions == 3 &&
            service.AppraisalTapActions == 2 && service.BackActions == 1,
            "the three-action limit cannot be exceeded");
    }

    private static void BarsNeverAuthorizeBackAndNoBlindRetryIsAllowed()
    {
        var bars = Three(() => BarsFrame(new byte[] { 1 }));
        var service = new GuardedInventoryRecovery();
        service.Begin(bars);
        var action = service.AuthorizeNextAction();
        Assert(action is { Action: RecoveryInputAction.ExitAppraisal },
            "AppraisalBars never authorizes Android Back");
        Assert(service.BackActions == 0 && service.AppraisalTapActions == 1,
            "bars exit records one appraisal tap and zero Back actions");
        Assert(service.ObservePostAction(bars) == RecoveryOutcome.ACTION_NOT_OBSERVED,
            "unchanged bars record ACTION_NOT_OBSERVED");
        Assert(service.AuthorizeNextAction() is null,
            "unchanged bars cannot authorize a blind repeated tap");
    }

    private static void UnexpectedStatesStop()
    {
        var bars = Three(() => BarsFrame(new byte[] { 1 }));
        var unexpected = new GuardedInventoryRecovery();
        unexpected.Begin(bars);
        unexpected.AuthorizeNextAction();
        Assert(unexpected.ObservePostAction(Three(() => OtherFrame(
                PokemonGoGameState.Inventory, "inventory"))) == RecoveryOutcome.UNEXPECTED_STOP,
            "an unexpected post-state stops fail-closed");

        var unknown = new GuardedInventoryRecovery();
        Assert(unknown.Begin(Three(UnknownFrame)) == RecoveryOutcome.UNKNOWN_STOP,
            "Unknown can never start recovery");
    }

    private static void RecoveryCommandUsesOnlyTheRecoveryServiceForRules()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "PogoInventory.Cli", "Program.cs"));
        var start = source.IndexOf(
            "static async Task<int> RecoverInventoryAsync", StringComparison.Ordinal);
        var end = source.IndexOf(
            "static async Task<int> OpenPokemonGoMainMenuAsync", start,
            StringComparison.Ordinal);
        var method = source[start..end];
        Assert(method.Contains("recovery.Begin(", StringComparison.Ordinal) &&
            method.Contains("recovery.AuthorizeNextAction(", StringComparison.Ordinal) &&
            method.Contains("recovery.ObservePostAction(", StringComparison.Ordinal),
            "recovery command delegates state-machine rules to GuardedInventoryRecovery");
        Assert(method.Contains("RecoveryInputAction.ExitAppraisal", StringComparison.Ordinal) &&
            method.Contains("RecoveryInputAction.PressBack", StringComparison.Ordinal),
            "recovery command executes only the service-authorized named action");
        Assert(!method.Contains("while (state.State", StringComparison.Ordinal) &&
            !method.Contains("CanStartBack", StringComparison.Ordinal) &&
            !method.Contains("AuthorizeBack", StringComparison.Ordinal) &&
            !method.Contains("state.State != expected", StringComparison.Ordinal),
            "old inline recovery rules are removed");
    }

    private static RecoveryFrame IntroFrame(byte[] screenshot) => new()
    {
        Detection = Detection(PokemonGoGameState.Appraisal,
            "AppraisalIntroDetected", "AppraisalIntroDialogDetected",
            "AppraisalIntroOverlayDetected"),
        Screenshot = screenshot,
        EvidenceSignature = "Appraisal|AppraisalIntroDetected|AppraisalIntroDialogDetected|AppraisalIntroOverlayDetected",
        Kind = RecoveryFrameKind.AppraisalIntro,
        HasIntroAnchor = true,
        HasBarsAnchor = false,
        HasConflictingAnchor = false,
        LocatorConfidence = 0.91,
        LocatorTarget = new NormalizedPoint { X = 0.50, Y = 0.88 },
        StableRegions = new[] { Roi(0.20), Roi(0.40) }
    };

    private static RecoveryFrame BarsFrame(byte[] screenshot) => new()
    {
        Detection = Detection(PokemonGoGameState.Appraisal, "AppraisalBarsDetected"),
        Screenshot = screenshot,
        EvidenceSignature = "Appraisal|AppraisalBarsDetected",
        Kind = RecoveryFrameKind.AppraisalBars,
        HasIntroAnchor = false,
        HasBarsAnchor = true,
        HasConflictingAnchor = false,
        StableRegions = new[] { Roi(0.60) },
        Bars = Enum.GetValues<AppraisalBarKind>().Select((kind, index) =>
            new RecoveryBarSignature
            {
                Kind = kind,
                Region = new NormalizedRegion
                    { X = 0.12, Y = 0.60 + index * 0.08, Width = 0.35, Height = 0.03 },
                TrackStartFraction = 0.05,
                TrackEndFraction = 0.90,
                FillFraction = 0.50 + index * 0.10,
                TrackWidthFraction = 0.85,
                ColorAndStructure = Roi(0.30 + index * 0.10)
            }).ToArray()
    };

    private static RecoveryFrame OtherFrame(PokemonGoGameState state, string evidence) => new()
    {
        Detection = Detection(state, evidence),
        Screenshot = new byte[] { 9 },
        EvidenceSignature = $"{state}|{evidence}",
        Kind = RecoveryFrameKind.Other,
        HasIntroAnchor = false,
        HasBarsAnchor = false,
        HasConflictingAnchor = false
    };

    private static RecoveryFrame UnknownFrame() => new()
    {
        Detection = Detection(PokemonGoGameState.Unknown, "unknown"),
        Screenshot = new byte[] { 0 },
        EvidenceSignature = "Unknown|unknown",
        Kind = RecoveryFrameKind.Unknown,
        HasIntroAnchor = false,
        HasBarsAnchor = false,
        HasConflictingAnchor = false
    };

    private static RecoveryFrame ConflictingFrame() => new()
    {
        Detection = Detection(PokemonGoGameState.Appraisal, "conflict"),
        Screenshot = new byte[] { 0 },
        EvidenceSignature = "Appraisal|conflict",
        Kind = RecoveryFrameKind.Conflicting,
        HasIntroAnchor = true,
        HasBarsAnchor = true,
        HasConflictingAnchor = true
    };

    private static RecoveryRoiSignature Roi(double value) =>
        new(Enumerable.Repeat(value, 40).ToArray());

    private static RecoveryFrame[] Three(Func<RecoveryFrame> factory) =>
        Enumerable.Range(0, 3).Select(_ => factory()).ToArray();

    private static PokemonGoGameStateDetection Detection(
        PokemonGoGameState state,
        params string[] evidence) => new()
    {
        State = state,
        Confidence = state == PokemonGoGameState.Unknown ? 0 : 1,
        Evidence = evidence,
        ScreenshotSha256 = string.Join("-", evidence)
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
