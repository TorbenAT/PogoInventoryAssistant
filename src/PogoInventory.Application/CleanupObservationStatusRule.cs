using PogoInventory.Core.Models;

namespace PogoInventory.Application;

/// <summary>
/// Single source of truth for recomputing a cleanup-proof row's
/// <c>ObservationStatus</c> from the observation's actual field completeness.
/// Shared by <see cref="CleanupProofRunner"/> (live capture) and
/// <see cref="CleanupEvidenceReprocessor"/> (offline reprocess) so both call
/// sites agree on exactly one definition of "Complete" -- Complete may never
/// survive an Unknown CP/IV, in either path.
/// </summary>
public static class CleanupObservationStatusRule
{
    /// <summary>
    /// Complete requires species + CP + all three IVs known; otherwise the
    /// row is never better than Partial. Never upgrades beyond what the
    /// evidence supports: a row that was Unresolved stays Unresolved rather
    /// than being promoted to Partial just because it is not Complete.
    /// </summary>
    public static string Recompute(PokemonObservation observation, string originalStatus)
    {
        var criticalTripleKnown =
            !string.Equals(observation.Species, "Unknown", StringComparison.Ordinal) &&
            observation.Cp is not null &&
            observation.AttackIv is not null &&
            observation.DefenseIv is not null &&
            observation.HpIv is not null;
        if (criticalTripleKnown) return "Complete";
        return string.Equals(originalStatus, "Unresolved", StringComparison.Ordinal) ? "Unresolved" : "Partial";
    }
}
