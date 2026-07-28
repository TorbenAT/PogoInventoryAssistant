namespace PogoInventory.Streaming;

public sealed record FrameQuery
{
    public FrameId? AfterFrameId { get; init; }
    public TimeSpan? MaximumAge { get; init; } = TimeSpan.FromMilliseconds(500);
    public bool RequireStable { get; init; }
    public int MinimumStableFrames { get; init; } = 3;
    public TimeSpan MinimumStableDuration { get; init; } = TimeSpan.FromMilliseconds(120);
    public double MinimumCompositeQuality { get; init; } = 0.35;
    public TimeSpan SearchWindow { get; init; } = TimeSpan.FromMilliseconds(300);

    internal void Validate()
    {
        if (MaximumAge < TimeSpan.Zero || SearchWindow < TimeSpan.Zero || MinimumStableDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FrameQuery));
        }

        if (MinimumStableFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumStableFrames));
        }

        if (MinimumCompositeQuality is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumCompositeQuality));
        }
    }
}

public sealed record FrameSubscription
{
    public FrameId? AfterFrameId { get; init; }
    public bool StableFramesOnly { get; init; }
    public int Capacity { get; init; } = 1;

    internal void Validate()
    {
        if (Capacity < 1 || Capacity > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(Capacity));
        }
    }
}

public sealed record StreamingFrameSourceOptions
{
    public int BufferCapacity { get; init; } = 90;
    public TimeSpan DefaultMaximumAge { get; init; } = TimeSpan.FromMilliseconds(500);
    public bool DropOldestWhenFull { get; init; } = true;

    internal void Validate()
    {
        if (BufferCapacity < 2 || BufferCapacity > 900)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferCapacity));
        }

        if (DefaultMaximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultMaximumAge));
        }
    }
}
