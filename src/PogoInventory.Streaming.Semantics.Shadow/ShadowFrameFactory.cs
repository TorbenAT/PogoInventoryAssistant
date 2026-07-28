using System.Security.Cryptography;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Vision.Models;

namespace PogoInventory.Streaming.Semantics.Shadow;

public static class ShadowFrameFactory
{
    public static ShadowFrameInput Capture(
        IFrameLease lease,
        IReadOnlyDictionary<string, NormalizedRegion> regions,
        IReadOnlyList<string>? roles = null)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(regions);

        var metadata = lease.Metadata;
        if (metadata.Descriptor.PixelFormat != FramePixelFormat.Bgra32)
            throw new ArgumentException("Only BGRA32 leases can enter the Phase 6B shadow path.", nameof(lease));

        var required = metadata.Descriptor.RequiredByteLength;
        if (lease.Pixels.Length < required)
            throw new ArgumentException("The frame lease is shorter than the declared descriptor.", nameof(lease));

        var pixels = lease.Pixels[..required].ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(pixels));
        var regionCopy = regions.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var semantic = new SemanticFrameObservation(
            metadata.Id.Value,
            hash,
            metadata.Descriptor.Width,
            metadata.Descriptor.Height,
            metadata.Descriptor.Width >= metadata.Descriptor.Height ? "Landscape" : "Portrait",
            regionCopy);

        var input = new ShadowFrameInput(
            semantic,
            metadata,
            pixels,
            roles is { Count: > 0 } ? roles.ToArray() : new[] { "StableFrame" });
        input.Validate();
        return input;
    }

    public static IReadOnlyList<ShadowFrameInput> Capture(
        SelectedFrameSet selected,
        IReadOnlyDictionary<string, NormalizedRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(regions);

        return selected.Frames.Values
            .GroupBy(x => x.FrameId)
            .OrderBy(x => x.Key.Value)
            .Select(group =>
            {
                var first = group.OrderBy(x => x.Role).First();
                var roles = group.Select(x => x.Role.ToString()).Order(StringComparer.Ordinal).ToArray();
                return Capture(first.Lease, regions, roles);
            })
            .ToArray();
    }
}
