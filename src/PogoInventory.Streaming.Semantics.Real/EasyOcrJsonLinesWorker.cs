using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace PogoInventory.Streaming.Semantics.Real;

public sealed record EasyOcrWorkerOptions(string PythonPath, string ScriptPath, int QueueCapacity = 1, TimeSpan? RequestTimeout = null)
{
    public TimeSpan EffectiveTimeout => RequestTimeout ?? TimeSpan.FromSeconds(2);
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PythonPath) || string.IsNullOrWhiteSpace(ScriptPath)) throw new ArgumentException("Python and worker paths are required.");
        if (QueueCapacity != 1) throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "The production worker queue is intentionally bounded to one.");
        if (EffectiveTimeout <= TimeSpan.Zero || EffectiveTimeout > TimeSpan.FromSeconds(6)) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
    }
}

public sealed record EasyOcrLine(string Text, double Confidence);
public sealed record EasyOcrResult(bool Succeeded, IReadOnlyList<EasyOcrLine> Lines, double LatencyMilliseconds, string? Error);

public sealed class EasyOcrJsonLinesWorker : IAsyncDisposable
{
    private readonly EasyOcrWorkerOptions _options;
    private readonly Channel<Request> _queue = Channel.CreateBounded<Request>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Process _process;
    private readonly Task _reader;
    private readonly Task _dispatcher;
    private readonly Dictionary<string, TaskCompletionSource<EasyOcrResult>> _pending = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private int _dropped;

    public EasyOcrJsonLinesWorker(EasyOcrWorkerOptions options)
    {
        _options = options;
        _options.Validate();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo(options.PythonPath)
            {
                Arguments = $"\"{options.ScriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _process.StartInfo.Environment["PYTHONPATH"] = Path.GetDirectoryName(options.ScriptPath) ?? string.Empty;
        if (!_process.Start()) throw new InvalidOperationException("EasyOCR worker did not start.");
        _reader = ReadResponsesAsync(_shutdown.Token);
        _dispatcher = DispatchAsync(_shutdown.Token);
    }

    public int DroppedRequests => Volatile.Read(ref _dropped);

    public async Task<EasyOcrResult> RecognizeAsync(byte[] png, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(png);
        cancellationToken.ThrowIfCancellationRequested();
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<EasyOcrResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _pending[id] = completion;
        if (!_queue.Writer.TryWrite(new Request(id, Convert.ToBase64String(png))))
        {
            lock (_gate) _pending.Remove(id);
            Interlocked.Increment(ref _dropped);
            return new(false, Array.Empty<EasyOcrLine>(), 0, "QUEUE_FULL");
        }
        try { return await completion.Task.WaitAsync(_options.EffectiveTimeout, cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException) { lock (_gate) _pending.Remove(id); return new(false, Array.Empty<EasyOcrLine>(), _options.EffectiveTimeout.TotalMilliseconds, "OCR_TIMEOUT"); }
        finally { lock (_gate) _pending.Remove(id); }
    }

    private async Task DispatchAsync(CancellationToken token)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            var json = JsonSerializer.Serialize(request);
            await _process.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(token).ConfigureAwait(false);
        }
    }

    private async Task ReadResponsesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_process.HasExited)
        {
            var line = await _process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false);
            if (line is null) break;
            try
            {
                using var json = JsonDocument.Parse(line);
                var root = json.RootElement;
                var id = root.GetProperty("id").GetString();
                if (id is null) continue;
                TaskCompletionSource<EasyOcrResult>? completion;
                lock (_gate) _pending.TryGetValue(id, out completion);
                if (completion is null) continue;
                var success = root.GetProperty("ok").GetBoolean();
                var latency = root.GetProperty("latencyMs").GetDouble();
                var lines = success ? root.GetProperty("lines").EnumerateArray().Select(item => new EasyOcrLine(item.GetProperty("text").GetString() ?? string.Empty, item.GetProperty("confidence").GetDouble())).ToArray() : Array.Empty<EasyOcrLine>();
                completion.TrySetResult(new(success, lines, latency, success ? null : root.GetProperty("error").GetString()));
            }
            catch (JsonException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try { await Task.WhenAll(_reader, _dispatcher).ConfigureAwait(false); } catch { }
        if (!_process.HasExited) { try { _process.Kill(entireProcessTree: true); } catch { } }
        _process.Dispose();
        _shutdown.Dispose();
    }

    private sealed record Request(string Id, string PngBase64);
}
