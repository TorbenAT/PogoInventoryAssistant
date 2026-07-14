using PogoInventory.RegionDiscovery.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasSelectedRegion
{
    public required string CandidateId { get; init; }
    public RegionCandidateKind Kind { get; init; }
    public required NormalizedRegion Region { get; init; }
    public double Score { get; init; }
    public required string SourceReason { get; init; }
    public required string SheetFile { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId) ||
            string.IsNullOrWhiteSpace(SourceReason) ||
            string.IsNullOrWhiteSpace(SheetFile) ||
            !double.IsFinite(Score) ||
            Score is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Crop atlas selected region is invalid.");
        }

        Region.Validate(CandidateId);
    }
}
