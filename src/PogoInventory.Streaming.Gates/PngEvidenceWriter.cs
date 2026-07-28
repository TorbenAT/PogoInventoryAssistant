using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public static class PngEvidenceWriter
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static async Task WriteAsync(string path, IFrameLease frame, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(frame);
        var descriptor = frame.Metadata.Descriptor;
        if (descriptor.PixelFormat != FramePixelFormat.Bgra32)
        {
            throw new NotSupportedException("Evidence PNG export currently supports BGRA32 only.");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = File.Create(path);
        await output.WriteAsync(Signature, cancellationToken).ConfigureAwait(false);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), descriptor.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), descriptor.Height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        await WriteChunkAsync(output, "IHDR", header, cancellationToken).ConfigureAwait(false);

        await using var compressed = new MemoryStream();
        await using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var source = frame.Pixels;
            var scanline = new byte[checked(1 + descriptor.Width * 4)];
            for (var y = 0; y < descriptor.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FillScanline(source, descriptor, y, scanline);
                await zlib.WriteAsync(scanline, cancellationToken).ConfigureAwait(false);
            }
        }

        await WriteChunkAsync(output, "IDAT", compressed.ToArray(), cancellationToken).ConfigureAwait(false);
        await WriteChunkAsync(output, "IEND", Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
    }

    private static void FillScanline(
        ReadOnlyMemory<byte> source,
        FrameDescriptor descriptor,
        int rowIndex,
        byte[] scanline)
    {
        scanline[0] = 0;
        var sourceRow = source.Span.Slice(rowIndex * descriptor.Stride, descriptor.Width * 4);
        for (var x = 0; x < descriptor.Width; x++)
        {
            var sourceOffset = x * 4;
            var destinationOffset = 1 + sourceOffset;
            scanline[destinationOffset] = sourceRow[sourceOffset + 2];
            scanline[destinationOffset + 1] = sourceRow[sourceOffset + 1];
            scanline[destinationOffset + 2] = sourceRow[sourceOffset];
            scanline[destinationOffset + 3] = sourceRow[sourceOffset + 3];
        }
    }

    private static async Task WriteChunkAsync(
        Stream output,
        string type,
        byte[] data,
        CancellationToken cancellationToken)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        await output.WriteAsync(length, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(typeBytes, cancellationToken).ConfigureAwait(false);
        if (data.Length > 0)
        {
            await output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32.Compute(crcInput));
        await output.WriteAsync(crc, cancellationToken).ConfigureAwait(false);
    }

    private static class Crc32
    {
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xffffffff;
            foreach (var value in data)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
                }
            }

            return ~crc;
        }
    }
}

public sealed record EvidenceExportResult(
    IReadOnlyList<string> Paths,
    IReadOnlyDictionary<long, IReadOnlyList<string>> RolesByFrameId);

public static class GateEvidenceExporter
{
    public static async Task<EvidenceExportResult> ExportAsync(
        SelectedFrameSet frames,
        string outputDirectory,
        int maximumFrames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (maximumFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));
        }

        Directory.CreateDirectory(outputDirectory);
        var grouped = frames.Frames.Values
            .GroupBy(x => x.FrameId.Value)
            .OrderBy(x => x.Key)
            .Take(maximumFrames)
            .ToArray();
        var paths = new List<string>();
        var roles = new Dictionary<long, IReadOnlyList<string>>();
        foreach (var group in grouped)
        {
            var roleNames = group.Select(x => x.Role.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var path = Path.Combine(outputDirectory, $"frame-{group.Key:D8}-{string.Join("_", roleNames)}.png");
            await PngEvidenceWriter.WriteAsync(path, group.First().Lease, cancellationToken).ConfigureAwait(false);
            paths.Add(path);
            roles[group.Key] = roleNames;
        }

        return new EvidenceExportResult(paths, roles);
    }
}
