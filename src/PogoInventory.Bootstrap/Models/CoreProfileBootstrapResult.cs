using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Bootstrap.Models;

public sealed record CoreProfileBootstrapResult
{
    public required string OutputDirectory { get; init; }
    public required string CaptureDirectory { get; init; }
    public required string ManifestPath { get; init; }
    public required string ProfilePath { get; init; }
    public required string AcceptanceDirectory { get; init; }
    public required ScreenDetectionProfile Profile { get; init; }
    public required CalibrationAcceptanceReport Acceptance { get; init; }
    public required IReadOnlyList<string> CapturedFiles { get; init; }
}
