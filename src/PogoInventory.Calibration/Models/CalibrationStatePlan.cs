using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationStatePlan
{
    public required ScreenState State { get; init; }
    public IReadOnlyList<CalibrationAnchorDefinition> Anchors { get; init; } =
        Array.Empty<CalibrationAnchorDefinition>();
}
