namespace PogoInventory.Core.Reference;

public sealed record SpeciesReferenceEntry
{
    public required string Name { get; init; }
    public required int DexNumber { get; init; }
    public required SpeciesClassification Classification { get; init; }
}
