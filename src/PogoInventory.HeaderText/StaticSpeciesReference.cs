using System.Globalization;
using System.Text;

namespace PogoInventory.HeaderText;

/// <summary>
/// In-memory <see cref="ISpeciesReference"/> backed by a fixed species name
/// list. Used directly by tests, and as the fallback used by the OCR spike
/// command when no reference-data file is available yet.
/// </summary>
public sealed class StaticSpeciesReference : ISpeciesReference
{
    private readonly Dictionary<string, string> _byNormalized;

    public StaticSpeciesReference(IEnumerable<string> species)
    {
        ArgumentNullException.ThrowIfNull(species);
        _byNormalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in species)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var key = Fold(name);
            if (key.Length == 0) continue;
            if (!_byNormalized.ContainsKey(key))
            {
                _byNormalized[key] = name;
            }
        }
    }

    public bool IsKnownSpecies(string text) => NormalizeSpecies(text) is not null;

    public string? NormalizeSpecies(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var folded = Fold(text);
        if (folded.Length == 0) return null;

        if (_byNormalized.TryGetValue(folded, out var exact))
        {
            return exact;
        }

        foreach (var candidate in _byNormalized)
        {
            if (Math.Abs(candidate.Key.Length - folded.Length) > 1) continue;
            if (IsWithinEditDistanceOne(candidate.Key, folded))
            {
                return candidate.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Case- and diacritic-folds text down to letters and digits only,
    /// dropping punctuation and symbols such as the Nidoran gender glyphs.
    /// </summary>
    internal static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }
        return builder.ToString();
    }

    private static bool IsWithinEditDistanceOne(string first, string second)
    {
        if (first == second) return true;

        if (first.Length == second.Length)
        {
            var mismatches = 0;
            for (var index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    mismatches++;
                    if (mismatches > 1) return false;
                }
            }
            return mismatches <= 1;
        }

        if (Math.Abs(first.Length - second.Length) != 1) return false;

        var shorter = first.Length < second.Length ? first : second;
        var longer = first.Length < second.Length ? second : first;
        var shortIndex = 0;
        var longIndex = 0;
        var edits = 0;
        while (shortIndex < shorter.Length && longIndex < longer.Length)
        {
            if (shorter[shortIndex] == longer[longIndex])
            {
                shortIndex++;
                longIndex++;
                continue;
            }
            edits++;
            if (edits > 1) return false;
            longIndex++;
        }
        return true;
    }
}
