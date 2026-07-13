namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureResult
{
    public required CalibrationCaptureRecord Capture { get; init; }
    public required CalibrationCaptureStatus Status { get; init; }
    public required string AbsoluteImagePath { get; init; }
    public required string SessionPath { get; init; }
}
