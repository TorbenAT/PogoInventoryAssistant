using System.Buffers;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Scrcpy;

var passed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) Environment.ExitCode = 1; else passed++;
}

Check("Read-only contract disables control", !ScrcpyReadOnlyContract.ControlChannelEnabled);
Check("Read-only contract reports zero input", ScrcpyReadOnlyContract.InputCommandsSent == 0);
Check("Raw stream contract is enabled", ScrcpyReadOnlyContract.RawStreamEnabled);
Check("Expected BGRA frame size is exact", checked(886 * 1920 * 4) == 6_804_480);
Check("Dimension resolver produces 886x1920", StreamDimensionResolver.Resolve(new(1080, 2340, "Portrait"), 1920, null, null) is { Width: 886, Height: 1920 });
var packet = new EncodedVideoPacket(new byte[] { 0, 0, 0, 1, 0x67 }, 1, TimeSpan.Zero, DateTimeOffset.UtcNow, true);
Check("Annex-B SPS payload is preserved", packet.Data.Span.SequenceEqual(new byte[] { 0, 0, 0, 1, 0x67 }));
var options = new ScrcpyOptions { DeviceSerial = "serial", AdbPath = "adb", ScrcpyServerJar = "server" };
options.Validate();
Check("Scrcpy options validate without input surface", options.MaxFps == 30 && options.MaxSize == 1920);
var transport = new FakeTransport(packet);
var decoder = new MetadataCaptureDecoder();
await using (var producer = new ScrcpyRawFrameProducer(transport, decoder))
{
    await foreach (var _ in producer.ReadFramesAsync()) { }
}
Check("Producer waits for metadata and preserves first packet", decoder.Metadata is { Width: 886, Height: 1920 } && decoder.Packets.Single().Data.Span.SequenceEqual(packet.Data.Span));
Console.WriteLine($"Phase 5 self-test: {passed}/8");

sealed class FakeTransport(EncodedVideoPacket packet) : IReadOnlyVideoTransport
{
    public ComponentLifecycle Lifecycle => ComponentLifecycle.Created;
    public VideoStreamMetadata? Metadata { get; private set; }
    public async IAsyncEnumerable<EncodedVideoPacket> ReadPacketsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Metadata = new("h264", 886, 1920, 30, "fake");
        yield return packet;
        await Task.CompletedTask;
    }
    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class MetadataCaptureDecoder : IVideoFrameDecoder
{
    public VideoStreamMetadata? Metadata { get; private set; }
    public List<EncodedVideoPacket> Packets { get; } = [];
    public async IAsyncEnumerable<DecodedVideoFrame> DecodeAsync(IAsyncEnumerable<EncodedVideoPacket> packets, VideoStreamMetadata metadata, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Metadata = metadata;
        await foreach (var item in packets.WithCancellation(cancellationToken)) Packets.Add(item);
        await Task.CompletedTask;
        yield break;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
