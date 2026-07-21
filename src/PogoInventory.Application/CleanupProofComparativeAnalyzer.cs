using PogoInventory.Core.Models;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

/// <summary>
/// Comparative (advisory-only) duplicate-group analysis shared between the
/// live <see cref="CleanupProofRunner"/> and the offline
/// <c>analyze-cleanup-evidence</c> reprocess command, so both produce
/// identical <c>comparative-cleanup-suggestions.csv</c> output from the same
/// logic instead of two independently maintained copies.
/// </summary>
public static class CleanupProofComparativeAnalyzer
{
    public static IReadOnlyList<CleanupProofComparativeSuggestion> BuildComparativeSuggestions(
        IReadOnlyList<CleanupProofDatabaseRow> rows)
    {
        var result = new List<CleanupProofComparativeSuggestion>();
        foreach (var group in rows.GroupBy(row => row.Observation.GroupKey, StringComparer.Ordinal))
        {
            var ranked = group.OrderByDescending(row => row.Observation.TotalIv ?? -1)
                .ThenByDescending(row => row.Observation.Cp ?? -1)
                .ThenBy(row => row.Ordinal)
                .ToArray();
            foreach (var row in ranked)
            {
                var comparator = ranked.FirstOrDefault(candidate => candidate.LocalPokemonId != row.LocalPokemonId);
                var missing = ProtectionFields(row.Observation);
                var exactReviewed = row.Observation.HasKnownCriticalValues &&
                    row.Observation.IdentityConfidence == IdentityConfidence.Exact &&
                    row.Observation.VariantIdentity?.VariantKey is not null;
                var protectedKnown = row.Observation.IsFavorite is true || row.Observation.IsShiny is true ||
                    row.Observation.IsBackground is true || row.Observation.IsShadow is true ||
                    row.Observation.IsPurified is true || row.Observation.IsLucky is true ||
                    row.Observation.IsCostume is true || row.Observation.IsDynamax is true ||
                    row.Observation.IsGigantamax is true;
                var better = comparator is not null &&
                    comparator.Observation.TotalIv is not null && row.Observation.TotalIv is not null &&
                    (comparator.Observation.TotalIv > row.Observation.TotalIv ||
                     comparator.Observation.TotalIv == row.Observation.TotalIv && comparator.Observation.Cp > row.Observation.Cp);
                var isRetained = row.LocalPokemonId == ranked[0].LocalPokemonId;
                var classification = isRetained && exactReviewed
                    ? "RETAINED_COMPARATOR"
                    : comparator is null || !exactReviewed || protectedKnown || !better
                        ? "INSUFFICIENT_COMPARISON_DATA"
                        : "LIKELY_DELETE_SUGGESTION";
                result.Add(new CleanupProofComparativeSuggestion
                {
                    LocalPokemonId = row.LocalPokemonId,
                    Species = row.Observation.Species,
                    Classification = classification,
                    ComparatorLocalPokemonId = better ? comparator!.LocalPokemonId : null,
                    Cp = row.Observation.Cp,
                    TotalIv = row.Observation.TotalIv,
                    Ordinal = row.Ordinal,
                    MissingProtectionChecks = missing
                });
            }
        }
        return result.OrderBy(item => item.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> ProtectionFields(PokemonObservation observation) =>
        new (string Name, bool? Value)[]
        {
            ("Favorite", observation.IsFavorite), ("Shiny", observation.IsShiny),
            ("Background", observation.IsBackground), ("Shadow", observation.IsShadow),
            ("Purified", observation.IsPurified), ("Lucky", observation.IsLucky),
            ("Costume", observation.IsCostume), ("Dynamax", observation.IsDynamax),
            ("Gigantamax", observation.IsGigantamax), ("SpecialMove", observation.HasSpecialMove),
            ("Xxl", observation.IsXxl), ("Xxs", observation.IsXxs), ("Nickname", string.IsNullOrWhiteSpace(observation.Nickname) ? null : true),
            ("CatchDate", observation.CatchDate is null ? null : true)
        }.Where(item => item.Value is null).Select(item => item.Name).ToArray();
}
