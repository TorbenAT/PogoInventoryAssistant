using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public sealed class ExplorationPlanner
{
    public IReadOnlyList<ExplorationAction> RankCandidates(
        ScreenObservation observation,
        IReadOnlyCollection<ExplorationAction> candidates,
        IReadOnlyCollection<StateTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(transitions);

        return candidates
            .Where(IsExecutable)
            .Where(action => action.ExpectedFromStates.Count == 0 ||
                action.ExpectedFromStates.Contains(observation.DetectedState))
            .OrderByDescending(action => Score(action, observation, transitions))
            .ThenBy(action => action.ActionName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExecutable(ExplorationAction action) =>
        action.RiskLevel is ExplorationRiskLevel.ReadOnly or ExplorationRiskLevel.ReversibleMutation;

    private static int Score(
        ExplorationAction action,
        ScreenObservation observation,
        IReadOnlyCollection<StateTransition> transitions)
    {
        var score = 0;
        if (action.RequiresAnchor && observation.VisibleAnchors.Count == 0)
        {
            return int.MinValue;
        }

        if (action.RequiresUiNode && observation.VisibleControls.Count == 0)
        {
            return int.MinValue;
        }

        if (!transitions.Any(x =>
                string.Equals(x.BeforeObservation, observation.ObservationId, StringComparison.Ordinal) &&
                string.Equals(x.ActionName, action.ActionName, StringComparison.Ordinal)))
        {
            score += 100;
        }

        if (observation.DetectedState == ScreenState.ExternalOverlay ||
            observation.DetectedState == ScreenState.KnownInformationalPopup)
        {
            score += action.ActionName.Contains("Dismiss", StringComparison.OrdinalIgnoreCase) ? 200 : -200;
        }

        return score + (action.RiskLevel == ExplorationRiskLevel.ReadOnly ? 10 : 1);
    }
}
