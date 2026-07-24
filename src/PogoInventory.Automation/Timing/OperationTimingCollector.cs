using System.Diagnostics;

namespace PogoInventory.Automation.Timing;

public enum TimingCategory
{
    CaptureTransfer,
    NamedOperation,
    FixedDelay,
    Ocr,
    Item,
    InputGesture
}

public sealed record TimingSample
{
    public required TimingCategory Category { get; init; }
    public required string Name { get; init; }
    public required double ElapsedMilliseconds { get; init; }
    public long? Bytes { get; init; }
    public int? ItemOrdinal { get; init; }

    /// <summary>
    /// The innermost enclosing <see cref="TimingCategory.NamedOperation"/> name
    /// at the moment this sample was recorded, or null if no named-operation
    /// scope was active. Stamped from an ambient stack maintained by
    /// <see cref="OperationTimingCollector"/> so captures/input taken inside
    /// nested named operations attribute to the innermost one only.
    /// </summary>
    public string? OperationName { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }
}

public interface IOperationTimingCollector
{
    IDisposable Measure(TimingCategory category, string name);

    void RecordCapture(string label, double elapsedMilliseconds, long bytes);

    void RecordFixedDelay(string reason, int milliseconds);

    /// <summary>
    /// Records an on-device input gesture (swipe/tap) driven through the
    /// automation transport. No-op on <see cref="NullOperationTimingCollector"/>.
    /// </summary>
    void RecordInput(string label, double elapsedMilliseconds);

    /// <summary>
    /// Marks the start of processing an item.
    /// </summary>
    /// <remarks>
    /// Sequential-only contract: per-item attribution assumes items are processed strictly sequentially.
    /// Concurrent or overlapping Begin/End calls will misattribute timing samples.
    /// </remarks>
    void BeginItem(int ordinal);

    /// <summary>
    /// Marks the end of processing an item.
    /// </summary>
    /// <remarks>
    /// Sequential-only contract: per-item attribution assumes items are processed strictly sequentially.
    /// Concurrent or overlapping Begin/End calls will misattribute timing samples.
    /// </remarks>
    void EndItem(int ordinal);

    /// <summary>
    /// Restarts the wall-clock stopwatch so <see cref="TimingReport.WallClockMilliseconds"/>
    /// measures the run itself rather than the time since the collector was
    /// constructed (which, for the CLI, includes unwind/profile loading that
    /// happens before the run starts). Call once at the start of the run.
    /// Any samples already recorded (there should be none in practice) are
    /// kept; only the wall-clock reference point moves.
    /// </summary>
    void MarkRunStart();

    TimingReport BuildReport();
}

/// <summary>
/// Opt-in wall-clock timing collector for the cleanup-proof flow. Every
/// public member is lock-guarded so it can be shared across the sequential
/// named-operations and runner calls without external synchronization.
/// </summary>
/// <remarks>
/// Per-item attribution assumes items are processed sequentially: BeginItem/EndItem delimit one item at a time.
/// Concurrent item processing would misattribute samples recorded between overlapping Begin/End calls.
/// </remarks>
public sealed class OperationTimingCollector : IOperationTimingCollector
{
    private readonly object _lock = new();
    private readonly List<TimingSample> _samples = new();
    private readonly List<string> _operationNameStack = new();
    private Stopwatch _wallClock = Stopwatch.StartNew();
    private int? _currentItemOrdinal;
    private Stopwatch? _itemStopwatch;

    public IDisposable Measure(TimingCategory category, string name) =>
        new MeasureScope(this, category, name);

    public void RecordCapture(string label, double elapsedMilliseconds, long bytes) =>
        AddSample(TimingCategory.CaptureTransfer, label, elapsedMilliseconds, bytes);

    public void RecordFixedDelay(string reason, int milliseconds) =>
        AddSample(TimingCategory.FixedDelay, reason, milliseconds, null);

    public void RecordInput(string label, double elapsedMilliseconds) =>
        AddSample(TimingCategory.InputGesture, label, elapsedMilliseconds, null);

    public void BeginItem(int ordinal)
    {
        lock (_lock)
        {
            _currentItemOrdinal = ordinal;
            _itemStopwatch = Stopwatch.StartNew();
        }
    }

