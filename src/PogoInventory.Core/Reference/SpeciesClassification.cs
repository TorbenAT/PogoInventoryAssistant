namespace PogoInventory.Core.Reference;

/// <summary>
/// Reference classification of a Pokémon species. This is metadata about the species
/// itself (from a versioned static data file), not an observation of a specific
/// caught Pokémon.
/// </summary>
public enum SpeciesClassification
{
    Ordinary,
    Legendary,
    Mythical,
    UltraBeast
}
