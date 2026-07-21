using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>
/// Normalized regions of interest used to locate the CP text and the
/// name/species text on each supported screen. Defaults are the Tesseract
/// iteration-4 tuned CP ROI (tightened height, fixes the Volbeat 961->761
/// false positive) against the real 20-item appraisal/details evidence set
/// (see docs/HEADER_OCR.md); the WinRT spike path can override via
/// --profile to reproduce its own prior 60/60 species measurement.
/// </summary>
public sealed record HeaderAnalysisProfile
{
    public NormalizedRegion DetailsCpRegion { get; init; } = new()
    {
        X = 0.28, Y = 0.08, Width = 0.44, Height = 0.05
    };

    public NormalizedRegion DetailsNameRegion { get; init; } = new()
    {
        X = 0.15, Y = 0.41, Width = 0.70, Height = 0.08
    };

    public NormalizedRegion AppraisalCpRegion { get; init; } = new()
    {
        X = 0.28, Y = 0.08, Width = 0.44, Height = 0.05
    };

    public NormalizedRegion AppraisalNameRegion { get; init; } = new()
    {
        X = 0.15, Y = 0.41, Width = 0.70, Height = 0.08
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
