using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Models;

namespace PogoInventory.Vision.Imaging;

public static class FingerprintExtractor
{
    public static byte[] Extract(
        PixelImage image,
        NormalizedRegion region,
        FingerprintMode mode,
        int fingerprintWidth,
        int fingerprintHeight)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(region);
        region.Validate();

        if (fingerprintWidth is < 2 or > 64)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidFingerprint,
                "Fingerprint width must be between 2 and 64.");
        }

        if (fingerprintHeight is < 2 or > 64)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidFingerprint,
                "Fingerprint height must be between 2 and 64.");
        }

        var rectangle = region.ToPixels(image.Width, image.Height);
        return mode switch
        {
            FingerprintMode.Color => ExtractColor(
                image,
                rectangle,
                fingerprintWidth,
                fingerprintHeight),
            FingerprintMode.Grayscale => ExtractGrayscale(
                image,
                rectangle,
                fingerprintWidth,
                fingerprintHeight),
            FingerprintMode.Edge => ExtractEdge(
                image,
                rectangle,
                fingerprintWidth,
                fingerprintHeight),
            _ => throw new ScreenVisionException(
                VisionErrorCode.InvalidFingerprint,
                $"Fingerprint mode {mode} is not supported.")
        };
    }

    public static int ExpectedLength(
        FingerprintMode mode,
        int fingerprintWidth,
        int fingerprintHeight) =>
        checked(
            fingerprintWidth * fingerprintHeight *
            (mode == FingerprintMode.Color ? 3 : 1));

    private static byte[] ExtractColor(
        PixelImage image,
        PixelRectangle rectangle,
        int outputWidth,
        int outputHeight)
    {
        var result = new byte[checked(outputWidth * outputHeight * 3)];
        var outputOffset = 0;

        ForEachCell(
            rectangle,
            outputWidth,
            outputHeight,
            (left, top, right, bottom) =>
            {
                var red = 0L;
                var green = 0L;
                var blue = 0L;
                var count = 0L;

                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        red += pixel.R;
                        green += pixel.G;
                        blue += pixel.B;
                        count++;
                    }
                }

                result[outputOffset++] = (byte)(red / count);
                result[outputOffset++] = (byte)(green / count);
                result[outputOffset++] = (byte)(blue / count);
            });

        return result;
    }

    private static byte[] ExtractGrayscale(
        PixelImage image,
        PixelRectangle rectangle,
        int outputWidth,
        int outputHeight)
    {
        var result = new byte[checked(outputWidth * outputHeight)];
        var outputOffset = 0;

        ForEachCell(
            rectangle,
            outputWidth,
            outputHeight,
            (left, top, right, bottom) =>
            {
                var gray = 0L;
                var count = 0L;

                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        gray += ToGray(pixel);
                        count++;
                    }
                }

                result[outputOffset++] = (byte)(gray / count);
            });

        return result;
    }

    private static byte[] ExtractEdge(
        PixelImage image,
        PixelRectangle rectangle,
        int outputWidth,
        int outputHeight)
    {
        var grayscale = ExtractGrayscale(
            image,
            rectangle,
            outputWidth,
            outputHeight);
        var edges = new byte[grayscale.Length];

        for (var y = 0; y < outputHeight; y++)
        {
            for (var x = 0; x < outputWidth; x++)
            {
                var index = y * outputWidth + x;
                var current = grayscale[index];
                var left = x > 0 ? grayscale[index - 1] : current;
                var up = y > 0 ? grayscale[index - outputWidth] : current;
                var magnitude = (Math.Abs(current - left) + Math.Abs(current - up)) / 2;
                edges[index] = (byte)magnitude;
            }
        }

        return edges;
    }

    private static void ForEachCell(
        PixelRectangle rectangle,
        int outputWidth,
        int outputHeight,
        Action<int, int, int, int> action)
    {
        for (var outputY = 0; outputY < outputHeight; outputY++)
        {
            var top = rectangle.Y +
                (int)Math.Floor((double)outputY * rectangle.Height / outputHeight);
            var bottom = rectangle.Y +
                (int)Math.Ceiling((double)(outputY + 1) * rectangle.Height / outputHeight);
            bottom = Math.Max(bottom, top + 1);
            bottom = Math.Min(bottom, rectangle.Y + rectangle.Height);

            for (var outputX = 0; outputX < outputWidth; outputX++)
            {
                var left = rectangle.X +
                    (int)Math.Floor((double)outputX * rectangle.Width / outputWidth);
                var right = rectangle.X +
                    (int)Math.Ceiling((double)(outputX + 1) * rectangle.Width / outputWidth);
                right = Math.Max(right, left + 1);
                right = Math.Min(right, rectangle.X + rectangle.Width);

                action(left, top, right, bottom);
            }
        }
    }

    private static int ToGray(Rgba32 pixel) =>
        (77 * pixel.R + 150 * pixel.G + 29 * pixel.B + 128) >> 8;
}
