namespace PogoInventory.HeaderText;

/// <summary>
/// Per-region Tesseract knobs (character whitelist + whether to force
/// single-line segmentation). Kept as plain data -- no dependency on the
/// Tesseract package itself -- so it stays testable from
/// PogoInventory.SelfTest, which does not (and per policy should not)
/// reference a native-binding NuGet package. The Tesseract-specific
/// recognizer (PogoInventory.TesseractOcr) maps this to the real
/// TesseractOCR.Enums.PageSegMode / Engine.SetVariable calls.
/// </summary>
public readonly record struct TesseractOcrConfig(string CharWhitelist, bool SingleLine);

public static class TesseractOcrConfigSelector
{
    /// <summary>
    /// CP text is always "CP" (or noisy variants) followed by digits --
    /// restricting the whitelist to exactly those glyphs removes the
    /// candidate confusion (e.g. thin "1" vs background noise) that a full
    /// alphanumeric dictionary invites.
    /// </summary>
    public const string CpWhitelist = "CPcp0123456789";

    /// <summary>Species/nickname text is unrestricted alpha (plus gender glyphs); no whitelist.</summary>
    public const string NameWhitelist = "";

    public static TesseractOcrConfig ConfigFor(HeaderRegionKind regionKind) => regionKind switch
    {
        HeaderRegionKind.Cp => new TesseractOcrConfig(CpWhitelist, SingleLine: true),
        HeaderRegionKind.Name => new TesseractOcrConfig(NameWhitelist, SingleLine: true),
        _ => throw new ArgumentOutOfRangeException(nameof(regionKind))
    };
}
