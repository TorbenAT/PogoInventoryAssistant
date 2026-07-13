namespace PogoInventory.Calibration.Models;

public sealed record CalibrationAcceptancePolicy
{
    public int MaximumFalsePositives { get; init; } = 0;
    public int MaximumMisclassifications { get; init; } = 0;
    public int MaximumWeakAnchors { get; init; } = 0;
    public double MinimumWinnerMargin { get; init; } = 0.05;
    public double MinimumAnchorSeparation { get; init; } = 0.05;
    public IReadOnlyList<StateAcceptanceRequirement> States { get; init; } =
        Array.Empty<StateAcceptanceRequirement>();
}
