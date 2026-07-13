namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCapturePromotionResult
{
    public required string CaptureId { get; init; }
    public required string FixtureId { get; init; }
    public required string FixturePath { get; init; }
    public required string ManifestPath { get; init; }
    public required string SessionPath { get; init; }
    public required bool AlreadyPromoted { get; init; }
}
