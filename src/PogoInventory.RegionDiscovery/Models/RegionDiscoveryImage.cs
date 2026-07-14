namespace PogoInventory.RegionDiscovery.Models;

public sealed record RegionDiscoveryImage
{
    public int SequenceNumber { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required string ClusterId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
