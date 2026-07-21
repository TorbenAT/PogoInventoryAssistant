using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>
/// Normalized regions of interest used to locate the CP text and the
/// name/species text on each supported screen. Defaults are starting points
/// derived from the existing identity header region (Y=0.42 h=0.045 in
/// <c>PokemonIdentityFingerprintProfile</c>) and are expected to be tuned by
/// the offline OCR spike against real screenshots.
/// </summary>
public sealed record HeaderAnalysisProfile
{
    public NormalizedRegion DetailsCpRegion { get; init; } = new()
    {
        X = 0.30, Y = 0.03, Width = 0.40, Height = 0.07
    };

    public NormalizedRegion DetailsNameRegion { get; init; } = new()
    {
        X = 0.20, Y = 0.40, Width = 0.60, Height = 0.07
    };

    public NormalizedRegion AppraisalCpRegion { get; init; } = new()
    {
        X = 0.30, Y = 0.03, Width = 0.40, Height = 0.07
    };

    public NormalizedRegion AppraisalNameRegion { get; init; } = new()
    {
        X = 0.20, Y = 0.40, Width = 0.60, Height = 0.07
    };

    public NormalizedRegion CpRegionFor(HeaderScreenType screen) => screen switch
    {
        HeaderScreenType.PokemonDetails => DetailsCpRegion,
        HeaderScreenType.AppraisalBars => AppraisalCpRegion,
        _ => throw new ArgumentOutOfRangeException(nameof(screen))
    };

    public NormalizedRegion NameRegionFor(HeaderScreenType screen) => screen switch
    {
        HeaderScreenType.PokemonDetails => DetailsNameRegion,
        HeaderScreenType.AppraisalBars => AppraisalNameRegion,
        _ => throw new ArgumentOutOfRangeException(nameof(screen))
    };

    public void Validate()
    {
        DetailsCpRegion.Validate(nameof(DetailsCpRegion));
        DetailsNameRegion.Validate(nameof(DetailsNameRegion));
        AppraisalCpRegion.Validate(nameof(AppraisalCpRegion));
        AppraisalNameRegion.Validate(nameof(AppraisalNameRegion));
    }
}
