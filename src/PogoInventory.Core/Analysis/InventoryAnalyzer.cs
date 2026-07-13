using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;

namespace PogoInventory.Core.Analysis;

public sealed class InventoryAnalyzer
{
    public InventoryAnalysisResult Analyze(
        IReadOnlyCollection<PokemonObservation> observations,
        RulePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(policy);

        ValidatePolicy(policy);
        ValidateObservations(observations);

        var decisions = new List<PokemonDecision>(observations.Count);

        foreach (var group in observations
                     .OrderBy(x => x.SequenceNumber)
                     .GroupBy(x => x.GroupKey, StringComparer.Ordinal))
        {
            decisions.AddRange(AnalyzeGroup(group.ToList(), policy));
        }

        return new InventoryAnalysisResult
        {
            Decisions = decisions.OrderBy(x => x.SequenceNumber).ToList()
        };
    }

    private static IEnumerable<PokemonDecision> AnalyzeGroup(
        IReadOnlyList<PokemonObservation> group,
        RulePolicy policy)
    {
        var resolved = new Dictionary<string, PokemonDecision>(StringComparer.Ordinal);
        var ordinaryCandidates = new List<PokemonObservation>();

        foreach (var pokemon in group)
        {
            var hardKeepReasons = GetHardKeepReasons(pokemon, policy);
            if (hardKeepReasons.Count > 0)
            {
                resolved[pokemon.ExternalKey] = Decision(
                    pokemon,
                    DecisionCategory.Keep,
                    hardKeepReasons,
                    policy);
                continue;
            }

            var reviewReasons = GetReviewReasons(pokemon, policy);
            if (reviewReasons.Count > 0)
            {
                resolved[pokemon.ExternalKey] = Decision(
                    pokemon,
                    DecisionCategory.Review,
                    reviewReasons,
                    policy);
                continue;
            }

            ordinaryCandidates.Add(pokemon);
        }

        if (ordinaryCandidates.Count > 0)
        {
            ResolveOrdinaryCandidates(ordinaryCandidates, resolved, policy);
        }

        return group.Select(x => resolved[x.ExternalKey]);
    }

    private static void ResolveOrdinaryCandidates(
        IReadOnlyList<PokemonObservation> candidates,
        IDictionary<string, PokemonDecision> resolved,
        RulePolicy policy)
    {
        var ranked = candidates
            .OrderByDescending(x => x.TotalIv ?? -1)
            .ThenByDescending(x => x.Cp)
            .ThenBy(x => x.SequenceNumber)
            .ToList();

        var protectedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var keeper in ranked.Take(policy.MinimumOrdinaryCopiesPerSpeciesForm))
        {
            protectedKeys.Add(keeper.ExternalKey);
            resolved[keeper.ExternalKey] = Decision(
                keeper,
                DecisionCategory.Keep,
                new[]
                {
                    new DecisionReason(
                        "KEEP_MINIMUM_COPY",
                        "Selected as an ordinary retained copy for this species, form and costume group.")
                },
                policy);
        }

        if (policy.PvpHeuristic.Enabled && policy.PvpHeuristic.PreserveBestCandidatePerGroup)
        {
            var pvpCandidate = ranked
                .Where(x => IsPreliminaryPvpCandidate(x, policy.PvpHeuristic))
                .OrderByDescending(PvpHeuristicScore)
                .ThenBy(x => x.SequenceNumber)
                .FirstOrDefault();

            if (pvpCandidate is not null && !protectedKeys.Contains(pvpCandidate.ExternalKey))
            {
                protectedKeys.Add(pvpCandidate.ExternalKey);
                resolved[pvpCandidate.ExternalKey] = Decision(
                    pvpCandidate,
                    DecisionCategory.Review,
                    new[]
                    {
                        new DecisionReason(
                            "REVIEW_PVP_HEURISTIC",
                            "Best preliminary low-Attack, high-Defense/HP PvP candidate in this duplicate group. Full species and league analysis is not implemented yet.")
                    },
                    policy);
            }
        }

