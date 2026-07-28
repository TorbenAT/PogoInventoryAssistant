using System.Buffers;

namespace PogoInventory.Streaming;

public enum FramePixelFormat
{
    Bgra32 = 0,
    Rgba32 = 1,
    Gray8 = 2
}

public readonly record struct FrameId(long Value) : IComparable<FrameId>
{
    public int CompareTo(FrameId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct FrameTimestamp(
    long SourceTicks,
    DateTimeOffset CapturedAtUtc,
    TimeSpan MonotonicSinceStart);

public readonly record struct FrameDescriptor(
    int Width,
    int Height,
    int Stride,
    FramePixelFormat PixelFormat)
{
    public int RequiredByteLength => checked(Stride * Height);
}

public readonly record struct FrameQuality(
    double Sharpness,
    double Exposure,
    double Motion,
    double CompressionNoise,
    double CompositeScore)
{
    public static FrameQuality Unknown { get; } = new(0, 0, 1, 1, 0);
}

public readonly record struct FrameStability(
    double DifferenceScore,
    int ConsecutiveStableFrames,
    TimeSpan StableDuration,
    bool IsStable);

public sealed record FrameMetadata(
    FrameId Id,
    FrameTimestamp Timestamp,
    FrameDescriptor Descriptor,
    FrameQuality Quality,
    FrameStability Stability,
    string SourceName,
    IReadOnlyDictionary<string, string>? Tags = null);

public interface IFrameLease : IDisposable
{
    FrameMetadata Metadata { get; }
    ReadOnlyMemory<byte> Pixels { get; }
}

public interface IStreamingFrameSource : IAsyncDisposable
{
    bool IsRunning { get; }
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<IFrameLease?> GetLatestAsync(FrameQuery query, CancellationToken cancellationToken = default);
    IAsyncEnumerable<FrameNotification> WatchAsync(FrameSubscription subscription, CancellationToken cancellationToken = default);
}

public interface IRawFrameProducer : IAsyncDisposable
{
    string Name { get; }
    IAsyncEnumerable<RawFrame> ReadFramesAsync(CancellationToken cancellationToken = default);
}

public interface IFrameQualityEvaluator
{
    FrameQuality Evaluate(in RawFrame frame, ReadOnlySpan<byte> previousPixels);
}

public interface IFrameStabilityEvaluator
{
    FrameStability Evaluate(in RawFrame frame, ReadOnlySpan<byte> previousPixels, FrameStability previousStability);
}

public sealed class RawFrame : IDisposable
{
    private IMemoryOwner<byte>? _owner;

    public RawFrame(
        IMemoryOwner<byte> owner,
        int length,
        FrameDescriptor descriptor,
        FrameTimestamp timestamp,
        long sourceSequence)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (length < descriptor.RequiredByteLength || length > owner.Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _owner = owner;
        Length = length;
        Descriptor = descriptor;
        Timestamp = timestamp;
        SourceSequence = sourceSequence;
    }

    public int Length { get; }
    public FrameDescriptor Descriptor { get; }
    public FrameTimestamp Timestamp { get; }
    public long SourceSequence { get; }
    public ReadOnlyMemory<byte> Pixels => (_owner ?? throw new ObjectDisposedException(nameof(RawFrame))).Memory[..Length];

    internal IMemoryOwner<byte> DetachOwner()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        return owner ?? throw new ObjectDisposedException(nameof(RawFrame));
    }

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Dispose();
}

public sealed record FrameNotification(FrameMetadata Metadata);
