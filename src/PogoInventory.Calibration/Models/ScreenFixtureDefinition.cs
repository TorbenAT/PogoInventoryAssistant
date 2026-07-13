using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Models;

public sealed record ScreenFixtureDefinition
{
    public required string Id { get; init; }
    public required string RelativePath { get; init; }
    public required ScreenState ExpectedState { get; init; }
    public required string Sha256 { get; init; }
    public required FixtureSafetyReview SafetyReview { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? Notes { get; init; }
}
