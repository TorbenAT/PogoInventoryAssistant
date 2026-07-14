namespace PogoInventory.ImagePretest.Models;

public sealed record ImagePretestCluster
{
    public required string Id { get; init; }
    public required string RepresentativeFileName { get; init; }
    public IReadOnlyList<string> Members { get; init; } = Array.Empty<string>();
    public int Count => Members.Count;
}
