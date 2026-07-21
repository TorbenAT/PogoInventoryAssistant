using PogoInventory.HeaderText;
using PogoInventory.Vision.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PogoInventory.HeaderOcr;

/// <summary>
/// <see cref="ITextRecognizer"/> implementation backed by the Windows.Media.Ocr
/// WinRT API. Decodes PNG bytes, crops to the requested normalized ROI and
/// upscales small crops (game header text is small) before recognition.
/// <paramref name="regionKind"/> (see <see cref="RecognizeAsync"/>) is plumbed
/// through for CP-specific preprocessing; a CP-only Otsu binarization pass
/// (<see cref="HeaderOcrBinarization"/>) was measured against the real
/// 60-frame spike set and made CP reads worse, not better, so it is not
/// wired in here -- see docs/HEADER_OCR.md for the numbers.
/// </summary>
public sealed class WindowsMediaTextRecognizer : ITextRecognizer
{
    private readonly OcrEngine _engine;

    public WindowsMediaTextRecognizer(string? languageTag = null)
    {
        _engine = CreateEngine(languageTag) ?? throw new InvalidOperationException(
            "No OCR engine is available for the requested language or the user's profile languages. " +
            "Install a Windows OCR language pack (Settings > Time & Language > Language & region).");
    }

    /// <summary>Returns whether an OCR engine can be created without throwing.</summary>
    public static bool IsSupported(string? languageTag = null) => CreateEngine(languageTag) is not null;

    public async Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(
        byte[] framePng,
        NormalizedRegion roi,
        CancellationToken cancellationToken = default,
        HeaderRegionKind regionKind = HeaderRegionKind.Name)
    {
        ArgumentNullException.ThrowIfNull(framePng);
        roi.Validate(nameof(roi));
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(framePng);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixelRoi = roi.ToPixels((int)decoder.PixelWidth, (int)decoder.PixelHeight);

        var upscale = HeaderOcrGeometry.ComputeUpscale(pixelRoi.Width, pixelRoi.Height);
        var headerTransform = HeaderOcrGeometry.ComputeTransform(
            (int)decoder.PixelWidth,
            (int)decoder.PixelHeight,
            pixelRoi,
            upscale);

        // BitmapTransform scales first, then crops Bounds in the SCALED coordinate
        // space, so Bounds must be the ROI scaled by `upscale`, not the original
        // pixel ROI.
        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds
            {
                X = headerTransform.BoundsX,
                Y = headerTransform.BoundsY,
                Width = headerTransform.BoundsWidth,
                Height = headerTransform.BoundsHeight
            },
            ScaledWidth = headerTransform.ScaledWidth,
            ScaledHeight = headerTransform.ScaledHeight,
            InterpolationMode = upscale > 1
                ? BitmapInterpolationMode.Fant
                : BitmapInterpolationMode.NearestNeighbor
        };

        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        // The returned bitmap is the cropped ROI at `upscale`, sized
        // BoundsWidth x BoundsHeight — not ScaledWidth/ScaledHeight (that's the
        // full scaled image before cropping).
        var outputWidth = (double)headerTransform.OutputWidth;
        var outputHeight = (double)headerTransform.OutputHeight;

        var ocrResult = await _engine.RecognizeAsync(softwareBitmap);

        var lines = new List<RecognizedTextLine>();
        foreach (var line in ocrResult.Lines)
        {
            if (line.Words.Count == 0) continue;

            var left = line.Words.Min(word => word.BoundingRect.Left);
            var top = line.Words.Min(word => word.BoundingRect.Top);
            var right = line.Words.Max(word => word.BoundingRect.Right);
            var bottom = line.Words.Max(word => word.BoundingRect.Bottom);

            var normalizedBounds = new NormalizedRegion
            {
                X = Math.Clamp(roi.X + left / outputWidth * roi.Width, 0, 1),
                Y = Math.Clamp(roi.Y + top / outputHeight * roi.Height, 0, 1),
                Width = Math.Max(0.0001, (right - left) / outputWidth * roi.Width),
                Height = Math.Max(0.0001, (bottom - top) / outputHeight * roi.Height)
            };

            lines.Add(new RecognizedTextLine
            {
                Text = line.Text,
                Confidence = null,
                NormalizedBounds = normalizedBounds
            });
        }

        return lines;
    }

    private static OcrEngine? CreateEngine(string? languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            var requested = OcrEngine.TryCreateFromLanguage(new Language(languageTag));
            if (requested is not null) return requested;
        }

        var english = OcrEngine.TryCreateFromLanguage(new Language("en"));
        return english ?? OcrEngine.TryCreateFromUserProfileLanguages();
    }
}
