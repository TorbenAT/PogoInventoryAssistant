using System.Text.RegularExpressions;

namespace PogoInventory.HeaderText;

/// <summary>
/// Resolves only narrowly evidenced header-label variants. A suffix is never
/// discarded generally: it must be an exact 100 marker (including the common
/// OCR rendering <c>l00</c>) plus harmless punctuation, or punctuation alone.
/// The remaining token must still resolve uniquely through the reference.
/// </summary>
public static class ReferenceSafeSpeciesResolver
{
    private static readonly Regex SafeSuffix = new(
        @"^\s*(?:(?:100|[1lI][0Oo][0Oo])\s*)?[.=_-]+\s*$|^\s*(?:100|[1lI][0Oo][0Oo])\s*[.=_-]*\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? Resolve(string text, ISpeciesReference reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(reference);

        var direct = reference.NormalizeSpecies(text);
        if (direct is not null)
        {
            return direct;
        }

        for (var split = 1; split < text.Length; split++)
        {
            var prefix = text[..split];
            if (!prefix.All(char.IsLetter))
            {
                break;
            }

            if (!SafeSuffix.IsMatch(text[split..]))
            {
                continue;
            }

            var resolved = reference.NormalizeSpecies(prefix) ??
                (reference as StaticSpeciesReference)?.NormalizeTerminalPrefix(
                    prefix, maximumMissingTerminalCharacters: 2);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }
}
