using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

internal sealed record ScreenProbe
{
    public required byte[] ScreenshotPng { get; init; }
    public required ScreenDetectionResult Detection { get; init; }
    public required byte[] IdentityFingerprint { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
}
