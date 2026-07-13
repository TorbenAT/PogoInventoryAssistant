using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record StateAcceptanceMetric
{
    public required ScreenState State { get; init; }
    public required int ExpectedCount { get; init; }
    public required int CorrectCount { get; init; }
    public required int UnknownCount { get; init; }
    public required int WrongKnownStateCount { get; init; }
    public required double Recall { get; init; }
    public required int MinimumApprovedFixtures { get; init; }
    public required double MinimumRecall { get; init; }
    public required bool Accepted { get; init; }
}
