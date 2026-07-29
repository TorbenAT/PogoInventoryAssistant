using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.SelfTest;

internal static class AppraisalHandoffEvaluatorTests
{
    public static Task StableFramesArmHandoffAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1), true); evaluator.Observe(Frame(2), true);
        Assert(evaluator.Observe(Frame(3), true).Status == AppraisalHandoffStatus.Ready, "Three distinct stable frames did not arm handoff.");
        return Task.CompletedTask;
    }

    public static Task TransitionAndDuplicateFramesDoNotArmAsync()
    {
        var evaluator = new AppraisalHandoffEvaluator(Options(), Regions());
        evaluator.Observe(Frame(1, motion: .2), true);
        evaluator.Observe(Frame(2), true); evaluator.Observe(Frame(2), true);
        Assert(evaluator.Observe(Frame(3), true).Status == AppraisalHandoffStatus.Pending, "Transition or duplicate frame counted as stable evidence.");
        Assert(evaluator.Observe(Frame(4), true).Status == AppraisalHandoffStatus.Ready, "Fresh third stable frame did not arm handoff.");
        return Task.CompletedTask;
    }

    public static Task FingerprintChangeAndFilterEndAreFailClosedAsync()
    {
        var baseline = AppraisalHandoffEvaluator.StableFingerprint(Frame(0), Options().RequiredRegions);
        var unchanged = new AppraisalHandoffEvaluator(Options(), Regions(), baseline);
        unchanged.Observe(Frame(1), true);
        Assert(unchanged.CompleteTimeout().Status == AppraisalHandoffStatus.NoEffectOrFilterEnd, "Stable unchanged fingerprint was not filter-end.");
        var changed = new AppraisalHandoffEvaluator(Options(), Regions(), baseline);
        changed.Observe(Frame(1, fingerprint: 2), true); changed.Observe(Frame(2, fingerprint: 2), true);
        Assert(changed.Observe(Frame(3, fingerprint: 2), true).Status == AppraisalHandoffStatus.Ready, "Changed stable fingerprint was not accepted.");
        return Task.CompletedTask;
    }

    private static StableRegionGateOptions Options() => new() { RequiredRegions = ["Header", "AppraisalPanel", "AttackBar", "DefenseBar", "HpBar"], MinimumStableFrames = 3, MaximumMotionScore = .05, MaximumDifferenceScore = .04, MinimumSimilarityScore = .94, MinimumSharpnessScore = .025 };
    private static IReadOnlyList<RegionDefinition> Regions() => Options().RequiredRegions.Select(x => new RegionDefinition { Name = x, Region = new NormalizedRegion(0, 0, .1, .1), StabilityRole = RegionStabilityRole.Required }).ToArray();
    private static TemporalFrameObservation Frame(long id, double motion = 0, ulong fingerprint = 1)
    {
        var regions = Options().RequiredRegions.ToDictionary(x => x, x => new RegionalFrameObservation { RegionName = x, StabilityRole = RegionStabilityRole.Required, ObserveTransition = true, MotionScore = motion, DifferenceScore = 0, SimilarityScore = 1, SharpnessScore = 1, BrightnessScore = .5, ContrastScore = .5, ChangeVelocity = 0, VisualFingerprint = fingerprint, IsLikelyStable = motion <= .05, IsLikelyTransitioning = motion > .05 });
        return new TemporalFrameObservation { FrameId = new FrameId(id), SourceTicks = id, MonotonicTimestamp = TimeSpan.FromMilliseconds(id * 100), UtcTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(id), GlobalDifferenceScore = 0, RegionalDifferenceScores = regions.ToDictionary(x => x.Key, _ => 0d), MotionScore = motion, SharpnessScore = 1, FreezeScore = 0, BrightnessScore = .5, ContrastScore = .5, Resolution = new FrameResolution(10, 10), IsLikelyStable = motion <= .05, IsLikelyTransitioning = motion > .05, QualityFlags = TemporalQualityFlags.None, Regions = regions, VisualFingerprint = fingerprint, ObservationDuration = TimeSpan.Zero };
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
