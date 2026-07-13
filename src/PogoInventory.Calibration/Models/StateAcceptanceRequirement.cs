using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record StateAcceptanceRequirement
{
    public required ScreenState State { get; init; }
    public int MinimumApprovedFixtures { get; init; } = 1;
    public double MinimumRecall { get; init; } = 1.0;
}
