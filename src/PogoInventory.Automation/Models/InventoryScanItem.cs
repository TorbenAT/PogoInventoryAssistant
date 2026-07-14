using PogoInventory.Observations.Models;

namespace PogoInventory.Automation.Models;

public sealed record InventoryScanItem
{
    public required int SequenceNumber { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required string ScreenshotFileName { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required string IdentityFingerprintBase64 { get; init; }
    public required string IdentityFingerprintSha256 { get; init; }
    public required double ScreenStateConfidence { get; init; }
    public CalcyObservation Observation { get; init; } =
        CalcyObservation.Unavailable(
            "CheckpointMigration",
            "This item was captured before observation schema 2.0.");
}
