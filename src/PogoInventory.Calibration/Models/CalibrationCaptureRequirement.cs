using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureRequirement
{
    public required ScreenState State { get; init; }
    public int RequiredUniqueCaptures { get; init; } = 1;
    public required string Instruction { get; init; }
    public IReadOnlyList<string> VariationHints { get; init; } = Array.Empty<string>();
    public bool OptionalWhenUnavailable { get; init; }
}
