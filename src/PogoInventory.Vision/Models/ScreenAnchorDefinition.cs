namespace PogoInventory.Vision.Models;

public sealed record ScreenAnchorDefinition
{
    public required string Name { get; init; }
    public required NormalizedRegion Region { get; init; }
    public FingerprintMode Mode { get; init; } = FingerprintMode.Grayscale;
    public AnchorExpectation Expectation { get; init; } = AnchorExpectation.Required;
    public int FingerprintWidth { get; init; } = 16;
    public int FingerprintHeight { get; init; } = 16;
    public double MatchThreshold { get; init; } = 0.90;
    public double Weight { get; init; } = 1.0;
    public IReadOnlyList<string> SamplesBase64 { get; init; } = Array.Empty<string>();
}
