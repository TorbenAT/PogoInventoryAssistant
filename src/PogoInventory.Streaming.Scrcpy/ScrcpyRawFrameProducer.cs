using System.Runtime.CompilerServices;
using PogoInventory.Streaming;
namespace PogoInventory.Streaming.Scrcpy;
public sealed class ScrcpyRawFrameProducer : IRawFrameProducer {
 readonly IReadOnlyVideoTransport _transport;readonly IVideoFrameDecoder _decoder;public string Name=>"scrcpy-read-only-h264";
 public ScrcpyRawFrameProducer(IReadOnlyVideoTransport transport,IVideoFrameDecoder decoder){_transport=transport;_decoder=decoder;}
 public async IAsyncEnumerable<RawFrame> ReadFramesAsync([EnumeratorCancellation]CancellationToken ct=default){
  var packets=_transport.ReadPacketsAsync(ct);await foreach(var f in _decoder.DecodeAsync(packets,_transport.Metadata??new("h264",0,0,30,"unknown"),ct).WithCancellation(ct)){using(f){var ts=new FrameTimestamp(f.SourceTimestamp?.Ticks??f.SourceSequence,f.UtcTimestamp,f.MonotonicTimestamp);yield return new RawFrame(f.DetachOwner(),f.Length,new FrameDescriptor(f.Width,f.Height,f.Stride,f.PixelFormat),ts,f.SourceSequence);}}
 }
 public async ValueTask DisposeAsync(){await _transport.DisposeAsync();await _decoder.DisposeAsync();}
}
