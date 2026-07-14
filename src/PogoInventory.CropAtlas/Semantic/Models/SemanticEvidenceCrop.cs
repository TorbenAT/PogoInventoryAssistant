using PogoInventory.RegionDiscovery.Models;

namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceCrop
{
    public required string CandidateId { get; init; }
    public RegionCandidateKind Kind { get; init; }
    public required string File { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required string Sha256 { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId) ||
            string.IsNullOrWhiteSpace(File) ||
            Width <= 0 ||
            Height <= 0 ||
            Sha256.Length != 64)
        {
            throw new InvalidOperationException(
                "Semantic evidence crop is invalid.");
        }
    }
}
