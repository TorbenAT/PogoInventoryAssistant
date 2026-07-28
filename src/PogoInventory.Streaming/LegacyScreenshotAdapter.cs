namespace PogoInventory.Streaming;

public interface ILegacyScreenshotSource
{
    ValueTask<byte[]> CapturePngAsync(CancellationToken cancellationToken = default);
}

public interface IFramePngEncoder
{
    byte[] Encode(FrameDescriptor descriptor, ReadOnlySpan<byte> pixels);
}

public sealed class StreamingScreenshotAdapter : ILegacyScreenshotSource
{
    private readonly IStreamingFrameSource _source;
    private readonly IFramePngEncoder _encoder;
    private readonly FrameQuery _query;

    public StreamingScreenshotAdapter(
        IStreamingFrameSource source,
        IFramePngEncoder encoder,
        FrameQuery? query = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _query = query ?? new FrameQuery
        {
            RequireStable = true,
            MinimumCompositeQuality = 0.40
        };
    }

    public async ValueTask<byte[]> CapturePngAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await _source.GetLatestAsync(_query, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            throw new InvalidOperationException("No frame satisfied the fail-closed screenshot query.");
        }

        return _encoder.Encode(lease.Metadata.Descriptor, lease.Pixels.Span);
    }
}
