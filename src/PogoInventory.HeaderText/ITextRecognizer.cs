using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>
/// A single recognized line of text and where it was found, expressed in
/// coordinates normalized against the full source frame (not the cropped ROI).
/// </summary>
public sealed record RecognizedTextLine
{
    public required string Text { get; init; }
    public double? Confidence { get; init; }
    public required NormalizedRegion NormalizedBounds { get; init; }
}

/// <summary>
/// Which header region is being recognized. Lets an implementation apply
/// region-specific preprocessing (e.g. CP-region binarization) without
/// growing the interface beyond a single hint.
/// </summary>
public enum HeaderRegionKind
{
    Name,
    Cp
}

/// <summary>
/// Dependency-light abstraction over an OCR engine. Implementations decode the
/// frame, crop to the requested region of interest and return recognized text
/// lines. Kept free of any platform-specific OCR dependency so higher layers
/// (and tests) can depend on this project alone.
/// </summary>
public interface ITextRecognizer
{
    Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(
        byte[] framePng,
        NormalizedRegion roi,
        CancellationToken cancellationToken = default,
        HeaderRegionKind regionKind = HeaderRegionKind.Name);
}
