using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record ConfusionMatrixCell
{
    public required ScreenState ExpectedState { get; init; }
    public required ScreenState ActualState { get; init; }
    public required int Count { get; init; }
}
