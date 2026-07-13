using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureRecord
{
    public required string Id { get; init; }
    public required int SequenceNumber { get; init; }
    public required ScreenState ExpectedState { get; init; }
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required string DeviceSerial { get; init; }
    public string? DeviceModel { get; init; }
    public string? AndroidVersion { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required ScreenOrientation Orientation { get; init; }
    public string? Notes { get; init; }
    public string? DuplicateOfCaptureId { get; init; }
    public string? PromotedFixtureId { get; init; }

    public bool IsDuplicate => !string.IsNullOrWhiteSpace(DuplicateOfCaptureId);
    public bool IsPromoted => !string.IsNullOrWhiteSpace(PromotedFixtureId);
}
