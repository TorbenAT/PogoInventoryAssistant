namespace PogoInventory.Vision.Models;

public sealed record KnownPokemonGoModal
{
    public required KnownPokemonGoModalId Id { get; init; }
    public required double Confidence { get; init; }
    public required string BeforeScreenshotSha256 { get; init; }
    public string? AfterScreenshotSha256 { get; init; }
    public ScreenState StateBefore { get; init; } = ScreenState.ExternalOverlay;
    public ScreenState? StateAfter { get; init; }
    public bool DismissalAllowed { get; init; }
    public string? Detail { get; init; }

    public void Validate()
    {
        if (!double.IsFinite(Confidence) || Confidence < 0 || Confidence > 1)
        {
            throw new InvalidOperationException("Modal confidence must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(BeforeScreenshotSha256))
        {
            throw new InvalidOperationException("Modal evidence requires a before screenshot hash.");
        }

        if (DismissalAllowed && Id != KnownPokemonGoModalId.NewMegaLevelAvailable)
        {
            throw new InvalidOperationException("Only explicitly allow-listed informational modals may be dismissed.");
        }

        if (DismissalAllowed && StateAfter is null)
        {
            throw new InvalidOperationException("A dismissible modal requires an expected post-dismiss state.");
        }
    }
}