    public void EndItem(int ordinal)
    {
        lock (_lock)
        {
            var elapsed = _itemStopwatch?.Elapsed.TotalMilliseconds ?? 0;
            _samples.Add(new TimingSample
            {
                Category = TimingCategory.Item,
                Name = $"Item{ordinal}",
                ElapsedMilliseconds = elapsed,
                ItemOrdinal = ordinal,
                TimestampUtc = DateTimeOffset.UtcNow
            });
            _currentItemOrdinal = null;
            _itemStopwatch = null;
        }
    }

    public void MarkRunStart()
    {
        lock (_lock)
        {
            _wallClock = Stopwatch.StartNew();
        }
    }

    public TimingReport BuildReport()
    {
        List<TimingSample> samples;
        double wallClockMilliseconds;
        lock (_lock)
        {
            samples = new List<TimingSample>(_samples);
            wallClockMilliseconds = _wallClock.Elapsed.TotalMilliseconds;
        }

        if (samples.Count == 0)
            return TimingReport.Empty;

        var capture = Summarize(samples, TimingCategory.CaptureTransfer);
        var fixedDelay = Summarize(samples, TimingCategory.FixedDelay);
        var ocr = Summarize(samples, TimingCategory.Ocr);
        var input = Summarize(samples, TimingCategory.InputGesture);
        var operations = samples
            .Where(sample => sample.Category == TimingCategory.NamedOperation)
            .GroupBy(sample => sample.Name, StringComparer.Ordinal)
            .Select(group =>
            {
                // Captures/input taken while this operation was the innermost
                // active named-operation scope are stamped with its name; a
                // capture inside a NESTED operation counts toward that inner
                // operation only, never toward this outer one too.
                var ownedCaptures = samples
                    .Where(sample => sample.Category == TimingCategory.CaptureTransfer && sample.OperationName == group.Key)
                    .ToArray();
                var ownedInput = samples
                    .Where(sample => sample.Category == TimingCategory.InputGesture && sample.OperationName == group.Key)
                    .ToArray();
                return new TimingOperationSummary
                {
                    Name = group.Key,
                    Count = group.Count(),
                    TotalMilliseconds = group.Sum(sample => sample.ElapsedMilliseconds),
                    MeanMilliseconds = group.Average(sample => sample.ElapsedMilliseconds),
                    CaptureCount = ownedCaptures.Length,
                    CaptureMilliseconds = ownedCaptures.Sum(sample => sample.ElapsedMilliseconds),
                    InputMilliseconds = ownedInput.Sum(sample => sample.ElapsedMilliseconds)
                };
            })
            .OrderBy(summary => summary.Name, StringComparer.Ordinal)
            .ToArray();
        var items = samples
            .Where(sample => sample.Category == TimingCategory.Item)
            .OrderBy(sample => sample.ItemOrdinal)
            .Select(sample => new ItemTimingSummary
            {
                Ordinal = sample.ItemOrdinal ?? 0,
                TotalMilliseconds = sample.ElapsedMilliseconds,
                CaptureMilliseconds = SumForItem(samples, TimingCategory.CaptureTransfer, sample.ItemOrdinal),
                FixedDelayMilliseconds = SumForItem(samples, TimingCategory.FixedDelay, sample.ItemOrdinal),
                OcrMilliseconds = SumForItem(samples, TimingCategory.Ocr, sample.ItemOrdinal),
                InputMilliseconds = SumForItem(samples, TimingCategory.InputGesture, sample.ItemOrdinal)
            })
            .ToArray();
        var namedOperationTotal = operations.Sum(summary => summary.TotalMilliseconds);
        var percentages = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [nameof(TimingCategory.CaptureTransfer)] = PercentOf(capture.TotalMilliseconds, wallClockMilliseconds),
            [nameof(TimingCategory.FixedDelay)] = PercentOf(fixedDelay.TotalMilliseconds, wallClockMilliseconds),
            [nameof(TimingCategory.Ocr)] = PercentOf(ocr.TotalMilliseconds, wallClockMilliseconds),
            [nameof(TimingCategory.NamedOperation)] = PercentOf(namedOperationTotal, wallClockMilliseconds),
            [nameof(TimingCategory.InputGesture)] = PercentOf(input.TotalMilliseconds, wallClockMilliseconds)
        };

