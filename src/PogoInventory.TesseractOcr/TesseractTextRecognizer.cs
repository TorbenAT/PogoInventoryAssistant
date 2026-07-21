using PogoInventory.HeaderText;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using TesseractOCR;
using TesseractOCR.Enums;

namespace PogoInventory.TesseractOcr;

/// <summary>
/// <see cref="ITextRecognizer"/> backed by Tesseract 5 (the TesseractOCR
/// binding), measured as an alternative engine to
/// <c>WindowsMediaTextRecognizer</c> specifically because Windows.Media.Ocr
/// deterministically drops thin "1" digit strokes in CP text. Decodes PNG
/// with the repo's own <see cref="PngDecoder"/> (no System.Drawing), crops
/// with the shared <see cref="HeaderOcrGeometry"/> / <see cref="HeaderOcrCropScaler"/>
/// pure helpers used by the WinRT path too, re-encodes the crop as PNG for
/// Tesseract's Pix loader, and applies a per-region character whitelist (see
/// <see cref="TesseractOcrConfigSelector"/>) -- CP text is restricted to
/// "CPcp0123456789" so a thin "1" only competes against digit shapes.
/// </summary>
public sealed class TesseractTextRecognizer : ITextRecognizer, IDisposable
{
    private readonly Engine _engine;
    private readonly bool _binarizeCpRegion;

    /// <param name="binarizeCpRegion">
    /// Spike knob: apply the shared Otsu binarization (<see cref="HeaderOcrBinarization"/>,
    /// the same pass measured as a regression for the WinRT engine, see
    /// docs/HEADER_OCR.md) to the CP crop only, before handing it to
    /// Tesseract. Off by default; flip on to measure it against Tesseract
    /// specifically, since a different engine can react differently to the
    /// same preprocessing.
    /// </param>
    public TesseractTextRecognizer(string tessDataDirectory, string languageTag = "eng", bool binarizeCpRegion = false)
    {
        ArgumentNullException.ThrowIfNull(tessDataDirectory);
        _engine = new Engine(tessDataDirectory, LanguageFromTag(languageTag), EngineMode.Default);
        _binarizeCpRegion = binarizeCpRegion;
    }

    /// <summary>Returns whether an engine can be created from the given tessdata directory without throwing.</summary>
    public static bool IsSupported(string tessDataDirectory, string languageTag = "eng")
    {
        try
        {
            using var engine = new Engine(tessDataDirectory, LanguageFromTag(languageTag), EngineMode.Default);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(
        byte[] framePng,
        NormalizedRegion roi,
        CancellationToken cancellationToken = default,
        HeaderRegionKind regionKind = HeaderRegionKind.Name)
    {
        ArgumentNullException.ThrowIfNull(framePng);
        roi.Validate(nameof(roi));
        cancellationToken.ThrowIfCancellationRequested();

        var frame = PngDecoder.Decode(framePng);
        var pixelRoi = roi.ToPixels(frame.Width, frame.Height);
        var upscale = HeaderOcrGeometry.ComputeUpscale(pixelRoi.Width, pixelRoi.Height);
        var cropped = HeaderOcrCropScaler.CropAndUpscale(frame, pixelRoi, upscale);
        if (_binarizeCpRegion && regionKind == HeaderRegionKind.Cp)
        {
            cropped = Binarize(cropped);
        }
        var croppedPng = PngEncoder.Encode(cropped);

        var config = TesseractOcrConfigSelector.ConfigFor(regionKind);
        _engine.SetVariable("tessedit_char_whitelist", config.CharWhitelist);

        using var image = TesseractOCR.Pix.Image.LoadFromMemory(croppedPng);
        using var page = _engine.Process(image, config.SingleLine ? PageSegMode.SingleLine : PageSegMode.SparseText);

        var lines = new List<RecognizedTextLine>();
        foreach (var block in page.Layout)
        {
            foreach (var paragraph in block.Paragraphs)
            {
                foreach (var textLine in paragraph.TextLines)
                {
                    var text = textLine.Text?.Trim();
                    if (string.IsNullOrEmpty(text)) continue;

                    var bounds = textLine.BoundingBox;
                    var normalizedBounds = bounds is { } box
                        ? new NormalizedRegion
                        {
                            X = Math.Clamp(roi.X + box.X1 / (double)cropped.Width * roi.Width, 0, 1),
                            Y = Math.Clamp(roi.Y + box.Y1 / (double)cropped.Height * roi.Height, 0, 1),
                            Width = Math.Max(0.0001, (box.X2 - box.X1) / (double)cropped.Width * roi.Width),
                            Height = Math.Max(0.0001, (box.Y2 - box.Y1) / (double)cropped.Height * roi.Height)
                        }
                        : roi;

                    lines.Add(new RecognizedTextLine
                    {
                        Text = text,
                        Confidence = textLine.Confidence / 100.0,
                        NormalizedBounds = normalizedBounds
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<RecognizedTextLine>>(lines);
    }

    /// <summary>
    /// HeaderOcrBinarization.Luminance expects (b, g, r) byte order; our
    /// PixelImage stores RGBA, so channels are swapped going in. Once
    /// binarized every channel holds the same black/white value, so no swap
    /// back is needed.
    /// </summary>
    private static PixelImage Binarize(PixelImage image)
    {
        var bgra = image.RgbaBytes.ToArray();
        for (var i = 0; i < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        }
        var stride = image.Width * 4;
        var threshold = HeaderOcrBinarization.ComputeThreshold(bgra, image.Width, image.Height, stride);
        var binarized = HeaderOcrBinarization.Binarize(bgra, image.Width, image.Height, stride, threshold);
        return new PixelImage(image.Width, image.Height, binarized);
    }

    private static Language LanguageFromTag(string languageTag) => languageTag switch
    {
        "eng" or "en" => Language.English,
        _ => Language.English
    };

    public void Dispose() => _engine.Dispose();
}
