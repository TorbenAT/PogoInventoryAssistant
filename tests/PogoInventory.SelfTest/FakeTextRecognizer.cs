using PogoInventory.HeaderText;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Scripted <see cref="ITextRecognizer"/> for header OCR tests. Never touches
/// a real OCR engine so self-tests stay independent of PogoInventory.HeaderOcr
/// (which targets a Windows-only TFM).
/// </summary>
internal sealed class FakeTextRecognizer : ITextRecognizer
{
    private readonly Func<NormalizedRegion, IReadOnlyList<RecognizedTextLine>> _resolver;

    public FakeTextRecognizer(Func<NormalizedRegion, IReadOnlyList<RecognizedTextLine>> resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Convenience factory for the default <see cref="HeaderAnalysisProfile"/>
    /// layout, where the CP region sits above the name/species region.
    /// </summary>
    public static FakeTextRecognizer WithCpAndName(string? cpText, string? nameText) =>
        new(roi => roi.Y < 0.2 ? LineFor(cpText) : LineFor(nameText));

    public Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(
        byte[] framePng,
        NormalizedRegion roi,
        CancellationToken cancellationToken = default,
        HeaderRegionKind regionKind = HeaderRegionKind.Name) =>
        Task.FromResult(_resolver(roi));

    private static IReadOnlyList<RecognizedTextLine> LineFor(string? text) =>
        string.IsNullOrEmpty(text)
            ? Array.Empty<RecognizedTextLine>()
            : new[]
            {
                new RecognizedTextLine
                {
                    Text = text,
                    Confidence = 0.9,
                    NormalizedBounds = new NormalizedRegion { X = 0.1, Y = 0.1, Width = 0.5, Height = 0.05 }
                }
            };
}
