namespace PogoInventory.Calibration.Models;

public sealed record ScreenFixtureManifest
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Name { get; init; }
    public required string ProfileName { get; init; }
    public CalibrationAcceptancePolicy Acceptance { get; init; } = new();
    public IReadOnlyList<ScreenFixtureDefinition> Fixtures { get; init; } =
        Array.Empty<ScreenFixtureDefinition>();
}
