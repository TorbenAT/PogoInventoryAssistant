using PogoInventory.RegionDiscovery.Models;

namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasCrop
{
    public required string CandidateId { get; init; }
    public RegionCandidateKind Kind { get; init; }
    public required string ClusterId { get; init; }
    public int RepresentativeIndex { get; init; }
    public required string SourceFile { get; init; }
    public required string CropFile { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required string Sha256 { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId) ||
            string.IsNullOrWhiteSpace(ClusterId) ||
            RepresentativeIndex < 0 ||
            string.IsNullOrWhiteSpace(SourceFile) ||
            string.IsNullOrWhiteSpace(CropFile) ||
            Width <= 0 ||
            Height <= 0 ||
            Sha256.Length != 64)
        {
            throw new InvalidOperationException(
                "Crop atlas crop is invalid.");
        }
    }
}
