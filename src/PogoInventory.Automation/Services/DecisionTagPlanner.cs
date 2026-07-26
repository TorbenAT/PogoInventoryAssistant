using PogoInventory.Automation.Models;
using PogoInventory.Core.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Services;

public sealed class DecisionTagPlanner
{
    public static readonly IReadOnlySet<string> AllowedTags =
        new HashSet<string>(StringComparer.Ordinal) { "AI-Indexed", "AI-Review" };

    public TagPlan Plan(
        RealScanObservationRecord observation,
        PokemonDecision decision,
        string runId,
        int ordinal,
        string stableFingerprint,
        bool cursorStateKnown = true)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var desiredTag = decision.Category == DecisionCategory.Keep
            ? "AI-Indexed"
            : "AI-Review";
        var reason = decision.Category == DecisionCategory.Delete
            ? "DeleteCandidate is mapped to non-destructive AI-Review."
            : decision.Category == DecisionCategory.Keep
                ? "Decision KEEP mapped to AI-Indexed."
                : "Decision REVIEW mapped to AI-Review.";
        var rejection = ValidateBinding(
            observation, decision, runId, ordinal, stableFingerprint, cursorStateKnown);

        if (observation.ObservationStatus is not ("Complete" or "Candidate"))
        {
            desiredTag = "AI-Review";
            reason = $"Observation status {observation.ObservationStatus} requires review.";
        }
        if (decision.Category == DecisionCategory.Keep && observation.SpeciesName is null)
        {
            desiredTag = "AI-Review";
            reason = "Critical species identity is missing; KEEP is downgraded to review.";
        }

        return new TagPlan
        {
            DesiredTag = desiredTag,
            RunId = runId,
            Ordinal = ordinal,
            StableFingerprint = stableFingerprint,
            DecisionType = decision.Category,
            Reason = reason,
            RequiresVisualVerification = true,
            MayExecute = rejection is null,
            RejectionReason = rejection,
            VerifiedScreenState = observation.DetectedState,
            Evidence = decision.Reasons,
            EvidenceReferences = observation.EvidenceReferences.ToArray()
        };
    }

    private static string? ValidateBinding(
        RealScanObservationRecord observation,
        PokemonDecision decision,
        string runId,
        int ordinal,
        string stableFingerprint,
        bool cursorStateKnown)
    {
        if (!AllowedTags.Contains(decision.Category == DecisionCategory.Keep ? "AI-Indexed" : "AI-Review"))
            return "Planner produced a tag outside the allowlist.";
        if (!cursorStateKnown)
            return "Cursor state is unknown or contradictory.";
        if (ordinal <= 0 || decision.SequenceNumber != ordinal || observation.Sequence != ordinal)
            return "Observation, decision and requested ordinal are not the same item.";
        if (!string.Equals(observation.InstanceEvidence.ScanRunId, runId, StringComparison.Ordinal))
            return "Observation belongs to a different run.";
        if (string.IsNullOrWhiteSpace(stableFingerprint) ||
            !string.Equals(observation.IdentityFingerprintSha256, stableFingerprint, StringComparison.Ordinal))
            return "Stable fingerprint is missing or does not bind the observation.";
        if (observation.DetectedState is not (ScreenState.PokemonDetails or ScreenState.AppraisalOpen))
            return "Current phone state is not verified as Details or Appraisal.";
        return null;
    }
}
