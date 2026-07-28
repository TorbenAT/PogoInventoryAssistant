namespace PogoInventory.Streaming;

internal sealed class FrameRingBuffer : IDisposable
{
    private readonly object _gate = new();
    private readonly SharedFrame?[] _frames;
    private int _start;
    private int _count;
    private bool _disposed;

    public FrameRingBuffer(int capacity) => _frames = new SharedFrame[capacity];

    public void Add(SharedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        SharedFrame? evicted = null;

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_count == _frames.Length)
            {
                evicted = _frames[_start];
                _frames[_start] = frame;
                _start = (_start + 1) % _frames.Length;
            }
            else
            {
                _frames[(_start + _count) % _frames.Length] = frame;
                _count++;
            }
        }

        evicted?.Release();
    }

    public IFrameLease? Select(FrameQuery query, DateTimeOffset now)
    {
        query.Validate();
        lock (_gate)
        {
            ThrowIfDisposed();
            SharedFrame? best = null;
            var newestTime = DateTimeOffset.MinValue;

            for (var index = 0; index < _count; index++)
            {
                var frame = _frames[(_start + index) % _frames.Length]!;
                var metadata = frame.Metadata;

                if (query.AfterFrameId is { } after && metadata.Id.CompareTo(after) <= 0)
                {
                    continue;
                }

                if (query.MaximumAge is { } maximumAge && now - metadata.Timestamp.CapturedAtUtc > maximumAge)
                {
                    continue;
                }

                if (query.RequireStable &&
                    (!metadata.Stability.IsStable ||
                     metadata.Stability.ConsecutiveStableFrames < query.MinimumStableFrames ||
                     metadata.Stability.StableDuration < query.MinimumStableDuration))
                {
                    continue;
                }

                if (metadata.Quality.CompositeScore < query.MinimumCompositeQuality)
                {
                    continue;
                }

                if (metadata.Timestamp.CapturedAtUtc > newestTime)
                {
                    newestTime = metadata.Timestamp.CapturedAtUtc;
                }

                if (best is null || IsBetter(frame.Metadata, best.Metadata, newestTime, query.SearchWindow))
                {
                    best = frame;
                }
            }

            return best?.Acquire();
        }
    }

    private static bool IsBetter(FrameMetadata candidate, FrameMetadata current, DateTimeOffset newestTime, TimeSpan window)
    {
        var candidateInsideWindow = newestTime - candidate.Timestamp.CapturedAtUtc <= window;
        var currentInsideWindow = newestTime - current.Timestamp.CapturedAtUtc <= window;

        if (candidateInsideWindow != currentInsideWindow)
        {
            return candidateInsideWindow;
        }

        var candidateScore = candidate.Quality.CompositeScore + (candidate.Stability.IsStable ? 0.15 : 0);
        var currentScore = current.Quality.CompositeScore + (current.Stability.IsStable ? 0.15 : 0);
        return candidateScore > currentScore ||
               (Math.Abs(candidateScore - currentScore) < 0.0001 && candidate.Id.CompareTo(current.Id) > 0);
    }

    public void Dispose()
    {
        SharedFrame?[] toRelease;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toRelease = _frames.Where(static frame => frame is not null).ToArray()!;
            Array.Clear(_frames);
            _count = 0;
        }

        foreach (var frame in toRelease)
        {
            frame?.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
