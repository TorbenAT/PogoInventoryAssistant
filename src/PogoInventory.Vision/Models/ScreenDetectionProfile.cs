namespace PogoInventory.Vision.Models;

public sealed record ScreenDetectionProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Name { get; init; }
    public ScreenOrientation RequiredOrientation { get; init; } = ScreenOrientation.Portrait;
    public int MinimumWidth { get; init; } = 1;
    public int MinimumHeight { get; init; } = 1;
    public double MinimumAspectRatio { get; init; } = 0.1;
    public double MaximumAspectRatio { get; init; } = 10.0;
    public double MinimumStateScore { get; init; } = 0.85;
    public double MinimumWinnerMargin { get; init; } = 0.05;
    public IReadOnlyList<ScreenStateDefinition> States { get; init; } =
        Array.Empty<ScreenStateDefinition>();
}
