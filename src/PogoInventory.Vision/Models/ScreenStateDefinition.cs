namespace PogoInventory.Vision.Models;

public sealed record ScreenStateDefinition
{
    public required ScreenState State { get; init; }
    public IReadOnlyList<ScreenAnchorDefinition> Anchors { get; init; } =
        Array.Empty<ScreenAnchorDefinition>();
}
