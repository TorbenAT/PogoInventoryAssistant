namespace PogoInventory.Automation.Models;

public sealed record InventoryScanCheckpoint
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string RunId { get; init; }
    public required string AutomationProfileName { get; init; }
    public required string AutomationProfileSha256 { get; init; }
    public required string ScreenProfileSha256 { get; init; }
    public required string DeviceSerial { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public AutomationRunStatus Status { get; init; } = AutomationRunStatus.Running;
    public AutomationStopReason StopReason { get; init; } = AutomationStopReason.None;
    public string? StopDetail { get; init; }
    public IReadOnlyList<InventoryScanItem> Items { get; init; } = Array.Empty<InventoryScanItem>();
    public IReadOnlyList<AutomationActionRecord> Actions { get; init; } =
        Array.Empty<AutomationActionRecord>();
}
