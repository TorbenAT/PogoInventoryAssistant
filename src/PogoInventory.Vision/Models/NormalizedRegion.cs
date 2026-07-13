using PogoInventory.Vision.Errors;

namespace PogoInventory.Vision.Models;

public sealed record NormalizedRegion
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public void Validate(string? name = null)
    {
        var label = string.IsNullOrWhiteSpace(name) ? "region" : $"region '{name}'";

        if (!double.IsFinite(X) ||
            !double.IsFinite(Y) ||
            !double.IsFinite(Width) ||
            !double.IsFinite(Height))
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidRegion,
                $"The {label} contains a non-finite value.");
        }

        if (X < 0 || Y < 0 || Width <= 0 || Height <= 0)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidRegion,
                $"The {label} must have non-negative coordinates and positive dimensions.");
        }

        const double epsilon = 0.0000001;
        if (X + Width > 1 + epsilon || Y + Height > 1 + epsilon)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidRegion,
                $"The {label} must remain inside the normalised screen bounds 0..1.");
        }
    }

    public PixelRectangle ToPixels(int imageWidth, int imageHeight)
    {
        Validate();

        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(imageWidth),
                "Image dimensions must be positive.");
        }

        var left = Math.Clamp((int)Math.Floor(X * imageWidth), 0, imageWidth - 1);
        var top = Math.Clamp((int)Math.Floor(Y * imageHeight), 0, imageHeight - 1);
        var right = Math.Clamp(
            (int)Math.Ceiling((X + Width) * imageWidth),
            left + 1,
            imageWidth);
        var bottom = Math.Clamp(
            (int)Math.Ceiling((Y + Height) * imageHeight),
            top + 1,
            imageHeight);

        return new PixelRectangle(left, top, right - left, bottom - top);
    }
}

public readonly record struct PixelRectangle(int X, int Y, int Width, int Height);
