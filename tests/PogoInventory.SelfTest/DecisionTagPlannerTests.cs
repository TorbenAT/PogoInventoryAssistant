using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Core.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class DecisionTagPlannerTests
{
    public static Task RunAsync()
    {
        var planner = new DecisionTagPlanner();
        foreach (var category in Enum.GetValues<DecisionCategory>())
        {
            var plan = planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.PokemonDetails, "Complete"), Decision(category, 1), "run-1", 1, "fp-1");
            Assert(plan.DesiredTag is "AI-Indexed" or "AI-Review", "allowlisted tag");
            Assert(plan.MayExecute, "bound plan executes");
            Assert(plan.RunId == "run-1" && plan.Ordinal == 1 && plan.StableFingerprint == "fp-1", "audit binding");
            Assert(plan.RequiresVisualVerification && plan.Evidence.Count == 1, "audit evidence");
            if (category == DecisionCategory.Keep) Assert(plan.DesiredTag == "AI-Indexed", "keep mapping");
            if (category == DecisionCategory.Review) Assert(plan.DesiredTag == "AI-Review", "review mapping");
            if (category == DecisionCategory.Delete) Assert(plan.DesiredTag == "AI-Review", "delete downgrade");
        }

        foreach (var status in new[] { "Partial", "Unknown" })
        {
            var plan = planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.PokemonDetails, status), Decision(DecisionCategory.Keep, 1), "run-1", 1, "fp-1");
            Assert(plan.DesiredTag == "AI-Review", $"{status} review mapping");
        }

        Assert(!planner.Plan(Observation("run-1", 1, "", ScreenState.PokemonDetails, "Complete"), Decision(DecisionCategory.Keep, 1), "run-1", 1, "").MayExecute, "missing fingerprint blocks");
        Assert(!planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.PokemonDetails, "Complete"), Decision(DecisionCategory.Keep, 1), "run-1", 0, "fp-1").MayExecute, "missing ordinal blocks");
        Assert(!planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.InventoryList, "Complete"), Decision(DecisionCategory.Keep, 1), "run-1", 1, "fp-1").MayExecute, "wrong screen blocks");
        Assert(!planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.PokemonDetails, "Complete"), Decision(DecisionCategory.Keep, 2), "run-1", 1, "fp-1").MayExecute, "ordinal mismatch blocks");
        Assert(!planner.Plan(Observation("run-1", 1, "fp-1", ScreenState.PokemonDetails, "Complete"), Decision(DecisionCategory.Keep, 1), "run-1", 1, "fp-1", false).MayExecute, "unknown cursor blocks");
        return Task.CompletedTask;
    }

    private static PokemonDecision Decision(DecisionCategory category, int sequence) => new()
    {
        ExternalKey = $"run-1:{sequence:000000}", SequenceNumber = sequence, Species = "Pikachu",
        GroupKey = "pikachu", Category = category, Reasons = new[] { new DecisionReason("TEST", "test evidence") }, PolicyVersion = "test"
    };

    private static RealScanObservationRecord Observation(string runId, int sequence, string fingerprint, ScreenState state, string status) => new()
    {
        Sequence = sequence, TimestampUtc = DateTimeOffset.UtcNow, DetectedState = state,
        StateConfidence = 1, ScreenshotSha256 = "screen", IdentityFingerprintSha256 = fingerprint,
        ProviderName = "test", AppraisalConfidence = 1, AppraisalStatus = status, ObservationStatus = status,
        VariantIdentity = new PokemonVariantIdentity { SpeciesName = "Pikachu" },
        InstanceEvidence = new PokemonInstanceEvidence
        {
            ScanRunId = runId, Sequence = sequence, InstanceEvidenceKey = $"{runId}:{sequence}",
            ScreenshotSha256 = "screen", IdentityFingerprintSha256 = fingerprint,
            CaptureTimestampUtc = DateTimeOffset.UtcNow, DeviceProfileHash = "device", NavigationAuditReference = "audit"
        }, EvidenceReferences = new[] { "evidence/task-k/item-1.png" }
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
