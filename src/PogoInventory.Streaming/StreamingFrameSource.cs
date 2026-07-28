using System.Threading.Channels;

namespace PogoInventory.Streaming;

public sealed class StreamingFrameSource : IStreamingFrameSource
{
    private readonly IRawFrameProducer _producer;
    private readonly IFrameQualityEvaluator _qualityEvaluator;
    private readonly IFrameStabilityEvaluator _stabilityEvaluator;
    private readonly StreamingFrameSourceOptions _options;
    private readonly FrameRingBuffer _buffer;
    private readonly object _lifecycleGate = new();
    private readonly object _subscriberGate = new();
    private readonly Dictionary<long, Subscriber> _subscribers = new();
    private CancellationTokenSource? _runCancellation;
    private Task? _pumpTask;
    private long _nextFrameId;
    private long _nextSubscriberId;
    private int _running;

    public StreamingFrameSource(
        IRawFrameProducer producer,
        IFrameQualityEvaluator? qualityEvaluator = null,
        IFrameStabilityEvaluator? stabilityEvaluator = null,
        StreamingFrameSourceOptions? options = null)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _qualityEvaluator = qualityEvaluator ?? new DefaultFrameQualityEvaluator();
        _stabilityEvaluator = stabilityEvaluator ?? new DefaultFrameStabilityEvaluator();
        _options = options ?? new StreamingFrameSourceOptions();
        _options.Validate();
        _buffer = new FrameRingBuffer(_options.BufferCapacity);
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;
    public Exception? LastError { get; private set; }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lifecycleGate)
        {
            if (_pumpTask is not null)
            {
                return ValueTask.CompletedTask;
            }

            _runCancellation = new CancellationTokenSource();
            _pumpTask = Task.Run(() => PumpAsync(_runCancellation.Token), CancellationToken.None);
            Volatile.Write(ref _running, 1);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task? pump;
        lock (_lifecycleGate)
        {
            pump = _pumpTask;
            _runCancellation?.Cancel();
        }

        if (pump is not null)
        {
            await pump.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<IFrameLease?> GetLatestAsync(FrameQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_buffer.Select(query, DateTimeOffset.UtcNow));
    }

    public async IAsyncEnumerable<FrameNotification> WatchAsync(
        FrameSubscription subscription,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();
        var subscriber = AddSubscriber(subscription);

        try
        {
            await foreach (var notification in subscriber.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return notification;
            }
        }
        finally
        {
            RemoveSubscriber(subscriber.Id);
        }
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        byte[] previous = Array.Empty<byte>();
        var previousStability = new FrameStability(1, 0, TimeSpan.Zero, false);

        try
        {
            await foreach (var raw in _producer.ReadFramesAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                using (raw)
                {
                    var quality = _qualityEvaluator.Evaluate(raw, previous);
                    var stability = _stabilityEvaluator.Evaluate(raw, previous, previousStability);
                    previousStability = stability;

                    var id = new FrameId(Interlocked.Increment(ref _nextFrameId));
                    var metadata = new FrameMetadata(
                        id,
                        raw.Timestamp,
                        raw.Descriptor,
                        quality,
                        stability,
                        _producer.Name);

                    var owner = raw.DetachOwner();
                    var shared = new SharedFrame(owner, raw.Length, metadata);
                    _buffer.Add(shared);
                    Publish(metadata);

                    if (previous.Length != raw.Length)
                    {
                        previous = new byte[raw.Length];
                    }

                    shared.GetPixels().Span.CopyTo(previous);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            LastError = error;
        }
        finally
        {
            Volatile.Write(ref _running, 0);
            CompleteSubscribers();
            lock (_lifecycleGate)
            {
                _runCancellation?.Dispose();
                _runCancellation = null;
                _pumpTask = null;
            }
        }
    }

    private Subscriber AddSubscriber(FrameSubscription subscription)
    {
        var options = new BoundedChannelOptions(subscription.Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        };
        var subscriber = new Subscriber(
            Interlocked.Increment(ref _nextSubscriberId),
            subscription,
            Channel.CreateBounded<FrameNotification>(options));

        lock (_subscriberGate)
        {
            _subscribers.Add(subscriber.Id, subscriber);
        }

        return subscriber;
    }

    private void Publish(FrameMetadata metadata)
    {
        Subscriber[] subscribers;
        lock (_subscriberGate)
        {
            subscribers = _subscribers.Values.ToArray();
        }

        foreach (var subscriber in subscribers)
        {
            if (subscriber.Subscription.AfterFrameId is { } after && metadata.Id.CompareTo(after) <= 0)
            {
                continue;
            }

            if (subscriber.Subscription.StableFramesOnly && !metadata.Stability.IsStable)
            {
                continue;
            }

            subscriber.Channel.Writer.TryWrite(new FrameNotification(metadata));
        }
    }

    private void RemoveSubscriber(long id)
    {
        Subscriber? subscriber;
        lock (_subscriberGate)
        {
            _subscribers.Remove(id, out subscriber);
        }

        subscriber?.Channel.Writer.TryComplete();
    }

    private void CompleteSubscribers()
    {
        Subscriber[] subscribers;
        lock (_subscriberGate)
        {
            subscribers = _subscribers.Values.ToArray();
            _subscribers.Clear();
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Channel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _buffer.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record Subscriber(
        long Id,
        FrameSubscription Subscription,
        Channel<FrameNotification> Channel);
}
