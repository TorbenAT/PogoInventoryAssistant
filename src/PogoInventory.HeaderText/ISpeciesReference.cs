namespace PogoInventory.HeaderText;

/// <summary>
/// Reference data used to validate OCR'd header text against known Pokémon
/// species names. Implementations should be case- and diacritic-insensitive
/// and should tolerate small OCR noise (edit distance &lt;= 1), for example the
/// gender glyphs on Nidoran (Nidoran♀ / Nidoran♂).
/// </summary>
public interface ISpeciesReference
{
    bool IsKnownSpecies(string text);

    /// <summary>
    /// Returns the canonical species name for the given OCR text, or null if
    /// the text does not match any known species within tolerance.
    /// </summary>
    string? NormalizeSpecies(string text);
}
