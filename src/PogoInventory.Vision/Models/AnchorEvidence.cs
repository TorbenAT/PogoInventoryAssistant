namespace PogoInventory.Vision.Models;

public sealed record AnchorEvidence
{
    public required string Name { get; init; }
    public required AnchorExpectation Expectation { get; init; }
    public required FingerprintMode Mode { get; init; }
    public required NormalizedRegion Region { get; init; }
    public required double Similarity { get; init; }
    public required double MatchThreshold { get; init; }
    public required bool Matched { get; init; }
    public required bool ConditionSatisfied { get; init; }
    public required double Weight { get; init; }
    public int? BestSampleIndex { get; init; }
}
