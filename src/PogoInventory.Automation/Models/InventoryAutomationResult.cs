namespace PogoInventory.Automation.Models;

public sealed record InventoryAutomationResult
{
    public required InventoryScanCheckpoint Checkpoint { get; init; }
    public required string CheckpointPath { get; init; }
    public required string CaptureDirectory { get; init; }
}
