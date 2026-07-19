using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

public sealed class ExplorationRecoveryService
{
    public IReadOnlyList<RecoveryRoute> FindRoutes(
        string fromState,
        IReadOnlyCollection<RecoveryRoute> routes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromState);
        ArgumentNullException.ThrowIfNull(routes);
        return routes
            .Where(route => string.Equals(route.FromState, fromState, StringComparison.Ordinal))
            .Where(route => route.Confidence >= 0.95)
            .OrderByDescending(route => route.Confidence)
            .ToArray();
    }
}
