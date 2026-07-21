using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.SelfTest;

internal static class CanonicalCloseLocatorTests
{
    public static Task RunAsync()
    {
        var locator = new VisualControlLocator();
        var positive = PngEncoder.Encode(CreateImage(drawShell: true, drawTopClear: false));
        var located = locator.LocateCanonicalCloseControl(positive);
        Assert(located is not null,
            $"canonical lower-centre close shell is located ({located?.Confidence:F3})");
        Assert(located!.Confidence >= 0.70,
            $"canonical close confidence is bounded ({located.Confidence:F3}, circle {located.CircularControlConfidence:F3}, x {located.XStrokeConfidence:F3})");
        Assert(located.CircularControlConfidence >= 0.70,
            "canonical close has circular shell evidence");
        Assert(located.XStrokeConfidence >= 0.70,
            "canonical close has crossing X-stroke evidence");
        Assert(located.Target.X is > 0.45 and < 0.55 && located.Target.Y is > 0.78 and < 0.88,
            "canonical close target is in lower-centre safe zone");
        Assert(located.Bounds.Width > 0 && located.Bounds.Height > 0,
            "canonical close includes bounds");
        Assert(located.ScreenshotSha256.Length == 64,
            "canonical close includes screenshot hash");

        Assert(locator.LocateCanonicalCloseControl(
                PngEncoder.Encode(CreateImage(drawShell: false, drawTopClear: false))) is null,
            "arbitrary crossed lines without shell are rejected");
        Assert(locator.LocateCanonicalCloseControl(
                PngEncoder.Encode(CreateImage(drawShell: false, drawTopClear: true))) is null,
            "search/text clear-X in the top field is rejected");
        return Task.CompletedTask;
    }

    private static PixelImage CreateImage(bool drawShell, bool drawTopClear)
    {
        const int width = 240;
        const int height = 400;
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < rgba.Length; index += 4)
        {
            rgba[index] = 32;
            rgba[index + 1] = 42;
            rgba[index + 2] = 52;
            rgba[index + 3] = 255;
        }

        if (drawShell)
            DrawCanonical(rgba, width, height, 120, 330, 13);
        else
            DrawCross(rgba, width, height, 120, 330, 13, (190, 235, 205));
        if (drawTopClear)
            DrawCross(rgba, width, height, 210, 70, 9, (210, 230, 220));
        return new PixelImage(width, height, rgba);
    }

    private static void DrawCanonical(byte[] rgba, int width, int height, int cx, int cy, int radius)
    {
        for (var y = cy - radius - 2; y <= cy + radius + 2; y++)
        for (var x = cx - radius - 2; x <= cx + radius + 2; x++)
        {
            var distance = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));
            if (distance is >= 11 and <= 15)
                Set(rgba, width, height, x, y, (0, 165, 175));
        }
        DrawCross(rgba, width, height, cx, cy, 9, (190, 235, 205));
    }

    private static void DrawCross(byte[] rgba, int width, int height, int cx, int cy, int radius,
        (byte R, byte G, byte B) color)
    {
        for (var offset = -radius; offset <= radius; offset++)
        {
            Set(rgba, width, height, cx + offset, cy + offset, color);
            Set(rgba, width, height, cx + offset, cy + offset + 1, color);
            Set(rgba, width, height, cx + offset, cy - offset, color);
            Set(rgba, width, height, cx + offset, cy - offset + 1, color);
        }
    }

    private static void Set(byte[] rgba, int width, int height, int x, int y,
        (byte R, byte G, byte B) color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        var offset = (y * width + x) * 4;
        rgba[offset] = color.R;
        rgba[offset + 1] = color.G;
        rgba[offset + 2] = color.B;
        rgba[offset + 3] = 255;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
