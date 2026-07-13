using System.Buffers.Binary;
using System.IO.Compression;
using PogoInventory.Vision.Errors;

namespace PogoInventory.Vision.Imaging;

public static class PngDecoder
{
    private static readonly byte[] Signature =
        { 137, 80, 78, 71, 13, 10, 26, 10 };

    private const int MaximumDimension = 16_384;
    private const long MaximumDecodedBytes = 512L * 1024L * 1024L;

    public static PixelImage Decode(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        return Decode((ReadOnlySpan<byte>)pngBytes);
    }

    public static PixelImage Decode(ReadOnlySpan<byte> pngBytes)
    {
        if (pngBytes.Length < Signature.Length ||
            !pngBytes[..Signature.Length].SequenceEqual(Signature))
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidPng,
                "The image does not contain a valid PNG signature.");
        }

        var offset = Signature.Length;
        var idat = new MemoryStream();
        int? width = null;
        int? height = null;
        byte? bitDepth = null;
        byte? colorType = null;
        byte? interlaceMethod = null;
        var sawIend = false;

        while (offset < pngBytes.Length)
        {
            if (pngBytes.Length - offset < 12)
            {
                throw Invalid("The PNG ended inside a chunk header.");
            }

            var chunkLengthValue = BinaryPrimitives.ReadUInt32BigEndian(
                pngBytes.Slice(offset, 4));
            if (chunkLengthValue > int.MaxValue)
            {
                throw Invalid("A PNG chunk was too large.");
            }

            var chunkLength = (int)chunkLengthValue;
            offset += 4;

            var chunkType = pngBytes.Slice(offset, 4);
            offset += 4;

            if ((long)pngBytes.Length - offset < (long)chunkLength + 4)
            {
                throw Invalid("The PNG ended inside a chunk body.");
            }

            var chunkData = pngBytes.Slice(offset, chunkLength);
            offset += chunkLength;
            offset += 4; // CRC. Signature, dimensions and decompressed length are validated separately.

            if (chunkType.SequenceEqual("IHDR"u8))
            {
                if (width is not null || chunkLength != 13)
                {
                    throw Invalid("The PNG contains an invalid IHDR chunk.");
                }

                var parsedWidth = BinaryPrimitives.ReadUInt32BigEndian(chunkData[..4]);
                var parsedHeight = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(4, 4));
                if (parsedWidth == 0 || parsedHeight == 0 ||
                    parsedWidth > MaximumDimension || parsedHeight > MaximumDimension)
                {
                    throw Unsupported(
                        $"PNG dimensions must be between 1 and {MaximumDimension} pixels.");
                }

                width = checked((int)parsedWidth);
                height = checked((int)parsedHeight);
                bitDepth = chunkData[8];
                colorType = chunkData[9];

                if (chunkData[10] != 0 || chunkData[11] != 0)
                {
                    throw Unsupported("Only standard PNG compression and filtering are supported.");
                }

                interlaceMethod = chunkData[12];
            }
            else if (chunkType.SequenceEqual("IDAT"u8))
            {
                idat.Write(chunkData);
            }
            else if (chunkType.SequenceEqual("IEND"u8))
            {
                sawIend = true;
                break;
            }
        }

        if (width is null || height is null || bitDepth is null || colorType is null)
        {
            throw Invalid("The PNG is missing its IHDR chunk.");
        }

        if (!sawIend)
        {
            throw Invalid("The PNG is missing its IEND chunk.");
        }

        if (bitDepth != 8)
        {
            throw Unsupported("Only 8-bit PNG screenshots are supported.");
        }

        if (interlaceMethod != 0)
        {
            throw Unsupported("Interlaced PNG images are not supported.");
        }

        var bytesPerPixel = colorType switch
        {
            0 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => throw Unsupported($"PNG color type {colorType} is not supported.")
        };

        var stride = checked(width.Value * bytesPerPixel);
        var expectedInflatedLength = checked((long)(stride + 1) * height.Value);
        if (expectedInflatedLength > MaximumDecodedBytes)
        {
            throw Unsupported("The decoded PNG would exceed the safety limit.");
        }

        byte[] inflated;
        try
        {
            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true);
            using var output = new MemoryStream((int)expectedInflatedLength);
            var buffer = new byte[81_920];
            long total = 0;

            while (true)
            {
                var read = zlib.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > expectedInflatedLength)
                {
                    throw Invalid("The decompressed PNG exceeded its expected size.");
                }

                output.Write(buffer, 0, read);
            }

            inflated = output.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidPng,
                "The PNG image data could not be decompressed.",
                exception);
        }

        if (inflated.LongLength != expectedInflatedLength)
        {
            throw Invalid(
                $"The decompressed PNG length was {inflated.LongLength}, expected {expectedInflatedLength}.");
        }

        var unfiltered = Unfilter(
            inflated,
            width.Value,
            height.Value,
            bytesPerPixel,
            stride);
        var rgba = ConvertToRgba(
            unfiltered,
            width.Value,
            height.Value,
            colorType.Value,
            bytesPerPixel);

        return new PixelImage(width.Value, height.Value, rgba);
    }

    private static byte[] Unfilter(
        IReadOnlyList<byte> inflated,
        int width,
        int height,
        int bytesPerPixel,
        int stride)
    {
        _ = width;
        var output = new byte[checked(stride * height)];
        var inputOffset = 0;

        for (var row = 0; row < height; row++)
        {
            var filter = inflated[inputOffset++];
            var rowOffset = row * stride;
            var previousRowOffset = (row - 1) * stride;

            for (var column = 0; column < stride; column++)
            {
                var raw = inflated[inputOffset++];
                var left = column >= bytesPerPixel
                    ? output[rowOffset + column - bytesPerPixel]
                    : 0;
                var up = row > 0
                    ? output[previousRowOffset + column]
                    : 0;
                var upLeft = row > 0 && column >= bytesPerPixel
                    ? output[previousRowOffset + column - bytesPerPixel]
                    : 0;

                output[rowOffset + column] = filter switch
                {
                    0 => raw,
                    1 => unchecked((byte)(raw + left)),
                    2 => unchecked((byte)(raw + up)),
                    3 => unchecked((byte)(raw + ((left + up) / 2))),
                    4 => unchecked((byte)(raw + Paeth(left, up, upLeft))),
                    _ => throw Invalid($"PNG filter type {filter} is invalid.")
                };
            }
        }

        return output;
    }

    private static byte[] ConvertToRgba(
        IReadOnlyList<byte> source,
        int width,
        int height,
        byte colorType,
        int bytesPerPixel)
    {
        var rgba = new byte[checked(width * height * 4)];
        var pixels = checked(width * height);

        for (var pixel = 0; pixel < pixels; pixel++)
        {
            var sourceOffset = pixel * bytesPerPixel;
            var targetOffset = pixel * 4;

            switch (colorType)
            {
                case 0:
                {
                    var gray = source[sourceOffset];
                    rgba[targetOffset] = gray;
                    rgba[targetOffset + 1] = gray;
                    rgba[targetOffset + 2] = gray;
                    rgba[targetOffset + 3] = 255;
                    break;
                }
                case 2:
                    rgba[targetOffset] = source[sourceOffset];
                    rgba[targetOffset + 1] = source[sourceOffset + 1];
                    rgba[targetOffset + 2] = source[sourceOffset + 2];
                    rgba[targetOffset + 3] = 255;
                    break;
                case 4:
                {
                    var gray = source[sourceOffset];
                    rgba[targetOffset] = gray;
                    rgba[targetOffset + 1] = gray;
                    rgba[targetOffset + 2] = gray;
                    rgba[targetOffset + 3] = source[sourceOffset + 1];
                    break;
                }
                case 6:
                    rgba[targetOffset] = source[sourceOffset];
                    rgba[targetOffset + 1] = source[sourceOffset + 1];
                    rgba[targetOffset + 2] = source[sourceOffset + 2];
                    rgba[targetOffset + 3] = source[sourceOffset + 3];
                    break;
                default:
                    throw Unsupported($"PNG color type {colorType} is not supported.");
            }
        }

        return rgba;
    }

    private static byte Paeth(byte left, byte up, byte upLeft)
    {
        var prediction = left + up - upLeft;
        var distanceLeft = Math.Abs(prediction - left);
        var distanceUp = Math.Abs(prediction - up);
        var distanceUpLeft = Math.Abs(prediction - upLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return left;
        }

        return distanceUp <= distanceUpLeft ? up : upLeft;
    }

    private static ScreenVisionException Invalid(string message) =>
        new(VisionErrorCode.InvalidPng, message);

    private static ScreenVisionException Unsupported(string message) =>
        new(VisionErrorCode.UnsupportedPng, message);
}
