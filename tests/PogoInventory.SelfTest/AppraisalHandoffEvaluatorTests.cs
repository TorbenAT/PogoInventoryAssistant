using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.SelfTest;

internal static class AppraisalHandoffEvaluatorTests
{
    public static Task StableFramesArmHandoffAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1), true); evaluator.Observe(Frame(3), true);
        Assert(evaluator.Observe(Frame(5), true).Status == AppraisalHandoffStatus.Ready, "Three independent stable frames did not arm handoff.");
        return Task.CompletedTask;
    }

    public static Task TransitionAndDuplicateFramesDoNotArmAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1, motion: .2), true);
        evaluator.Observe(Frame(2), true); evaluator.Observe(Frame(2), true);
        Assert(evaluator.Observe(Frame(4), true).Status == AppraisalHandoffStatus.Pending, "Transition or duplicate frame counted as stable evidence.");
        Assert(evaluator.Observe(Frame(6), true).Status == AppraisalHandoffStatus.Ready, "Fresh third stable frame did not arm handoff.");
        return Task.CompletedTask;
    }

    public static Task FingerprintChangeAndFilterEndAreFailClosedAsync()
    {
        var baseline = AppraisalHandoffEvaluator.StableFingerprint(Frame(0), Options().RequiredRegions);
        var unchanged = new AppraisalHandoffEvaluator(Options(), Regions(), baseline);
        unchanged.Observe(Frame(1), true);
        Assert(unchanged.CompleteTimeout().Status == AppraisalHandoffStatus.NoEffectOrFilterEnd, "Stable unchanged fingerprint was not filter-end.");
        var changed = new AppraisalHandoffEvaluator(Options(), Regions(), baseline);
        changed.Observe(Frame(1, motion: .2, fingerprint: 0), true);
        changed.Observe(Frame(2, fingerprint: ulong.MaxValue), true);
        changed.Observe(Frame(4, fingerprint: ulong.MaxValue), true);
        Assert(changed.Observe(Frame(6, fingerprint: ulong.MaxValue), true).Status == AppraisalHandoffStatus.Ready, "Changed stable fingerprint was not accepted.");
        return Task.CompletedTask;
    }

    public static Task CrossItemFramesNeverMixAsync()
    {
        var baseline = AppraisalHandoffEvaluator.StableFingerprint(
            Frame(0, fingerprint: 0), Options().RequiredRegions);
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions(), baseline);
        evaluator.Observe(Frame(1, fingerprint: 0), true);
        evaluator.Observe(Frame(3, fingerprint: 0), true);
        evaluator.Observe(Frame(4, motion: .2, fingerprint: 0), true);
        var firstChanged = evaluator.Observe(Frame(5, fingerprint: ulong.MaxValue), true);
        Assert(firstChanged.Status == AppraisalHandoffStatus.Pending,
            "Two pre-swipe frames plus one changed frame armed a mixed-item handoff.");
        Assert(firstChanged.QualifiedFrameIds.Select(x => x.Value).SequenceEqual([5L]),
            "Pre-swipe frame IDs leaked into the changed-item evidence set.");
        evaluator.Observe(Frame(7, fingerprint: ulong.MaxValue), true);
        Assert(evaluator.Observe(Frame(9, fingerprint: ulong.MaxValue), true).Status == AppraisalHandoffStatus.Ready,
            "Three matching changed-item frames did not arm handoff.");
        return Task.CompletedTask;
    }

    public static Task HighSimilarityNeedsTransitionAndSemanticProofAsync()
    {
        var baseline = AppraisalHandoffEvaluator.StableFingerprint(
            Frame(0, fingerprint: 0), Options().RequiredRegions);
        var noTransition = new AppraisalHandoffEvaluator(
            Options(), Regions(), baseline);
        noTransition.Observe(Frame(1, fingerprint: 1), true);
        noTransition.Observe(Frame(3, fingerprint: 1), true);
        Assert(
            noTransition.Observe(Frame(5, fingerprint: 1), true).Status ==
                AppraisalHandoffStatus.Pending,
            "A high-similarity candidate armed without observed transition.");

        var missingPrevious = new AppraisalHandoffEvaluator(
            Options(), Regions(), baseline);
        missingPrevious.Observe(
            Frame(1, motion: .2, fingerprint: 0,
                qualityFlags: TemporalQualityFlags.MissingPreviousFrame),
            true);
        missingPrevious.Observe(Frame(2, fingerprint: 1), true);
        missingPrevious.Observe(Frame(4, fingerprint: 1), true);
        Assert(
            missingPrevious.Observe(Frame(6, fingerprint: 1), true).Status ==
                AppraisalHandoffStatus.Pending,
            "A missing previous frame was misused as transition evidence.");

        var transitioned = new AppraisalHandoffEvaluator(
            Options(), Regions(), baseline);
        transitioned.Observe(Frame(1, motion: .2, fingerprint: 0), true);
        transitioned.Observe(Frame(2, fingerprint: 1), true);
        transitioned.Observe(Frame(4, fingerprint: 1), true);
        var ready = transitioned.Observe(Frame(6, fingerprint: 1), true);
        Assert(ready.Status == AppraisalHandoffStatus.Ready,
            "A stable high-similarity candidate did not arm after transition.");
        Assert(ready.ReasonCounts.ContainsKey("SemanticProgressionProofRequired"),
            "A high-similarity candidate did not require semantic progression proof.");

        var observedDuringAction = new AppraisalHandoffEvaluator(
            Options(), Regions(), baseline,
            postActionTransitionObserved: true);
        observedDuringAction.Observe(Frame(2, fingerprint: 1), true);
        observedDuringAction.Observe(Frame(4, fingerprint: 1), true);
        var readyAfterAction = observedDuringAction.Observe(
            Frame(6, fingerprint: 1), true);
        Assert(readyAfterAction.Status == AppraisalHandoffStatus.Ready,
            "A transition observed during the named action did not arm post-action evidence.");
        Assert(readyAfterAction.ReasonCounts.ContainsKey(
                "ActionTransitionObserved"),
            "The action-time transition was not retained in the audit reasons.");
        Assert(readyAfterAction.ReasonCounts.ContainsKey(
                "SemanticProgressionProofRequired"),
            "A high-similarity post-action candidate bypassed semantic proof.");
        return Task.CompletedTask;
    }

    public static Task CandidateFingerprintChangesResetEvidenceAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1, fingerprint: 0), true);
        evaluator.Observe(Frame(3, fingerprint: 0), true);
        var changed = evaluator.Observe(Frame(5, fingerprint: ulong.MaxValue), true);
        Assert(changed.Status == AppraisalHandoffStatus.Pending,
            "Frames from two candidate fingerprints armed a handoff.");
        Assert(changed.QualifiedFrameIds.Select(x => x.Value).SequenceEqual([5L]),
            "Candidate fingerprint change did not reset the evidence window.");
        evaluator.Observe(Frame(7, fingerprint: ulong.MaxValue), true);
        Assert(evaluator.Observe(Frame(9, fingerprint: ulong.MaxValue), true).Status == AppraisalHandoffStatus.Ready,
            "Replacement candidate did not arm after three matching frames.");
        return Task.CompletedTask;
    }

    public static Task CompatibleFingerprintJitterArmsAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1, fingerprint: 0), true);
        evaluator.Observe(Frame(3, fingerprint: 1), true);
        Assert(
            evaluator.Observe(Frame(5, fingerprint: 2), true).Status ==
                AppraisalHandoffStatus.Ready,
            "Three regionally compatible pHash frames did not arm the handoff.");
        return Task.CompletedTask;
    }

    public static Task RegionalSharpnessOverrideAllowsUniformIvBarAsync()
    {
        var options = Options() with
        {
            MinimumSharpnessScoreByRegion =
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["AttackBar"] = .015
                }
        };
        var evaluator = new AppraisalHandoffEvaluator(options, Regions());
        evaluator.Observe(Frame(1, attackSharpness: .0216), true);
        evaluator.Observe(Frame(3, attackSharpness: .0216), true);
        Assert(
            evaluator.Observe(Frame(5, attackSharpness: .0216), true).Status ==
                AppraisalHandoffStatus.Ready,
            "A valid uniform IV bar did not use its configured regional sharpness threshold.");

        var rejected = new AppraisalHandoffEvaluator(options, Regions());
        rejected.Observe(Frame(1, attackSharpness: .01), true);
        rejected.Observe(Frame(3, attackSharpness: .01), true);
        var rejectedSnapshot = rejected.Observe(Frame(5, attackSharpness: .01), true);
        Assert(
            rejectedSnapshot.Status == AppraisalHandoffStatus.Pending,
            "A bar below the regional sharpness threshold was accepted.");
        Assert(
            rejectedSnapshot.ReasonCounts.ContainsKey("SharpnessTooLow:AttackBar"),
            "A gate rejection did not retain its failed region for diagnosis.");

        var zeroIvOptions = Options() with
        {
            MinimumSharpnessScoreByRegion =
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["AttackBar"] = 0,
                    ["DefenseBar"] = 0,
                    ["HpBar"] = 0
                }
        };
        var zeroIv = new AppraisalHandoffEvaluator(zeroIvOptions, Regions());
        zeroIv.Observe(Frame(1, attackSharpness: 0), true);
        zeroIv.Observe(Frame(3, attackSharpness: 0), true);
        Assert(
            zeroIv.Observe(Frame(5, attackSharpness: 0), true).Status ==
                AppraisalHandoffStatus.Ready,
            "A valid uniform zero-IV bar was rejected as an unsharp frame.");
        return Task.CompletedTask;
    }

    public static Task EvidenceSpacingIsEnforcedAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1), true);
        evaluator.Observe(Frame(2), true);
        evaluator.Observe(Frame(3), true);
        Assert(
            evaluator.Observe(Frame(4), true).Status == AppraisalHandoffStatus.Pending,
            "Frames below the configured ID spacing armed independent evidence.");
        var ready = evaluator.Observe(Frame(5), true);
        Assert(ready.Status == AppraisalHandoffStatus.Ready,
            "Three correctly spaced frames did not arm evidence.");
        Assert(ready.QualifiedFrameIds.Select(x => x.Value).SequenceEqual([1L, 3L, 5L]),
            "The evaluator did not retain the expected independent frames.");
        return Task.CompletedTask;
    }

    private static StableRegionGateOptions Options() => new() { RequiredRegions = ["Header", "AppraisalPanel", "AttackBar", "DefenseBar", "HpBar"], MinimumStableFrames = 3, MaximumMotionScore = .05, MaximumDifferenceScore = .04, MinimumSimilarityScore = .94, MinimumSharpnessScore = .025 };
    private static IReadOnlyList<RegionDefinition> Regions() => Options().RequiredRegions.Select(x => new RegionDefinition { Name = x, Region = new NormalizedRegion(0, 0, .1, .1), StabilityRole = RegionStabilityRole.Required }).ToArray();
    private static TemporalFrameObservation Frame(
        long id,
        double motion = 0,
        ulong fingerprint = 1,
        double attackSharpness = 1,
        TemporalQualityFlags qualityFlags = TemporalQualityFlags.None)
    {
        var regions = Options().RequiredRegions.ToDictionary(x => x, x => new RegionalFrameObservation { RegionName = x, StabilityRole = RegionStabilityRole.Required, ObserveTransition = true, MotionScore = motion, DifferenceScore = 0, SimilarityScore = 1, SharpnessScore = string.Equals(x, "AttackBar", StringComparison.Ordinal) ? attackSharpness : 1, BrightnessScore = .5, ContrastScore = .5, ChangeVelocity = 0, VisualFingerprint = fingerprint, IsLikelyStable = motion <= .05, IsLikelyTransitioning = motion > .05 });
        return new TemporalFrameObservation { FrameId = new FrameId(id), SourceTicks = id, MonotonicTimestamp = TimeSpan.FromMilliseconds(id * 100), UtcTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(id), GlobalDifferenceScore = 0, RegionalDifferenceScores = regions.ToDictionary(x => x.Key, _ => 0d), MotionScore = motion, SharpnessScore = 1, FreezeScore = 0, BrightnessScore = .5, ContrastScore = .5, Resolution = new FrameResolution(10, 10), IsLikelyStable = motion <= .05, IsLikelyTransitioning = motion > .05, QualityFlags = qualityFlags, Regions = regions, VisualFingerprint = fingerprint, ObservationDuration = TimeSpan.Zero };
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
