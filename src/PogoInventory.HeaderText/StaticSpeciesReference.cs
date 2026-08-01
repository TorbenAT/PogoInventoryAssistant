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

        // An OCR edit is trustworthy only when it identifies exactly one
        // reference entry. Enumeration order must never decide identity.
        var candidates = _byNormalized
            .Where(candidate =>
                Math.Abs(candidate.Key.Length - folded.Length) <= 1 &&
                IsWithinEditDistanceOne(candidate.Key, folded))
            .Take(2)
            .ToArray();
        if (candidates.Length == 1)
        {
            return candidates[0].Value;
        }

        return null;
    }

    /// <summary>
    /// Resolves a terminally truncated reference label only when exactly one
    /// species has the supplied folded prefix and the omitted suffix is small.
    /// This is intentionally separate from normal OCR edit tolerance.
    /// </summary>
    public string? NormalizeTerminalPrefix(
        string text,
        int maximumMissingTerminalCharacters)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            maximumMissingTerminalCharacters is < 1 or > 2)
        {
            return null;
        }

        var folded = Fold(text);
        if (folded.Length < 6)
        {
            return null;
        }

        var candidates = _byNormalized
            .Where(candidate =>
                candidate.Key.StartsWith(folded, StringComparison.Ordinal) &&
                candidate.Key.Length - folded.Length is >= 1 and <= 2)
            .Take(2)
            .ToArray();
        return candidates.Length == 1 ? candidates[0].Value : null;
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
