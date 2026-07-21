namespace PogoInventory.HeaderText;

/// <summary>
/// The screen the header analyzer is reading from. ROI defaults differ per
/// screen because the CP/name header sits at slightly different positions on
/// the Pokemon Details screen versus the Appraisal bars screen.
/// </summary>
public enum HeaderScreenType
{
    PokemonDetails,
    AppraisalBars
}
