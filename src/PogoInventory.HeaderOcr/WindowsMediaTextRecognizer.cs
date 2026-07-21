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
        CancellationToken cancellationToken = default)
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

        var upscale = ComputeUpscale(pixelRoi.Width, pixelRoi.Height);
        var scaledWidth = (uint)Math.Max(1, pixelRoi.Width * upscale);
        var scaledHeight = (uint)Math.Max(1, pixelRoi.Height * upscale);

        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds
            {
                X = (uint)pixelRoi.X,
                Y = (uint)pixelRoi.Y,
                Width = (uint)pixelRoi.Width,
                Height = (uint)pixelRoi.Height
            },
            ScaledWidth = scaledWidth,
            ScaledHeight = scaledHeight,
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
                X = Math.Clamp(roi.X + left / scaledWidth * roi.Width, 0, 1),
                Y = Math.Clamp(roi.Y + top / scaledHeight * roi.Height, 0, 1),
                Width = Math.Max(0.0001, (right - left) / scaledWidth * roi.Width),
                Height = Math.Max(0.0001, (bottom - top) / scaledHeight * roi.Height)
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

    /// <summary>
    /// Game header text is small; upscale small crops 2-4x with a smoother
    /// interpolation mode so the OCR engine has more pixels to work with.
    /// </summary>
    private static int ComputeUpscale(int width, int height)
    {
        var smallest = Math.Min(width, height);
        if (smallest <= 0) return 1;
        if (smallest < 15) return 4;
        if (smallest < 30) return 3;
        if (smallest < 60) return 2;
        return 1;
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
