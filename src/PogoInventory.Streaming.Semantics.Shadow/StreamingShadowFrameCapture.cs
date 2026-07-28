using System.Runtime.CompilerServices;
using PogoInventory.Streaming;
using PogoInventory.Vision.Models;

namespace PogoInventory.Streaming.Semantics.Shadow;

public sealed record StreamingShadowCaptureOptions
{
    public int MaximumFrames { get; init; } = 30;
    public int SubscriptionCapacity { get; init; } = 2;
    public bool RequireStable { get; init; } = true;
    public int MinimumStableFrames { get; init; } = 3;
    public TimeSpan MinimumStableDuration { get; init; } = TimeSpan.FromMilliseconds(120);
    public double MinimumCompositeQuality { get; init; } = 0.35;
    public TimeSpan MaximumFrameAge { get; init; } = TimeSpan.FromSeconds(1);

    public void Validate()
    {
        if (MaximumFrames < 1 || MaximumFrames > 1000)
            throw new ArgumentOutOfRangeException(nameof(MaximumFrames));
        if (SubscriptionCapacity < 1 || SubscriptionCapacity > 64)
            throw new ArgumentOutOfRangeException(nameof(SubscriptionCapacity));
        if (MinimumStableFrames < 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumStableFrames));
        if (MinimumStableDuration < TimeSpan.Zero || MaximumFrameAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MinimumStableDuration));
        if (MinimumCompositeQuality is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumCompositeQuality));
    }
}

public sealed class StreamingShadowFrameCapture
{
    private readonly IStreamingFrameSource _source;
    private readonly IReadOnlyDictionary<string, NormalizedRegion> _regions;

    public StreamingShadowFrameCapture(
        IStreamingFrameSource source,
        IReadOnlyDictionary<string, NormalizedRegion> regions)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(regions);
        _regions = regions.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var region in _regions) region.Value.Validate(region.Key);
    }

    public async IAsyncEnumerable<ShadowFrameInput> CaptureAsync(
        StreamingShadowCaptureOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!_source.IsRunning)
            throw new InvalidOperationException("The streaming source must already be running.");

        var emitted = 0;
        long lastFrameId = -1;
        var subscription = new FrameSubscription
        {
            StableFramesOnly = options.RequireStable,
            Capacity = options.SubscriptionCapacity
        };

        await foreach (var notification in _source.WatchAsync(subscription, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var afterValue = Math.Max(-1, notification.Metadata.Id.Value - 1);
            using var lease = await _source.GetLatestAsync(
                new FrameQuery
                {
                    AfterFrameId = new FrameId(afterValue),
                    MaximumAge = options.MaximumFrameAge,
                    RequireStable = options.RequireStable,
                    MinimumStableFrames = options.MinimumStableFrames,
                    MinimumStableDuration = options.MinimumStableDuration,
                    MinimumCompositeQuality = options.MinimumCompositeQuality
                },
                cancellationToken).ConfigureAwait(false);

            if (lease is null ||
                lease.Metadata.Id.Value < notification.Metadata.Id.Value ||
                lease.Metadata.Id.Value <= lastFrameId)
                continue;

            var captured = ShadowFrameFactory.Capture(
                lease,
                _regions,
                new[] { options.RequireStable ? "LatestStableFrame" : "LatestFrame" });
            lastFrameId = captured.FrameId;
            yield return captured;

            emitted++;
            if (emitted >= options.MaximumFrames)
                yield break;
        }
    }
}
