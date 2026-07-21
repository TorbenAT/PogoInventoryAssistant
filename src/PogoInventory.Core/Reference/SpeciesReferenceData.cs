using System.Globalization;
using System.Text;

namespace PogoInventory.Core.Reference;

/// <summary>
/// Immutable, versioned reference data describing all known Pokémon species and their
/// rarity classification. This is loaded once (typically from
/// <c>data/reference/species-reference.json</c>) and consulted for read-only lookups.
///
/// Lookups are conservative: an unrecognised species name never resolves to "not
/// protected" or "not legendary/mythical". Callers must treat a null/unknown result as
/// unknown, never as false, per the project rule that unknown data is never equivalent
/// to false.
/// </summary>
public sealed class SpeciesReferenceData
{
    private readonly Dictionary<string, SpeciesReferenceEntry> _byNormalizedName;

    public string Version { get; }
    public string Source { get; }
    public SpeciesCpRange CpRange { get; }
    public IReadOnlyList<SpeciesReferenceEntry> Species { get; }

    internal SpeciesReferenceData(
        string version,
        string source,
        SpeciesCpRange cpRange,
        IReadOnlyList<SpeciesReferenceEntry> species)
    {
        Version = version;
        Source = source;
        CpRange = cpRange;
        Species = species;

        _byNormalizedName = new Dictionary<string, SpeciesReferenceEntry>(StringComparer.Ordinal);
        foreach (var entry in species)
        {
            _byNormalizedName[Normalize(entry.Name)] = entry;
        }
    }

    /// <summary>
    /// True only if the species name is recognised in the reference data. A false
    /// result means "unknown", not "confirmed not a Pokémon species".
    /// </summary>
    public bool IsKnownSpecies(string? name) =>
        !string.IsNullOrWhiteSpace(name) && _byNormalizedName.ContainsKey(Normalize(name));

    /// <summary>
    /// Returns the reference classification for a known species, or null when the
    /// species is not recognised. Null must be treated as unknown by callers, never as
    /// Ordinary.
    /// </summary>
    public SpeciesClassification? Classification(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _byNormalizedName.TryGetValue(Normalize(name), out var entry)
            ? entry.Classification
            : null;
    }

    /// <summary>
    /// True when the species is known and classified as Legendary, Mythical or
    /// UltraBeast. False when the species is known and Ordinary. Null when the species
    /// is not recognised at all -- callers must never collapse null into false.
    /// </summary>
    public bool? IsProtectedRarity(string? name)
    {
        var classification = Classification(name);
        if (classification is null)
        {
            return null;
        }

        return classification is SpeciesClassification.Legendary
            or SpeciesClassification.Mythical
            or SpeciesClassification.UltraBeast;
    }

    /// <summary>
    /// Case-insensitive, diacritic-insensitive, whitespace-trimmed normalisation key used
    /// for species name lookups (so "Nidoran-M", "nidoran m", "Flabébé" and "Flabebe"
    /// style variations resolve consistently).
    /// </summary>
    internal static string Normalize(string name)
    {
        var trimmed = name.Trim();
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
