using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureStateProgress
{
    public required ScreenState State { get; init; }
    public required int RequiredUniqueCaptures { get; init; }
    public required int UniqueCaptureCount { get; init; }
    public required int DuplicateCaptureCount { get; init; }
    public required int PromotedCaptureCount { get; init; }
    public required bool OptionalWhenUnavailable { get; init; }
    public required string Instruction { get; init; }
    public required IReadOnlyList<string> VariationHints { get; init; }

    public int Remaining => Math.Max(0, RequiredUniqueCaptures - UniqueCaptureCount);
    public bool Complete => Remaining == 0;
}
