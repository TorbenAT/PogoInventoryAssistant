using PogoInventory.Appraisal.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalImageDiagnostics
{
    public static PixelImage DrawOverlay(
        PixelImage source,
        AppraisalAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(analysis);
        analysis.Validate();

        var rgba = source.RgbaBytes.ToArray();
        foreach (var bar in analysis.Bars)
        {
            var rectangle = bar.Region.ToPixels(
                source.Width,
                source.Height);
            DrawRectangle(
                rgba,
                source.Width,
                source.Height,
                rectangle,
                bar.TrackDetected
                    ? (byte)0
                    : (byte)220,
                bar.TrackDetected
                    ? (byte)220
                    : (byte)0,
                40,
                255,
                3);

            if (bar.FillEndColumn >= 0)
            {
                var x = rectangle.X +
                    Math.Clamp(
                        bar.FillEndColumn,
                        0,
                        rectangle.Width - 1);
                DrawVerticalLine(
                    rgba,
                    source.Width,
                    source.Height,
                    x,
                    rectangle.Y,
                    rectangle.Y + rectangle.Height - 1,
                    255,
                    0,
                    255,
                    255,
                    3);
            }
        }

        return new PixelImage(source.Width, source.Height, rgba);
    }

    public static PixelImage Crop(
        PixelImage source,
        NormalizedRegion region)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(region);
        var rectangle = region.ToPixels(source.Width, source.Height);
        var rgba = new byte[
            checked(rectangle.Width * rectangle.Height * 4)];
        var target = 0;

        for (var y = 0; y < rectangle.Height; y++)
        {
            for (var x = 0; x < rectangle.Width; x++)
            {
                var pixel = source.GetPixel(
                    rectangle.X + x,
                    rectangle.Y + y);
                rgba[target++] = pixel.R;
                rgba[target++] = pixel.G;
                rgba[target++] = pixel.B;
                rgba[target++] = pixel.A;
            }
        }

        return new PixelImage(
            rectangle.Width,
            rectangle.Height,
            rgba);
    }

    private static void DrawRectangle(
        byte[] target,
        int width,
        int height,
        PixelRectangle rectangle,
        byte red,
        byte green,
        byte blue,
        byte alpha,
        int thickness)
    {
        for (var offset = 0; offset < thickness; offset++)
        {
            var left = rectangle.X + offset;
            var right = rectangle.X + rectangle.Width - 1 - offset;
            var top = rectangle.Y + offset;
            var bottom = rectangle.Y + rectangle.Height - 1 - offset;

            for (var x = left; x <= right; x++)
            {
                SetPixel(target, width, height, x, top, red, green, blue, alpha);
                SetPixel(target, width, height, x, bottom, red, green, blue, alpha);
            }

            for (var y = top; y <= bottom; y++)
            {
                SetPixel(target, width, height, left, y, red, green, blue, alpha);
                SetPixel(target, width, height, right, y, red, green, blue, alpha);
            }
        }
    }

    private static void DrawVerticalLine(
        byte[] target,
        int width,
        int height,
        int x,
        int top,
        int bottom,
        byte red,
        byte green,
        byte blue,
        byte alpha,
        int thickness)
    {
        var half = thickness / 2;
        for (var drawX = x - half; drawX <= x + half; drawX++)
        {
            for (var y = top; y <= bottom; y++)
            {
                SetPixel(
                    target,
                    width,
                    height,
                    drawX,
                    y,
                    red,
                    green,
                    blue,
                    alpha);
            }
        }
    }

    private static void SetPixel(
        byte[] target,
        int width,
        int height,
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        if ((uint)x >= (uint)width ||
            (uint)y >= (uint)height)
        {
            return;
        }

        var index = checked((y * width + x) * 4);
        target[index] = red;
        target[index + 1] = green;
        target[index + 2] = blue;
        target[index + 3] = alpha;
    }
}
