using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>
/// Normalized regions of interest used to locate the CP text and the
/// name/species text on each supported screen. Defaults are spike-tuned
/// against the real 20-item appraisal/details evidence set (see
/// local-data/validation/ocr-spike/profile-wide.json and
/// docs/HEADER_OCR.md), not the original starting-point guesses.
/// </summary>
public sealed record HeaderAnalysisProfile
{
    public NormalizedRegion DetailsCpRegion { get; init; } = new()
    {
        X = 0.28, Y = 0.07, Width = 0.44, Height = 0.07
    };

    public NormalizedRegion DetailsNameRegion { get; init; } = new()
    {
        X = 0.15, Y = 0.41, Width = 0.70, Height = 0.08
    };

    public NormalizedRegion AppraisalCpRegion { get; init; } = new()
    {
        X = 0.28, Y = 0.07, Width = 0.44, Height = 0.07
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
