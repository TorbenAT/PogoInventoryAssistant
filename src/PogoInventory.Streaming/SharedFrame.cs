using System.Buffers;

namespace PogoInventory.Streaming;

internal sealed class SharedFrame
{
    private IMemoryOwner<byte>? _owner;
    private int _references;

    public SharedFrame(IMemoryOwner<byte> owner, int length, FrameMetadata metadata)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Length = length;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _references = 1;
    }

    public int Length { get; }
    public FrameMetadata Metadata { get; }

    public IFrameLease Acquire()
    {
        while (true)
        {
            var current = Volatile.Read(ref _references);
            if (current <= 0)
            {
                throw new ObjectDisposedException(nameof(SharedFrame));
            }

            if (Interlocked.CompareExchange(ref _references, current + 1, current) == current)
            {
                return new FrameLease(this);
            }
        }
    }

    public ReadOnlyMemory<byte> GetPixels()
    {
        var owner = Volatile.Read(ref _owner);
        return owner is null
            ? throw new ObjectDisposedException(nameof(SharedFrame))
            : owner.Memory[..Length];
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _references) == 0)
        {
            Interlocked.Exchange(ref _owner, null)?.Dispose();
        }
    }

    private sealed class FrameLease : IFrameLease
    {
        private SharedFrame? _frame;

        public FrameLease(SharedFrame frame) => _frame = frame;

        public FrameMetadata Metadata => (_frame ?? throw new ObjectDisposedException(nameof(FrameLease))).Metadata;
        public ReadOnlyMemory<byte> Pixels => (_frame ?? throw new ObjectDisposedException(nameof(FrameLease))).GetPixels();

        public void Dispose()
        {
            Interlocked.Exchange(ref _frame, null)?.Release();
        }
    }
}