        return new TimingReport
        {
            IsEmpty = false,
            WallClockMilliseconds = wallClockMilliseconds,
            CaptureTransfer = capture,
            FixedDelay = fixedDelay,
            Ocr = ocr,
            Input = input,
            Operations = operations,
            Items = items,
            CategoryPercentOfWallClock = percentages
        };
    }

    private void AddSample(TimingCategory category, string name, double elapsedMilliseconds, long? bytes)
    {
        lock (_lock)
        {
            _samples.Add(new TimingSample
            {
                Category = category,
                Name = name,
                ElapsedMilliseconds = elapsedMilliseconds,
                Bytes = bytes,
                ItemOrdinal = _currentItemOrdinal,
                OperationName = _operationNameStack.Count > 0 ? _operationNameStack[^1] : null,
                TimestampUtc = DateTimeOffset.UtcNow
            });
        }
    }

    private void PushOperationName(string name)
    {
        lock (_lock)
        {
            _operationNameStack.Add(name);
        }
    }

    private void PopOperationName()
    {
        lock (_lock)
        {
            if (_operationNameStack.Count > 0)
                _operationNameStack.RemoveAt(_operationNameStack.Count - 1);
        }
    }

    private static TimingCategorySummary Summarize(IReadOnlyList<TimingSample> samples, TimingCategory category)
    {
        var matching = samples.Where(sample => sample.Category == category).ToArray();
        if (matching.Length == 0)
            return TimingCategorySummary.Empty;

        return new TimingCategorySummary
        {
            Count = matching.Length,
            TotalMilliseconds = matching.Sum(sample => sample.ElapsedMilliseconds),
            MeanMilliseconds = matching.Average(sample => sample.ElapsedMilliseconds),
            MinMilliseconds = matching.Min(sample => sample.ElapsedMilliseconds),
            MaxMilliseconds = matching.Max(sample => sample.ElapsedMilliseconds),
            TotalBytes = matching.Sum(sample => sample.Bytes ?? 0)
        };
    }

    private static double SumForItem(IReadOnlyList<TimingSample> samples, TimingCategory category, int? ordinal) =>
        samples.Where(sample => sample.Category == category && sample.ItemOrdinal == ordinal)
            .Sum(sample => sample.ElapsedMilliseconds);

    private static double PercentOf(double part, double whole) =>
        whole <= 0 ? 0 : part / whole * 100.0;

    private sealed class MeasureScope : IDisposable
    {
        private readonly OperationTimingCollector _owner;
        private readonly TimingCategory _category;
        private readonly string _name;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public MeasureScope(OperationTimingCollector owner, TimingCategory category, string name)
        {
            _owner = owner;
            _category = category;
            _name = name;
            if (_category == TimingCategory.NamedOperation)
                _owner.PushOperationName(_name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stopwatch.Stop();
            // Record before popping so this operation's own duration sample is
            // stamped with itself as the innermost name (consistent with any
            // capture/input samples recorded during its body).
            _owner.AddSample(_category, _name, _stopwatch.Elapsed.TotalMilliseconds, null);
            if (_category == TimingCategory.NamedOperation)
                _owner.PopOperationName();
        }
    }
}

/// <summary>
/// Inert <see cref="IOperationTimingCollector"/> used as the default so
/// timing instrumentation never changes behavior unless a real collector is
/// explicitly passed in.
/// </summary>
public sealed class NullOperationTimingCollector : IOperationTimingCollector
{
    public static readonly NullOperationTimingCollector Instance = new();

    private static readonly IDisposable NoopScope = new NoopDisposable();

    private NullOperationTimingCollector()
    {
    }

    public IDisposable Measure(TimingCategory category, string name) => NoopScope;

    public void RecordCapture(string label, double elapsedMilliseconds, long bytes)
    {
    }

    public void RecordFixedDelay(string reason, int milliseconds)
    {
    }

    public void RecordInput(string label, double elapsedMilliseconds)
    {
    }

    public void BeginItem(int ordinal)
    {
    }

    public void EndItem(int ordinal)
    {
    }

    public void MarkRunStart()
    {
    }

    public TimingReport BuildReport() => TimingReport.Empty;

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
