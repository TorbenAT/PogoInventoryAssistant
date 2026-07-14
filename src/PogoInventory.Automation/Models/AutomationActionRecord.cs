using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public sealed record AutomationActionRecord
{
    public required int SequenceNumber { get; init; }
    public required AutomationActionKind Kind { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public ScreenState? StateBefore { get; init; }
    public ScreenState? StateAfter { get; init; }
    public string? Detail { get; init; }
}
