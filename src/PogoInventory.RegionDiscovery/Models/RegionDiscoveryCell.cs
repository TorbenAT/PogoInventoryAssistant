using PogoInventory.Vision.Models;

namespace PogoInventory.RegionDiscovery.Models;

public sealed record RegionDiscoveryCell
{
    public int Row { get; init; }
    public int Column { get; init; }
    public required NormalizedRegion Region { get; init; }
    public double MeanLuminance { get; init; }
    public double MeanEdgeDensity { get; init; }
    public double GlobalVariation { get; init; }
    public double ConsecutiveVariation { get; init; }
    public double WithinClusterVariation { get; init; }
    public double BetweenClusterSeparation { get; init; }
    public double StableChromeScore { get; init; }
    public double ScreenStateScore { get; init; }
    public double DynamicContentScore { get; init; }
    public double TextDensityScore { get; init; }

    public double ScoreFor(RegionCandidateKind kind) =>
        kind switch
        {
            RegionCandidateKind.StableChrome => StableChromeScore,
            RegionCandidateKind.ScreenStateDiscriminator => ScreenStateScore,
            RegionCandidateKind.DynamicContent => DynamicContentScore,
            RegionCandidateKind.TextDense => TextDensityScore,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public void Validate()
    {
        Region.Validate();
        foreach (var value in new[]
        {
            MeanLuminance,
            MeanEdgeDensity,
            GlobalVariation,
            ConsecutiveVariation,
            WithinClusterVariation,
            BetweenClusterSeparation,
            StableChromeScore,
            ScreenStateScore,
            DynamicContentScore,
            TextDensityScore
        })
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    "Region discovery cell metrics must be between zero and one.");
            }
        }
    }
}
