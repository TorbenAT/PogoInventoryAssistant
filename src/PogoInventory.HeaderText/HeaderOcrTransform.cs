using PogoInventory.Vision.Models;

namespace PogoInventory.HeaderText;

/// <summary>Pixel-space BitmapTransform inputs, computed without touching WinRT.</summary>
public readonly record struct HeaderOcrTransform(
    uint BoundsX,
    uint BoundsY,
    uint BoundsWidth,
    uint BoundsHeight,
    uint ScaledWidth,
    uint ScaledHeight,
    uint OutputWidth,
    uint OutputHeight);

/// <summary>
/// Pure geometry for Windows.Graphics.Imaging.BitmapTransform, kept free of the
/// WinRT-only PogoInventory.HeaderOcr project so it is directly testable.
/// BitmapTransform scales the whole decoded image to ScaledWidth/ScaledHeight
/// first, then crops Bounds in that scaled coordinate space — so Bounds must be
/// the pixel ROI scaled by `upscale`, not the original pixel ROI.
/// </summary>
public static class HeaderOcrGeometry
{
    public static HeaderOcrTransform ComputeTransform(
        int imageWidth,
        int imageHeight,
        PixelRectangle pixelRoi,
        int upscale)
    {
        var scaledWidth = (uint)Math.Max(1, imageWidth * upscale);
        var scaledHeight = (uint)Math.Max(1, imageHeight * upscale);

        var boundsX = (uint)Math.Max(0, pixelRoi.X * upscale);
        var boundsY = (uint)Math.Max(0, pixelRoi.Y * upscale);
        var boundsWidth = (uint)Math.Max(1, pixelRoi.Width * upscale);
        var boundsHeight = (uint)Math.Max(1, pixelRoi.Height * upscale);

        // Clamp for rounding: the scaled crop must never fall outside the
        // scaled image.
        if (boundsX >= scaledWidth) boundsX = scaledWidth - 1;
        if (boundsY >= scaledHeight) boundsY = scaledHeight - 1;
        if (boundsX + boundsWidth > scaledWidth) boundsWidth = scaledWidth - boundsX;
        if (boundsY + boundsHeight > scaledHeight) boundsHeight = scaledHeight - boundsY;

        return new HeaderOcrTransform(
            boundsX,
            boundsY,
            boundsWidth,
            boundsHeight,
            scaledWidth,
            scaledHeight,
            boundsWidth,
            boundsHeight);
    }

    /// <summary>
    /// Game header text is small; upscale small crops with a smoother
    /// interpolation mode so the OCR engine has more pixels to work with.
    /// The CP region (~164px tall at 1080x2340) previously fell through to 1x
    /// -- the 60px cutoff was tuned for the old, narrower default ROIs, not
    /// the current wider spike-tuned ones -- so the cutoff for 2x now covers
    /// crops up to 220px on the smallest side (spike-measured: raises real CP
    /// reads from 50/60 to 53/60 with no species regression). A CP-specific
    /// 3x escalation and CP-only Otsu binarization were both tried and
    /// measured worse against the real 60-frame set (see HEADER_OCR.md), so
    /// this stays a single, region-agnostic cutoff.
    /// </summary>
    public static int ComputeUpscale(int width, int height)
    {
        var smallest = Math.Min(width, height);
        if (smallest <= 0) return 1;
        if (smallest < 15) return 4;
        if (smallest < 30) return 3;
        if (smallest < 220) return 2;
        return 1;
    }
}
