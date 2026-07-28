using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace PogoInventory.Streaming.Scrcpy;

public sealed class AdbDeviceValidator {
 public async Task<StreamFailure?> ValidateAsync(string adb,string serial,CancellationToken ct){
  try { var r=await RunAsync(adb,"devices",ct); if(r.ExitCode!=0)return new(StreamFailureCode.AdbUnavailable,"adb devices failed",r.Error);
   var matches=r.Output.Split('\n',StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x=>x.Trim()).Where(x=>x.StartsWith(serial+"\t",StringComparison.Ordinal)).ToArray();
   if(matches.Length==0)return new(StreamFailureCode.DeviceNotFound,$"Device '{serial}' was not found."); if(matches.Length>1)return new(StreamFailureCode.MultipleDevicesMatched,$"More than one matching device was found for '{serial}'."); if(!matches[0].EndsWith("\tdevice",StringComparison.Ordinal))return new(StreamFailureCode.DeviceNotFound,$"Device '{serial}' is not in device state."); return null;
  } catch(Exception e){return new(StreamFailureCode.AdbUnavailable,"adb could not be executed",e.Message);} }
 internal static async Task<(int ExitCode,string Output,string Error)> RunAsync(string file,string args,CancellationToken ct){var p=new Process{StartInfo=new(file,args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true}};p.Start();var o=p.StandardOutput.ReadToEndAsync(ct);var e=p.StandardError.ReadToEndAsync(ct);await p.WaitForExitAsync(ct);return(p.ExitCode,await o,await e);}
}