        foreach (var pokemon in ranked.Where(x => !protectedKeys.Contains(x.ExternalKey)))
        {
            if (policy.DeleteRequiresExactIdentity && pokemon.IdentityConfidence != IdentityConfidence.Exact)
            {
                resolved[pokemon.ExternalKey] = Decision(
                    pokemon,
                    DecisionCategory.Review,
                    new[]
                    {
                        new DecisionReason(
                            "REVIEW_IDENTITY_NOT_EXACT",
                            "Identity confidence is not Exact, so the Pokémon cannot be marked DELETE.")
                    },
                    policy);
                continue;
            }

            var better = ranked.FirstOrDefault(x =>
                protectedKeys.Contains(x.ExternalKey) &&
                IsStrictlyBetter(x, pokemon));

            if (better is null && policy.DeleteRequiresStrictlyBetterDuplicate)
            {
                resolved[pokemon.ExternalKey] = Decision(
                    pokemon,
                    DecisionCategory.Review,
                    new[]
                    {
                        new DecisionReason(
                            "REVIEW_NO_STRICTLY_BETTER_DUPLICATE",
                            "No retained duplicate is strictly better by total IV and CP.")
                    },
                    policy);
                continue;
            }

            resolved[pokemon.ExternalKey] = Decision(
                pokemon,
                DecisionCategory.Delete,
                new[]
                {
                    new DecisionReason(
                        "DELETE_REDUNDANT_DUPLICATE",
                        $"A retained duplicate ({better?.ExternalKey ?? "not required by policy"}) is strictly better, and no protection rule applies.")
                },
                policy,
                better?.ExternalKey);
        }
    }

    private static List<DecisionReason> GetHardKeepReasons(
        PokemonObservation pokemon,
        RulePolicy policy)
    {
        var reasons = new List<DecisionReason>();

        if (pokemon.IsPerfect)
        {
            reasons.Add(new("KEEP_PERFECT", "Perfect 15/15/15 IV."));
        }

        AddIfTrue(reasons, pokemon.IsShiny, "KEEP_SHINY", "Shiny Pokémon.");
        AddIfTrue(reasons, pokemon.IsMythical, "KEEP_MYTHICAL", "Mythical Pokémon.");
        AddIfTrue(reasons, pokemon.IsBackground, "KEEP_BACKGROUND", "Background or location-card Pokémon.");
        AddIfTrue(reasons, pokemon.IsFavorite, "KEEP_FAVORITE", "Marked as favorite.");

        if (pokemon.CatchDate is not null && pokemon.CatchDate <= policy.OldPokemonCutoff)
        {
            reasons.Add(new(
                "KEEP_OLD",
                $"Caught on {pokemon.CatchDate.Value:yyyy-MM-dd}, on or before the configured cutoff {policy.OldPokemonCutoff:yyyy-MM-dd}."));
        }

        if (pokemon.Tags.Any(tag => policy.TradeTagNames.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            reasons.Add(new("KEEP_TRADE_TAG", "Has a configured Trade tag."));
        }

        if (!string.IsNullOrWhiteSpace(pokemon.Nickname) &&
            policy.TradeNicknameFragments.Any(fragment =>
                pokemon.Nickname.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add(new("KEEP_TRADE_NICKNAME", "Nickname contains a configured Trade fragment."));
        }

        return reasons;
    }

    private static List<DecisionReason> GetReviewReasons(
        PokemonObservation pokemon,
        RulePolicy policy)
    {
        var reasons = new List<DecisionReason>();

        if (!pokemon.HasKnownCriticalValues)
        {
            reasons.Add(new(
                "REVIEW_UNKNOWN_CRITICAL_DATA",
                "One or more critical values are unknown. Unknown data cannot be treated as absence."));
        }

        if (policy.DeleteRequiresExactIdentity && pokemon.IdentityConfidence != IdentityConfidence.Exact)
        {
            reasons.Add(new(
                "REVIEW_IDENTITY_NOT_EXACT",
                "Identity confidence is not Exact."));
        }

        AddIfTrue(reasons, pokemon.IsLegendary, "REVIEW_LEGENDARY", "Legendary Pokémon.");
        AddIfTrue(reasons, pokemon.IsUltraBeast, "REVIEW_ULTRA_BEAST", "Ultra Beast.");
        AddIfTrue(reasons, pokemon.IsShadow, "REVIEW_SHADOW", "Shadow Pokémon.");
        AddIfTrue(reasons, pokemon.IsPurified, "REVIEW_PURIFIED", "Purified Pokémon.");
        AddIfTrue(reasons, pokemon.IsLucky, "REVIEW_LUCKY", "Lucky Pokémon.");
        AddIfTrue(reasons, pokemon.IsCostume, "REVIEW_COSTUME", "Costume Pokémon.");
        AddIfTrue(reasons, pokemon.IsDynamax, "REVIEW_DYNAMAX", "Dynamax Pokémon.");
        AddIfTrue(reasons, pokemon.IsGigantamax, "REVIEW_GIGANTAMAX", "Gigantamax Pokémon.");
        AddIfTrue(reasons, pokemon.HasSpecialMove, "REVIEW_SPECIAL_MOVE", "Has a special or legacy move.");
        AddIfTrue(reasons, pokemon.IsXxl, "REVIEW_XXL", "XXL Pokémon.");
        AddIfTrue(reasons, pokemon.IsXxs, "REVIEW_XXS", "XXS Pokémon.");

        return reasons;
    }

    private static bool IsPreliminaryPvpCandidate(
        PokemonObservation pokemon,
        PvpHeuristicPolicy policy) =>
        pokemon.AttackIv <= policy.MaximumAttackIv &&
        pokemon.DefenseIv >= policy.MinimumDefenseIv &&
        pokemon.HpIv >= policy.MinimumHpIv;

    private static int PvpHeuristicScore(PokemonObservation pokemon) =>
        ((15 - pokemon.AttackIv!.Value) * 100) +
        (pokemon.DefenseIv!.Value * 10) +
        pokemon.HpIv!.Value;

    private static bool IsStrictlyBetter(
        PokemonObservation candidate,
        PokemonObservation target)
    {
        var candidateIv = candidate.TotalIv ?? -1;
        var targetIv = target.TotalIv ?? -1;

        return candidateIv > targetIv ||
               (candidateIv == targetIv && candidate.Cp > target.Cp);
    }

    private static PokemonDecision Decision(
        PokemonObservation pokemon,
        DecisionCategory category,
        IEnumerable<DecisionReason> reasons,
        RulePolicy policy,
        string? betterDuplicateExternalKey = null) =>
        new()
        {
            ExternalKey = pokemon.ExternalKey,
            SequenceNumber = pokemon.SequenceNumber,
            Species = pokemon.Species,
            GroupKey = pokemon.GroupKey,
            Category = category,
            Reasons = reasons.ToList(),
            PolicyVersion = policy.Version,
            BetterDuplicateExternalKey = betterDuplicateExternalKey
        };

    private static void AddIfTrue(
        ICollection<DecisionReason> reasons,
        bool? value,
        string code,
        string message)
    {
        if (value is true)
        {
            reasons.Add(new(code, message));
        }
    }

    private static void ValidatePolicy(RulePolicy policy)
    {
        if (policy.MinimumOrdinaryCopiesPerSpeciesForm < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policy),
                "MinimumOrdinaryCopiesPerSpeciesForm must be at least 1.");
        }

        if (policy.PvpHeuristic.MaximumAttackIv is < 0 or > 15 ||
            policy.PvpHeuristic.MinimumDefenseIv is < 0 or > 15 ||
            policy.PvpHeuristic.MinimumHpIv is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policy),
                "PvP heuristic IV thresholds must be between 0 and 15.");
        }
    }

    private static void ValidateObservations(IReadOnlyCollection<PokemonObservation> observations)
    {
        var duplicateKeys = observations
            .GroupBy(x => x.ExternalKey, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate ExternalKey values: {string.Join(", ", duplicateKeys)}");
        }

        foreach (var pokemon in observations)
        {
            ValidateIv(pokemon.AttackIv, pokemon.ExternalKey, nameof(pokemon.AttackIv));
            ValidateIv(pokemon.DefenseIv, pokemon.ExternalKey, nameof(pokemon.DefenseIv));
            ValidateIv(pokemon.HpIv, pokemon.ExternalKey, nameof(pokemon.HpIv));
        }
    }

    private static void ValidateIv(int? value, string externalKey, string field)
    {
        if (value is < 0 or > 15)
        {
            throw new InvalidOperationException(
                $"{externalKey}: {field} must be between 0 and 15 when present.");
        }
    }
}
