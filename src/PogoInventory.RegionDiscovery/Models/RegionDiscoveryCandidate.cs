using PogoInventory.Vision.Models;

namespace PogoInventory.RegionDiscovery.Models;

public sealed record RegionDiscoveryCandidate
{
    public required string Id { get; init; }
    public RegionCandidateKind Kind { get; init; }
    public required NormalizedRegion Region { get; init; }
    public double AverageScore { get; init; }
    public double MaximumScore { get; init; }
    public int CellCount { get; init; }
    public required string ProvisionalReason { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(ProvisionalReason))
        {
            throw new InvalidOperationException(
                "Region discovery candidate identity is incomplete.");
        }

        Region.Validate();
        if (!double.IsFinite(AverageScore) ||
            !double.IsFinite(MaximumScore) ||
            AverageScore is < 0 or > 1 ||
            MaximumScore is < 0 or > 1 ||
            CellCount <= 0)
        {
            throw new InvalidOperationException(
                "Region discovery candidate metrics are invalid.");
        }
    }
}
