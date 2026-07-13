namespace PogoInventory.Vision.Imaging;

public sealed class PixelImage
{
    private readonly byte[] _rgba;

    public PixelImage(int width, int height, byte[] rgba)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ArgumentNullException.ThrowIfNull(rgba);

        var expectedLength = checked(width * height * 4);
        if (rgba.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} RGBA bytes, received {rgba.Length}.",
                nameof(rgba));
        }

        Width = width;
        Height = height;
        _rgba = rgba.ToArray();
    }

    public int Width { get; }
    public int Height { get; }

    public ReadOnlySpan<byte> RgbaBytes => _rgba;

    public Rgba32 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        var offset = checked((y * Width + x) * 4);
        return new Rgba32(
            _rgba[offset],
            _rgba[offset + 1],
            _rgba[offset + 2],
            _rgba[offset + 3]);
    }
}
