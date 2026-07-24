namespace PogoInventory.Automation.Timing;

public sealed record TimingCategorySummary
{
    public required int Count { get; init; }
    public required double TotalMilliseconds { get; init; }
    public required double MeanMilliseconds { get; init; }
    public required double MinMilliseconds { get; init; }
    public required double MaxMilliseconds { get; init; }
    public required long TotalBytes { get; init; }

    public static readonly TimingCategorySummary Empty = new()
    {
        Count = 0,
        TotalMilliseconds = 0,
        MeanMilliseconds = 0,
        MinMilliseconds = 0,
        MaxMilliseconds = 0,
        TotalBytes = 0
    };
}

public sealed record TimingOperationSummary
{
    public required string Name { get; init; }
    public required int Count { get; init; }
    public required double TotalMilliseconds { get; init; }
    public required double MeanMilliseconds { get; init; }
}

public sealed record ItemTimingSummary
{
    public required int Ordinal { get; init; }
    public required double TotalMilliseconds { get; init; }
    public required double CaptureMilliseconds { get; init; }
    public required double FixedDelayMilliseconds { get; init; }
    public required double OcrMilliseconds { get; init; }
}

/// <summary>
/// The four-way wall-clock breakdown surfaced in the proof-summary Timing
/// section: device-to-host screen capture, fixed <c>Task.Delay</c> waits,
/// header OCR, and everything else (named-operation overhead, analysis, I/O).
/// </summary>
public sealed record TimingWallClockBreakdown
{
    public required double ScreenCapturePercent { get; init; }
    public required double FixedWaitPercent { get; init; }
    public required double OcrPercent { get; init; }
    public required double OtherPercent { get; init; }
}

public sealed record TimingReport
{
    public required bool IsEmpty { get; init; }
    public required double WallClockMilliseconds { get; init; }
    public required TimingCategorySummary CaptureTransfer { get; init; }
    public required TimingCategorySummary FixedDelay { get; init; }
    public required TimingCategorySummary Ocr { get; init; }
    public required IReadOnlyList<TimingOperationSummary> Operations { get; init; }
    public required IReadOnlyList<ItemTimingSummary> Items { get; init; }
    public required IReadOnlyDictionary<string, double> CategoryPercentOfWallClock { get; init; }

    public double PerItemMeanMilliseconds =>
        Items.Count == 0 ? 0 : Items.Average(item => item.TotalMilliseconds);

    public TimingWallClockBreakdown WallClockBreakdown()
    {
        var screenCapture = PercentOfWallClock(CaptureTransfer.TotalMilliseconds);
        var fixedWait = PercentOfWallClock(FixedDelay.TotalMilliseconds);
        var ocr = PercentOfWallClock(Ocr.TotalMilliseconds);
        return new TimingWallClockBreakdown
        {
            ScreenCapturePercent = screenCapture,
            FixedWaitPercent = fixedWait,
            OcrPercent = ocr,
            OtherPercent = Math.Max(0, 100 - screenCapture - fixedWait - ocr)
        };
    }

    private double PercentOfWallClock(double part) =>
        WallClockMilliseconds <= 0 ? 0 : part / WallClockMilliseconds * 100.0;

    public static readonly TimingReport Empty = new()
    {
        IsEmpty = true,
        WallClockMilliseconds = 0,
        CaptureTransfer = TimingCategorySummary.Empty,
        FixedDelay = TimingCategorySummary.Empty,
        Ocr = TimingCategorySummary.Empty,
        Operations = Array.Empty<TimingOperationSummary>(),
        Items = Array.Empty<ItemTimingSummary>(),
        CategoryPercentOfWallClock = new Dictionary<string, double>()
    };
}
