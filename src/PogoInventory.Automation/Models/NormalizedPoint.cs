namespace PogoInventory.Automation.Models;

public sealed record NormalizedPoint
{
    public required double X { get; init; }
    public required double Y { get; init; }

    public void Validate(string name)
    {
        if (!double.IsFinite(X) || X is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "Normalised X must be between 0 and 1.");
        }

        if (!double.IsFinite(Y) || Y is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "Normalised Y must be between 0 and 1.");
        }
    }

    public (int X, int Y) ToPixels(int width, int height)
    {
        Validate(nameof(NormalizedPoint));
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Screen dimensions must be positive.");
        }

        var x = Math.Clamp((int)Math.Round(X * (width - 1)), 0, width - 1);
        var y = Math.Clamp((int)Math.Round(Y * (height - 1)), 0, height - 1);
        return (x, y);
    }
}
