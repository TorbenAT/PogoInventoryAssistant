using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.CropAtlas.Services;

internal static class PixelImageTransforms
{
    public static PixelImage Crop(
        PixelImage source,
        PixelRectangle rectangle)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (rectangle.X < 0 ||
            rectangle.Y < 0 ||
            rectangle.Width <= 0 ||
            rectangle.Height <= 0 ||
            rectangle.X + rectangle.Width > source.Width ||
            rectangle.Y + rectangle.Height > source.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rectangle),
                "Crop rectangle must remain inside the source image.");
        }

        var rgba = new byte[
            checked(rectangle.Width * rectangle.Height * 4)];
        var targetOffset = 0;
        for (var y = 0; y < rectangle.Height; y++)
        {
            for (var x = 0; x < rectangle.Width; x++)
            {
                var pixel = source.GetPixel(
                    rectangle.X + x,
                    rectangle.Y + y);
                rgba[targetOffset++] = pixel.R;
                rgba[targetOffset++] = pixel.G;
                rgba[targetOffset++] = pixel.B;
                rgba[targetOffset++] = pixel.A;
            }
        }

        return new PixelImage(
            rectangle.Width,
            rectangle.Height,
            rgba);
    }

    public static PixelImage ResizeToFit(
        PixelImage source,
        int maximumWidth,
        int maximumHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maximumWidth <= 0 || maximumHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumWidth),
                "Maximum dimensions must be positive.");
        }

        var scale = Math.Min(
            1,
            Math.Min(
                maximumWidth / (double)source.Width,
                maximumHeight / (double)source.Height));
        if (scale >= 1)
        {
            return source;
        }

        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var rgba = new byte[checked(width * height * 4)];
        var offset = 0;

        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(
                source.Height - 1,
                (int)Math.Floor(y / scale));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(
                    source.Width - 1,
                    (int)Math.Floor(x / scale));
                var pixel = source.GetPixel(sourceX, sourceY);
                rgba[offset++] = pixel.R;
                rgba[offset++] = pixel.G;
                rgba[offset++] = pixel.B;
                rgba[offset++] = pixel.A;
            }
        }

        return new PixelImage(width, height, rgba);
    }

    public static PixelImage ComposeGrid(
        IReadOnlyList<PixelImage?> tiles,
        int columns,
        int rows,
        int padding = 8,
        int border = 2)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (columns <= 0 || rows <= 0 ||
            tiles.Count != checked(columns * rows) ||
            padding < 0 ||
            border < 0)
        {
            throw new ArgumentException(
                "Grid dimensions, tile count, padding and border are invalid.");
        }

        var present = tiles.Where(tile => tile is not null).Cast<PixelImage>().ToArray();
        if (present.Length == 0)
        {
            throw new InvalidOperationException(
                "A contact sheet requires at least one image.");
        }

        var tileWidth = present.Max(tile => tile.Width);
        var tileHeight = present.Max(tile => tile.Height);
        var width = checked(
            columns * tileWidth +
            (columns + 1) * padding +
            columns * border * 2);
        var height = checked(
            rows * tileHeight +
            (rows + 1) * padding +
            rows * border * 2);
        var rgba = Enumerable.Repeat((byte)255, checked(width * height * 4))
            .ToArray();

        for (var index = 0; index < tiles.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellX = padding + column * (tileWidth + border * 2 + padding);
            var cellY = padding + row * (tileHeight + border * 2 + padding);
            DrawBorder(
                rgba,
                width,
                height,
                cellX,
                cellY,
                tileWidth + border * 2,
                tileHeight + border * 2,
                border);

            var tile = tiles[index];
            if (tile is null)
            {
                continue;
            }

            var x = cellX + border + (tileWidth - tile.Width) / 2;
            var y = cellY + border + (tileHeight - tile.Height) / 2;
            Blit(rgba, width, height, tile, x, y);
        }

        return new PixelImage(width, height, rgba);
    }

    private static void DrawBorder(
        byte[] target,
        int targetWidth,
        int targetHeight,
        int x,
        int y,
        int width,
        int height,
        int border)
    {
        if (border == 0)
        {
            return;
        }

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                if (row >= border &&
                    row < height - border &&
                    column >= border &&
                    column < width - border)
                {
                    continue;
                }

                SetPixel(
                    target,
                    targetWidth,
                    targetHeight,
                    x + column,
                    y + row,
                    64,
                    64,
                    64,
                    255);
            }
        }
    }

    private static void Blit(
        byte[] target,
        int targetWidth,
        int targetHeight,
        PixelImage source,
        int x,
        int y)
    {
        for (var sourceY = 0; sourceY < source.Height; sourceY++)
        {
            for (var sourceX = 0; sourceX < source.Width; sourceX++)
            {
                var pixel = source.GetPixel(sourceX, sourceY);
                SetPixel(
                    target,
                    targetWidth,
                    targetHeight,
                    x + sourceX,
                    y + sourceY,
                    pixel.R,
                    pixel.G,
                    pixel.B,
                    pixel.A);
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
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return;
        }

        var offset = checked((y * width + x) * 4);
        target[offset] = red;
        target[offset + 1] = green;
        target[offset + 2] = blue;
        target[offset + 3] = alpha;
    }
}
