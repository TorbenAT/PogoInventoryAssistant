using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record AnchorQualityMetric
{
    public required ScreenState State { get; init; }
    public required string AnchorName { get; init; }
    public required AnchorExpectation Expectation { get; init; }
    public required double MatchThreshold { get; init; }
    public required int PositiveFixtureCount { get; init; }
    public required int NegativeFixtureCount { get; init; }
    public double? PositiveMinimumSimilarity { get; init; }
    public double? PositiveAverageSimilarity { get; init; }
    public double? PositiveMaximumSimilarity { get; init; }
    public double? NegativeMaximumSimilarity { get; init; }
    public double? Separation { get; init; }
    public required int PositiveConditionFailureCount { get; init; }
    public required int NegativeMatchedCount { get; init; }
    public required bool Weak { get; init; }
    public IReadOnlyList<string> WeakReasons { get; init; } = Array.Empty<string>();
}
