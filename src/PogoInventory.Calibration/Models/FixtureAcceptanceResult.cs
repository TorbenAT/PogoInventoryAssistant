using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record FixtureAcceptanceResult
{
    public required string FixtureId { get; init; }
    public required string RelativePath { get; init; }
    public required ScreenState ExpectedState { get; init; }
    public required ScreenState ActualState { get; init; }
    public required FixtureOutcome Outcome { get; init; }
    public required double Confidence { get; init; }
    public double? WinnerMargin { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
