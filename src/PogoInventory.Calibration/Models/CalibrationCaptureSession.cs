namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCaptureSession
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Id { get; init; }
    public required string PlanName { get; init; }
    public required string PlanSha256 { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public string? LockedDeviceSerial { get; init; }
    public string? LockedDeviceModel { get; init; }
    public int? LockedImageWidth { get; init; }
    public int? LockedImageHeight { get; init; }
    public IReadOnlyList<CalibrationCaptureRecord> Captures { get; init; } =
        Array.Empty<CalibrationCaptureRecord>();
}
