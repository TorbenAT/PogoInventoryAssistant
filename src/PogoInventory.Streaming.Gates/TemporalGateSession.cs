using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class TemporalGateSession : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Queue<GateFrameRecord> _history;
    private readonly Queue<FrameId> _observedFrameIds;
    private readonly int _historyCapacity;
    private readonly int _observedFrameIdCapacity;
    private bool _completed;
    private bool _disposed;
    private FrameId? _lastFrameId;
    private TemporalGateResult? _result;

    public TemporalGateSession(
        string gateName,
        TimeSpan timeout,
        int historyCapacity = 240,
        int observedFrameIdCapacity = 1024,
        DateTimeOffset? startUtc = null,
        string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gateName);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (historyCapacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        }

        if (observedFrameIdCapacity < historyCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(observedFrameIdCapacity));
        }

        SessionId = sessionId ?? Guid.NewGuid().ToString("N");
        GateName = gateName;
        Timeout = timeout;
        StartUtc = startUtc ?? DateTimeOffset.UtcNow;
        _historyCapacity = historyCapacity;
        _observedFrameIdCapacity = observedFrameIdCapacity;
        _history = new Queue<GateFrameRecord>(historyCapacity);
        _observedFrameIds = new Queue<FrameId>(observedFrameIdCapacity);
    }

    public string SessionId { get; }
    public string GateName { get; }
    public TimeSpan Timeout { get; }
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset? LastObservationUtc { get; private set; }
    public long FramesObserved { get; private set; }
    public long FramesRejected { get; private set; }
    public long HistoryEvictions { get; private set; }
    public int PeakHistoryDepth { get; private set; }
    public bool IsCompleted => Volatile.Read(ref _completed);
    public TemporalGateResult? Result => _result;

    public IReadOnlyList<FrameId> ObservedFrameIds
    {
        get
        {
            lock (_sync)
            {
                return _observedFrameIds.ToArray();
            }
        }
    }

    public bool TryAdd(TemporalFrameObservation observation, RetainedFrame? frame, out GateReasonCode rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_completed)
            {
                frame?.Dispose();
                rejectionReason = GateReasonCode.InsufficientEvidence;
                return false;
            }

            if (_lastFrameId.HasValue && observation.FrameId.CompareTo(_lastFrameId.Value) <= 0)
            {
                frame?.Dispose();
                FramesRejected++;
                rejectionReason = GateReasonCode.OutOfOrderFrame;
                return false;
            }

            _lastFrameId = observation.FrameId;
            LastObservationUtc = observation.UtcTimestamp;
            FramesObserved++;
            _history.Enqueue(new GateFrameRecord(observation, frame));
            _observedFrameIds.Enqueue(observation.FrameId);

            while (_history.Count > _historyCapacity)
            {
                var evicted = _history.Dequeue();
                evicted.Frame?.Dispose();
                HistoryEvictions++;
            }

            while (_observedFrameIds.Count > _observedFrameIdCapacity)
            {
                _observedFrameIds.Dequeue();
            }

            PeakHistoryDepth = Math.Max(PeakHistoryDepth, _history.Count);
            rejectionReason = GateReasonCode.Pending;
            return true;
        }
    }

    public bool TryComplete(TemporalGateResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsTerminal)
        {
            throw new ArgumentException("A session can only complete with a terminal result.", nameof(result));
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            if (_completed)
            {
                return false;
            }

            _result = result;
            _completed = true;
            return true;
        }
    }

    internal IReadOnlyList<GateFrameRecord> SnapshotRecords()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _history.ToArray();
        }
    }

    public IReadOnlyList<TemporalFrameObservation> SnapshotForReport()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _history.Select(x => x.Observation).ToArray();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            while (_history.Count > 0)
            {
                _history.Dequeue().Frame?.Dispose();
            }

            _observedFrameIds.Clear();
        }

        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TemporalGateSession));
        }
    }
}

internal sealed record GateFrameRecord(TemporalFrameObservation Observation, RetainedFrame? Frame);
