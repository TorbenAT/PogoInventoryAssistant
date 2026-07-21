namespace PogoInventory.Core.Reference;

public sealed record SpeciesCpRange
{
    public required int Min { get; init; }
    public required int Max { get; init; }
}
