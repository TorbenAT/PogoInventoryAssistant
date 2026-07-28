using System.Security.Cryptography;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Semantics;

public sealed record VisualFrame(
    FrameMetadata Metadata,
    ReadOnlyMemory<byte> Pixels,
    string EvidenceHash,
    string Source,
    int PhysicalDisplayWidth,
    int PhysicalDisplayHeight)
{
    public void Validate()
    {
        Metadata.Descriptor.PixelFormat.ShouldBeBgra32();
        if (Pixels.Length < Metadata.Descriptor.RequiredByteLength)
            throw new ArgumentException("Visual frame pixels are incomplete.", nameof(Pixels));
        if (string.IsNullOrWhiteSpace(EvidenceHash) || string.IsNullOrWhiteSpace(Source))
            throw new ArgumentException("Visual frame evidence and source are required.");
        if (PhysicalDisplayWidth < 1 || PhysicalDisplayHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(PhysicalDisplayWidth));
    }
}

public sealed record FrameBarrier(
    long MinimumFrameIdExclusive,
    DateTimeOffset? MinimumCapturedAtUtcExclusive,
    TimeSpan MaximumAge,
    string RequiredState)
{
    public bool Accepts(FrameMetadata metadata, DateTimeOffset nowUtc)
    {
        if (metadata.Id.Value <= MinimumFrameIdExclusive) return false;
        if (MinimumCapturedAtUtcExclusive is { } minimum && metadata.Timestamp.CapturedAtUtc <= minimum) return false;
        if (MaximumAge <= TimeSpan.Zero || nowUtc - metadata.Timestamp.CapturedAtUtc > MaximumAge) return false;
        return metadata.Tags is not null && metadata.Tags.TryGetValue("screen", out var state) &&
               string.Equals(state, RequiredState, StringComparison.Ordinal);
    }
}

public static class BgraPixelBridge
{
    public static byte[] ToTightlyPackedRgba32(ReadOnlySpan<byte> bgra, int width, int height, int stride)
    {
        if (width < 1 || height < 1 || stride < checked(width * 4) || bgra.Length < checked(stride * height))
            throw new ArgumentOutOfRangeException(nameof(stride));
        var rgba = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var source = bgra.Slice(y * stride, width * 4);
            var destination = rgba.AsSpan(y * width * 4, width * 4);
            for (var offset = 0; offset < source.Length; offset += 4)
            {
                destination[offset] = source[offset + 2];
                destination[offset + 1] = source[offset + 1];
                destination[offset + 2] = source[offset];
                destination[offset + 3] = source[offset + 3];
            }
        }
        return rgba;
    }

    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

file static class FramePixelFormatExtensions
{
    public static void ShouldBeBgra32(this FramePixelFormat format)
    {
        if (format != FramePixelFormat.Bgra32)
            throw new ArgumentException("VisualFrame requires BGRA32 source pixels.");
    }
}