public sealed class ScrcpyReadOnlyVideoTransport : IReadOnlyVideoTransport {
 readonly ScrcpyOptions _o; readonly AdbDeviceValidator _validator=new(); TcpClient? _tcp; Process? _server; Task<string>? _serverError; CancellationTokenSource? _run; int _state=(int)ComponentLifecycle.Created; public ComponentLifecycle Lifecycle=>(ComponentLifecycle)Volatile.Read(ref _state); ComponentLifecycle IReadOnlyVideoTransport.Lifecycle=>Lifecycle; public VideoStreamMetadata? Metadata{get;private set;}
 public ScrcpyReadOnlyVideoTransport(ScrcpyOptions options){_o=options;_o.Validate();}
 public async IAsyncEnumerable<EncodedVideoPacket> ReadPacketsAsync([EnumeratorCancellation]CancellationToken cancellationToken){
  if(Interlocked.CompareExchange(ref _state,(int)ComponentLifecycle.Starting,(int)ComponentLifecycle.Created)!=(int)ComponentLifecycle.Created && Interlocked.CompareExchange(ref _state,(int)ComponentLifecycle.Starting,(int)ComponentLifecycle.Stopped)!=(int)ComponentLifecycle.Stopped)throw new InvalidOperationException("Transport may only be started once at a time.");
  _run=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); using var startup=CancellationTokenSource.CreateLinkedTokenSource(_run.Token); startup.CancelAfter(_o.StartupTimeout);
  var failure=await _validator.ValidateAsync(_o.AdbPath,_o.DeviceSerial,startup.Token); if(failure!=null){Volatile.Write(ref _state,(int)ComponentLifecycle.Faulted);throw new StreamTransportException(failure);}
  if(!File.Exists(_o.ScrcpyServerJar)){Volatile.Write(ref _state,(int)ComponentLifecycle.Faulted);throw new StreamTransportException(new(StreamFailureCode.ScrcpyServerMissing,"scrcpy server JAR not found",_o.ScrcpyServerJar));}
  var remote="/data/local/tmp/pogo-scrcpy-server.jar"; await RequireAdbAsync($"-s {Q(_o.DeviceSerial)} push {Q(_o.ScrcpyServerJar)} {remote}",startup.Token,StreamFailureCode.ScrcpyServerStartFailed);
  await RequireAdbAsync($"-s {Q(_o.DeviceSerial)} forward tcp:{_o.LocalPort} localabstract:scrcpy",startup.Token,StreamFailureCode.VideoSocketConnectionFailed);
  DisplayDimensions display; ResolvedStreamDimensions resolved;
  try { var displayOutput=(await AdbDeviceValidator.RunAsync(_o.AdbPath,$"-s {Q(_o.DeviceSerial)} shell wm size",startup.Token)).Output; display=AdbDisplayDimensionParser.ParseWmSize(displayOutput); resolved=StreamDimensionResolver.Resolve(display,_o.MaxSize,_o.RequestedWidth,_o.RequestedHeight); }
  catch(StreamTransportException) { Volatile.Write(ref _state,(int)ComponentLifecycle.Faulted); throw; }
  catch(Exception e) { Volatile.Write(ref _state,(int)ComponentLifecycle.Faulted); throw new StreamTransportException(new(StreamFailureCode.StreamDimensionMismatch,"Could not resolve the expected raw stream dimensions.",e.Message)); }
  var args=$"-s {Q(_o.DeviceSerial)} shell CLASSPATH={remote} app_process / {_o.JavaMainClass} {_o.ScrcpyServerVersion} tunnel_forward=true audio=false control=false cleanup=true raw_stream=true max_size={_o.MaxSize} max_fps={_o.MaxFps}";
  _server=new Process{StartInfo=new(_o.AdbPath,args){RedirectStandardError=true,RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true}}; if(!_server.Start())throw new StreamTransportException(new(StreamFailureCode.ScrcpyServerStartFailed,"Could not start scrcpy server process.")); _serverError=_server.StandardError.ReadToEndAsync();
  await Task.Delay(500,startup.Token); _tcp=new TcpClient(); await _tcp.ConnectAsync("127.0.0.1",_o.LocalPort,startup.Token); var stream=_tcp.GetStream(); Metadata=new("h264",resolved.Width,resolved.Height,_o.MaxFps,_o.DeviceSerial); Volatile.Write(ref _state,(int)ComponentLifecycle.Running);
  var buffer=new byte[256*1024];long seq=0;var sw=Stopwatch.StartNew();
  try{while(true){var n=await stream.ReadAsync(buffer,_run.Token);if(n==0){var detail=_server is not null&&_server.HasExited&&_serverError is not null?await _serverError:null;throw new StreamTransportException(new(StreamFailureCode.StreamEndedUnexpectedly,"scrcpy video stream ended.",detail?.Trim()));}var copy=new byte[n];Buffer.BlockCopy(buffer,0,copy,0,n);yield return new(copy,++seq,sw.Elapsed,DateTimeOffset.UtcNow,ContainsIdr(copy));}}
  finally{await StopAsync(CancellationToken.None);}
 }
 static bool ContainsIdr(ReadOnlySpan<byte>b){for(int i=0;i+4<b.Length;i++)if(b[i]==0&&b[i+1]==0&&(b[i+2]==1||b[i+2]==0&&b[i+3]==1)){int j=b[i+2]==1?i+3:i+4;if(j<b.Length&&(b[j]&0x1f)==5)return true;}return false;}
 async Task RequireAdbAsync(string args,CancellationToken ct,StreamFailureCode code){var r=await AdbDeviceValidator.RunAsync(_o.AdbPath,args,ct);if(r.ExitCode!=0)throw new StreamTransportException(new(code,$"adb command failed: {args}",r.Error));}
 static string Q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
 public async ValueTask StopAsync(CancellationToken cancellationToken=default){var old=(ComponentLifecycle)Interlocked.Exchange(ref _state,(int)ComponentLifecycle.Stopping);if(old is ComponentLifecycle.Stopped or ComponentLifecycle.Disposed)return;_run?.Cancel();_tcp?.Dispose();if(_server is{HasExited:false}){try{_server.Kill(true);await _server.WaitForExitAsync(cancellationToken);}catch{}}try{await AdbDeviceValidator.RunAsync(_o.AdbPath,$"-s {Q(_o.DeviceSerial)} forward --remove tcp:{_o.LocalPort}",cancellationToken);}catch{} _server?.Dispose();_run?.Dispose();Volatile.Write(ref _state,(int)ComponentLifecycle.Stopped);}
 public async ValueTask DisposeAsync(){await StopAsync();Volatile.Write(ref _state,(int)ComponentLifecycle.Disposed);}
}
public sealed class StreamTransportException(StreamFailure failure):Exception(failure.Message){public StreamFailure Failure{get;}=failure;}
