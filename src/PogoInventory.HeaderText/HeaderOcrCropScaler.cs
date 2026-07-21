using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>
/// Pure RGBA crop + nearest-neighbor upscale, kept engine-agnostic so both a
/// WinRT-based and a Tesseract-based <see cref="ITextRecognizer"/> can share
/// identical crop geometry (<see cref="HeaderOcrGeometry"/>) and get pixel-for
/// -pixel comparable inputs. Nearest-neighbor (not bilinear) is used
/// deliberately: it never blends a thin white digit stroke into the
/// surrounding background, which is exactly the failure this recognizer
/// exists to avoid.
/// </summary>
public static class HeaderOcrCropScaler
{
    public static PixelImage CropAndUpscale(PixelImage source, PixelRectangle roi, int upscale)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (upscale <= 0) throw new ArgumentOutOfRangeException(nameof(upscale));
        if (roi.Width <= 0 || roi.Height <= 0) throw new ArgumentOutOfRangeException(nameof(roi));
        if (roi.X < 0 || roi.Y < 0 ||
            roi.X + roi.Width > source.Width || roi.Y + roi.Height > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(roi), "ROI falls outside the source image.");
        }

        var outputWidth = roi.Width * upscale;
        var outputHeight = roi.Height * upscale;
        var output = new byte[checked(outputWidth * outputHeight * 4)];

        for (var y = 0; y < outputHeight; y++)
        {
            var sourceY = roi.Y + y / upscale;
            var rowOffset = y * outputWidth * 4;
            for (var x = 0; x < outputWidth; x++)
            {
                var sourceX = roi.X + x / upscale;
                var pixel = source.GetPixel(sourceX, sourceY);
                var offset = rowOffset + x * 4;
                output[offset] = pixel.R;
                output[offset + 1] = pixel.G;
                output[offset + 2] = pixel.B;
                output[offset + 3] = pixel.A;
            }
        }

        return new PixelImage(outputWidth, outputHeight, output);
    }
}
