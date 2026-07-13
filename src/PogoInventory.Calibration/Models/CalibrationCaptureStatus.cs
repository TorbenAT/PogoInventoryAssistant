using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureStatus
{
    public required string PlanName { get; init; }
    public required string SessionId { get; init; }
    public required int TotalCaptureCount { get; init; }
    public required int UniqueCaptureCount { get; init; }
    public required int DuplicateCaptureCount { get; init; }
    public required int PromotedCaptureCount { get; init; }
    public required bool RequiredCoverageComplete { get; init; }
    public ScreenState? NextRecommendedState { get; init; }
    public IReadOnlyList<CalibrationCaptureStateProgress> States { get; init; } =
        Array.Empty<CalibrationCaptureStateProgress>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
