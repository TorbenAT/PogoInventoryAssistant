namespace PogoInventory.Vision.Models;

public sealed record StateEvidence
{
    public required ScreenState State { get; init; }
    public required double Score { get; init; }
    public required bool Eligible { get; init; }
    public IReadOnlyList<string> RejectionReasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AnchorEvidence> Anchors { get; init; } =
        Array.Empty<AnchorEvidence>();
}
