using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record CalibrationCapturePlan
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Name { get; init; }
    public ScreenOrientation RequiredOrientation { get; init; } = ScreenOrientation.Portrait;
    public int MinimumWidth { get; init; } = 720;
    public int MinimumHeight { get; init; } = 1280;
    public bool LockDeviceSerial { get; init; } = true;
    public bool LockExactGeometry { get; init; } = true;
    public IReadOnlyList<CalibrationCaptureRequirement> Requirements { get; init; } =
        Array.Empty<CalibrationCaptureRequirement>();
}
