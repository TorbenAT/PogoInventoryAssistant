using System.Text.RegularExpressions;

namespace PogoInventory.HeaderText;

public enum SearchQueryKind
{
    ExactSpecies,
    BroadFilter
}

public sealed record SearchQueryClassification
{
    public required SearchQueryKind Kind { get; init; }

    /// <summary>Only set when <see cref="Kind"/> is <see cref="SearchQueryKind.ExactSpecies"/>.</summary>
    public string? Species { get; init; }
}

/// <summary>
/// Classifies a Pokémon GO inventory search query as either an exact species
/// query (optionally combined with non-species filters via "&amp;", e.g.
/// "pidgey&amp;age0-365") or a broad filter query (e.g. "age0-1825",
/// "0*,1*,2*", "distance200-", "!favorite", "#Trade").
/// </summary>
public static class SearchQueryClassifier
{
    private static readonly Regex NumericPrefixToken = new(@"^[a-zA-Z]+[0-9]", RegexOptions.Compiled);

    public static SearchQueryClassification Classify(string query, ISpeciesReference speciesReference)
    {
        ArgumentNullException.ThrowIfNull(speciesReference);

        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchQueryClassification { Kind = SearchQueryKind.BroadFilter };
        }

        var tokens = query.Trim().Split(
            '&',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return new SearchQueryClassification { Kind = SearchQueryKind.BroadFilter };
        }

        string? speciesToken = null;
        foreach (var token in tokens)
        {
            if (LooksLikeBroadFilterToken(token)) continue;

            if (speciesToken is not null)
            {
                // More than one species-like token: not a simple exact-species query.
                return new SearchQueryClassification { Kind = SearchQueryKind.BroadFilter };
            }
            speciesToken = token;
        }

        if (speciesToken is null)
        {
            return new SearchQueryClassification { Kind = SearchQueryKind.BroadFilter };
        }

        var normalized = speciesReference.NormalizeSpecies(speciesToken);
        return normalized is null
            ? new SearchQueryClassification { Kind = SearchQueryKind.BroadFilter }
            : new SearchQueryClassification { Kind = SearchQueryKind.ExactSpecies, Species = normalized };
    }

    private static bool LooksLikeBroadFilterToken(string token)
    {
        if (token.Length == 0) return true;

        // Pokemon GO search filter operator prefixes.
        if (token[0] is '!' or '#' or '@') return true;

        // Wildcard / list style filters, e.g. "0*,1*,2*".
        if (token.Contains(',', StringComparison.Ordinal)) return true;
        if (token.Contains('*', StringComparison.Ordinal)) return true;

        // Ranged numeric filters, e.g. "age0-1825", "distance200-".
        if (token.Contains('-', StringComparison.Ordinal) && token.Any(char.IsDigit)) return true;

        // Keyword directly followed by digits without a range separator, e.g. "cp1000".
        if (NumericPrefixToken.IsMatch(token) && token.Any(char.IsDigit)) return true;

        return false;
    }
}
