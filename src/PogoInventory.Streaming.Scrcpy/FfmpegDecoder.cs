using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Scrcpy;
public sealed class FfmpegDecoderOptions { public string FfmpegPath{get;init;}="ffmpeg"; public int Width{get;init;}=1080; public int Height{get;init;}=2400; public TimeSpan ShutdownTimeout{get;init;}=TimeSpan.FromSeconds(5); }
public sealed class FfmpegBgraVideoFrameDecoder : IVideoFrameDecoder {
 readonly FfmpegDecoderOptions _o; Process? _p; Task<string>? _stderr; bool _started; public FfmpegBgraVideoFrameDecoder(FfmpegDecoderOptions options){_o=options;}
 public async IAsyncEnumerable<DecodedVideoFrame> DecodeAsync(IAsyncEnumerable<EncodedVideoPacket> packets,VideoStreamMetadata metadata,[EnumeratorCancellation]CancellationToken ct){
  var width=metadata.Width>0?metadata.Width:_o.Width;var height=metadata.Height>0?metadata.Height:_o.Height;var stride=checked(width*4);var frameBytes=checked(stride*height);
  _p=new Process{StartInfo=new(_o.FfmpegPath,"-hide_banner -loglevel error -fflags nobuffer -flags low_delay -f h264 -i pipe:0 -an -sn -dn -f rawvideo -pix_fmt bgra pipe:1"){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true}};
  try{if(!_p.Start())throw new StreamTransportException(new(StreamFailureCode.DecoderInitializationFailed,"FFmpeg did not start."));_started=true;_stderr=_p.StandardError.ReadToEndAsync();}catch(Exception e){throw new StreamTransportException(new(StreamFailureCode.DecoderInitializationFailed,"FFmpeg could not start.",e.Message));}
  var writer=Task.Run(async()=>{await foreach(var packet in packets.WithCancellation(ct)){await _p.StandardInput.BaseStream.WriteAsync(packet.Data,ct);await _p.StandardInput.BaseStream.FlushAsync(ct);} _p.StandardInput.Close();},ct);
  long seq=0;var sw=Stopwatch.StartNew();
  try{while(!ct.IsCancellationRequested){var owner=MemoryPool<byte>.Shared.Rent(frameBytes);var mem=owner.Memory[..frameBytes];int read=0;var started=sw.Elapsed;while(read<frameBytes){var n=await _p.StandardOutput.BaseStream.ReadAsync(mem[read..],ct);if(n==0)break;read+=n;}if(read==0){owner.Dispose();if(_p.HasExited){var error=_stderr is null?string.Empty:await _stderr;throw new StreamTransportException(new(StreamFailureCode.FrameDecodeFailed,"FFmpeg exited before producing a complete frame.",error.Trim()));}break;}if(read!=frameBytes){owner.Dispose();throw new StreamTransportException(new(StreamFailureCode.FrameDecodeFailed,"FFmpeg returned a partial raw frame."));}var now=sw.Elapsed;yield return new(owner,frameBytes,width,height,stride,FramePixelFormat.Bgra32,++seq,now,DateTimeOffset.UtcNow,null,false,now-started);}}
  finally{try{await writer;}catch(OperationCanceledException){}try{if(_started&&!_p.HasExited)_p.Kill(true);}catch(InvalidOperationException){} }
 }
 public ValueTask DisposeAsync(){try{if(_started&&_p is not null&&!_p.HasExited)_p.Kill(true);}catch(InvalidOperationException){} _p?.Dispose();return ValueTask.CompletedTask;}
}
