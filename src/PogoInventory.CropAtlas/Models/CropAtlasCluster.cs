namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasCluster
{
    public required string ClusterId { get; init; }
    public int ImageCount { get; init; }
    public IReadOnlyList<string> RepresentativeFiles { get; init; } =
        Array.Empty<string>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClusterId) ||
            ImageCount <= 0 ||
            RepresentativeFiles.Count <= 0 ||
            RepresentativeFiles.Count > ImageCount ||
            RepresentativeFiles.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Crop atlas cluster is invalid.");
        }
    }
}
