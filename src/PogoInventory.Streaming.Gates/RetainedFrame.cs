using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class RetainedFrame : IDisposable
{
    private IFrameLease? _root;
    private int _references = 1;
    private int _rootDisposed;
    private static long _activeReferences;

    public RetainedFrame(IFrameLease root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        Interlocked.Increment(ref _activeReferences);
    }

    public static long ActiveReferences => Interlocked.Read(ref _activeReferences);

    public FrameMetadata Metadata =>
        Volatile.Read(ref _root)?.Metadata ?? throw new ObjectDisposedException(nameof(RetainedFrame));

    public IFrameLease Acquire()
    {
        while (true)
        {
            var current = Volatile.Read(ref _references);
            if (current <= 0)
            {
                throw new ObjectDisposedException(nameof(RetainedFrame));
            }

            if (Interlocked.CompareExchange(ref _references, current + 1, current) == current)
            {
                Interlocked.Increment(ref _activeReferences);
                return new ChildLease(this);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _rootDisposed, 1) == 0)
        {
            Release();
        }
    }

    private ReadOnlyMemory<byte> Pixels =>
        Volatile.Read(ref _root)?.Pixels ?? throw new ObjectDisposedException(nameof(RetainedFrame));

    private void Release()
    {
        var remaining = Interlocked.Decrement(ref _references);
        Interlocked.Decrement(ref _activeReferences);
        if (remaining == 0)
        {
            Interlocked.Exchange(ref _root, null)?.Dispose();
        }
        else if (remaining < 0)
        {
            throw new ObjectDisposedException(nameof(RetainedFrame));
        }
    }

    private sealed class ChildLease : IFrameLease
    {
        private RetainedFrame? _owner;

        public ChildLease(RetainedFrame owner) => _owner = owner;

        public FrameMetadata Metadata =>
            (_owner ?? throw new ObjectDisposedException(nameof(ChildLease))).Metadata;

        public ReadOnlyMemory<byte> Pixels =>
            (_owner ?? throw new ObjectDisposedException(nameof(ChildLease))).Pixels;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}
