using System.Buffers.Binary;
using System.IO.Compression;

namespace PogoInventory.Vision.Imaging;

public static class PngEncoder
{
    private static readonly byte[] Signature =
        { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] Encode(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(
            header[..4],
            checked((uint)image.Width));
        BinaryPrimitives.WriteUInt32BigEndian(
            header.Slice(4, 4),
            checked((uint)image.Height));
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(
                   compressed,
                   CompressionLevel.Optimal,
                   leaveOpen: true))
        {
            var source = image.RgbaBytes;
            var stride = checked(image.Width * 4);
            for (var row = 0; row < image.Height; row++)
            {
                zlib.WriteByte(0);
                zlib.Write(source.Slice(row * stride, stride));
            }
        }

        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(
        Stream output,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        if (type.Length != 4)
        {
            throw new ArgumentException(
                "PNG chunk types must contain four bytes.",
                nameof(type));
        }

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(
            length,
            checked((uint)data.Length));
        output.Write(length);
        output.Write(type);
        output.Write(data);

        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        crc ^= 0xffffffffu;

        Span<byte> encodedCrc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(encodedCrc, crc);
        output.Write(encodedCrc);
    }

    private static uint UpdateCrc(
        uint crc,
        ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (uint)-(int)(crc & 1);
                crc = (crc >> 1) ^ (0xedb88320u & mask);
            }
        }

        return crc;
    }
}
