using System.Buffers;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Scrcpy;

public enum StreamFailureCode { None, DeviceNotFound, DeviceUnauthorized, DeviceOffline, MultipleDevicesMatched, AdbUnavailable, ScrcpyServerMissing, ScrcpyVersionMismatch, ScrcpyServerStartFailed, VideoSocketConnectionFailed, FfmpegUnavailable, UnsupportedProtocol, UnsupportedCodec, DecoderInitializationFailed, StreamEndedUnexpectedly, FrameDecodeFailed, ResolutionChangedUnsupported, StreamDimensionMismatch, NoFramesReceived, NoStableFrameObserved, ObservationTimedOut, StartupTimedOut, ShutdownTimedOut, Cancelled }
public enum ComponentLifecycle { Created, Starting, Running, Stopping, Stopped, Faulted, Disposed }
public sealed record StreamFailure(StreamFailureCode Code, string Message, string? Detail = null);
public sealed record EncodedVideoPacket(ReadOnlyMemory<byte> Data, long Sequence, TimeSpan ReceivedMonotonic, DateTimeOffset ReceivedUtc, bool IsKeyFrame);
public sealed class DecodedVideoFrame : IDisposable {
 private IMemoryOwner<byte>? _owner;
 public DecodedVideoFrame(IMemoryOwner<byte> owner,int length,int width,int height,int stride,FramePixelFormat pixelFormat,long sourceSequence,TimeSpan monotonicTimestamp,DateTimeOffset utcTimestamp,TimeSpan? sourceTimestamp,bool isKeyFrame,TimeSpan decodeDuration){_owner=owner;Length=length;Width=width;Height=height;Stride=stride;PixelFormat=pixelFormat;SourceSequence=sourceSequence;MonotonicTimestamp=monotonicTimestamp;UtcTimestamp=utcTimestamp;SourceTimestamp=sourceTimestamp;IsKeyFrame=isKeyFrame;DecodeDuration=decodeDuration;}
 public int Length{get;} public int Width{get;} public int Height{get;} public int Stride{get;} public FramePixelFormat PixelFormat{get;} public long SourceSequence{get;} public TimeSpan MonotonicTimestamp{get;} public DateTimeOffset UtcTimestamp{get;} public TimeSpan? SourceTimestamp{get;} public bool IsKeyFrame{get;} public TimeSpan DecodeDuration{get;}
 public IMemoryOwner<byte> DetachOwner()=>Interlocked.Exchange(ref _owner,null)??throw new ObjectDisposedException(nameof(DecodedVideoFrame)); public void Dispose()=>Interlocked.Exchange(ref _owner,null)?.Dispose();
}
public interface IVideoFrameDecoder : IAsyncDisposable { IAsyncEnumerable<DecodedVideoFrame> DecodeAsync(IAsyncEnumerable<EncodedVideoPacket> packets, VideoStreamMetadata metadata, CancellationToken cancellationToken); }
public sealed record VideoStreamMetadata(string Codec, int Width, int Height, int MaxFps, string DeviceName);
public interface IReadOnlyVideoTransport : IAsyncDisposable { ComponentLifecycle Lifecycle { get; } VideoStreamMetadata? Metadata { get; } IAsyncEnumerable<EncodedVideoPacket> ReadPacketsAsync(CancellationToken cancellationToken); ValueTask StopAsync(CancellationToken cancellationToken=default); }
public sealed class ScrcpyOptions {
 public required string DeviceSerial {get;init;} public required string AdbPath {get;init;} public required string ScrcpyServerJar {get;init;} public string ScrcpyServerVersion {get;init;}="4.0"; public string JavaMainClass {get;init;}="com.genymobile.scrcpy.Server"; public int LocalPort {get;init;}=27183; public int MaxSize {get;init;}=1920; public int MaxFps {get;init;}=30; public TimeSpan StartupTimeout {get;init;}=TimeSpan.FromSeconds(15); public TimeSpan ShutdownTimeout {get;init;}=TimeSpan.FromSeconds(5);
 public int? RequestedWidth {get;init;} public int? RequestedHeight {get;init;}
 public void Validate(){ArgumentException.ThrowIfNullOrWhiteSpace(DeviceSerial);ArgumentException.ThrowIfNullOrWhiteSpace(AdbPath);ArgumentException.ThrowIfNullOrWhiteSpace(ScrcpyServerJar);if(MaxFps<=0||MaxSize<=0)throw new ArgumentOutOfRangeException();}
}

public static class ScrcpyReadOnlyContract
{
    public const bool ControlChannelEnabled = false;
    public const bool RawStreamEnabled = true;
    public const int InputCommandsSent = 0;
}
