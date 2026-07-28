using System.Runtime.CompilerServices;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public interface IFrameLeaseSource
{
    long DroppedFrames { get; }
    IAsyncEnumerable<IFrameLease> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed class StreamingFrameLeaseSource : IFrameLeaseSource
{
    private readonly IStreamingFrameSource _source;
    private readonly int _subscriptionCapacity;
    private long _droppedFrames;

    public StreamingFrameLeaseSource(IStreamingFrameSource source, int subscriptionCapacity = 4)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (subscriptionCapacity < 1 || subscriptionCapacity > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(subscriptionCapacity));
        }

        _subscriptionCapacity = subscriptionCapacity;
    }

    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

    public async IAsyncEnumerable<IFrameLease> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FrameId? lastFrameId = null;
        await foreach (var notification in _source.WatchAsync(
                           new FrameSubscription { Capacity = _subscriptionCapacity },
                           cancellationToken).ConfigureAwait(false))
        {
            var lease = await _source.GetLatestAsync(
                new FrameQuery
                {
                    AfterFrameId = lastFrameId,
                    MaximumAge = TimeSpan.FromSeconds(1),
                    MinimumCompositeQuality = 0,
                    SearchWindow = TimeSpan.FromSeconds(1)
                },
                cancellationToken).ConfigureAwait(false);
            if (lease is null)
            {
                continue;
            }

            if (lastFrameId.HasValue)
            {
                var gap = lease.Metadata.Id.Value - lastFrameId.Value.Value - 1;
                if (gap > 0)
                {
                    Interlocked.Add(ref _droppedFrames, gap);
                }
            }

            if (lease.Metadata.Id.Value < notification.Metadata.Id.Value)
            {
                Interlocked.Increment(ref _droppedFrames);
            }

            lastFrameId = lease.Metadata.Id;
            yield return lease;
        }
    }
}

public sealed class EnumerableFrameLeaseSource : IFrameLeaseSource
{
    private readonly IAsyncEnumerable<IFrameLease> _frames;

    public EnumerableFrameLeaseSource(IAsyncEnumerable<IFrameLease> frames) =>
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));

    public long DroppedFrames => 0;

    public async IAsyncEnumerable<IFrameLease> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }
}
