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

    /// <summary>
    /// Number of <see cref="TimingCategory.CaptureTransfer"/> samples stamped
    /// with this operation's name as the innermost enclosing named-operation
    /// scope. A capture taken inside a NESTED named operation counts toward
    /// that inner operation only, never toward this one too.
    /// </summary>
    public required int CaptureCount { get; init; }

    /// <summary>See <see cref="CaptureCount"/> for attribution rules.</summary>
    public required double CaptureMilliseconds { get; init; }

    /// <summary>
    /// Sum of <see cref="TimingCategory.InputGesture"/> samples stamped with
    /// this operation's name, same innermost-only attribution as <see cref="CaptureCount"/>.
    /// </summary>
    public required double InputMilliseconds { get; init; }
}

public sealed record ItemTimingSummary
{
    public required int Ordinal { get; init; }
    public required double TotalMilliseconds { get; init; }
    public required double CaptureMilliseconds { get; init; }
    public required double FixedDelayMilliseconds { get; init; }
    public required double OcrMilliseconds { get; init; }
    public required double InputMilliseconds { get; init; }

    /// <summary>
    /// Wall-clock time within this item not accounted for by capture, fixed
    /// delay, OCR or input-gesture samples: on-device app processing, tap/swipe
    /// dispatch overhead not covered by <see cref="InputMilliseconds"/>, and any
    /// other unmeasured work. Present so the per-item breakdown is self-explaining.
    /// </summary>
    public double ResidualMilliseconds =>
        TotalMilliseconds - (CaptureMilliseconds + FixedDelayMilliseconds + OcrMilliseconds + InputMilliseconds);
}

/// <summary>
/// The four-way wall-clock breakdown surfaced in the proof-summary Timing
/// section: device-to-host screen capture, fixed <c>Task.Delay</c> waits,
/// header OCR, and everything else (named-operation overhead, analysis, I/O).
/// </summary>
public sealed record TimingWallClockBreakdown
{
    public required double ScreenCapturePercent { get; init; }
    public required double InputPercent { get; init; }
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
    public required TimingCategorySummary Input { get; init; }
    public required IReadOnlyList<TimingOperationSummary> Operations { get; init; }
    public required IReadOnlyList<ItemTimingSummary> Items { get; init; }

    /// <summary>
    /// Each <see cref="TimingCategory"/>'s total milliseconds as a percentage
    /// of <see cref="WallClockMilliseconds"/>. <see cref="TimingCategory.NamedOperation"/>
    /// spans nest the finer-grained capture, fixed-delay and OCR samples taken
    /// during that named operation (e.g. a capture measured inside
    /// <c>AdvanceToNextPokemonInAppraisalAsync</c> is also included in that
    /// operation's own duration), so the percentages here can legitimately sum
    /// to more than 100%: they are independent per-category shares of wall
    /// clock, not mutually exclusive partitions of it.
    /// </summary>
    public required IReadOnlyDictionary<string, double> CategoryPercentOfWallClock { get; init; }

    public double PerItemMeanMilliseconds =>
        Items.Count == 0 ? 0 : Items.Average(item => item.TotalMilliseconds);

    public TimingWallClockBreakdown WallClockBreakdown()
    {
        var screenCapture = PercentOfWallClock(CaptureTransfer.TotalMilliseconds);
        var input = PercentOfWallClock(Input.TotalMilliseconds);
        var fixedWait = PercentOfWallClock(FixedDelay.TotalMilliseconds);
        var ocr = PercentOfWallClock(Ocr.TotalMilliseconds);
        return new TimingWallClockBreakdown
        {
            ScreenCapturePercent = screenCapture,
            InputPercent = input,
            FixedWaitPercent = fixedWait,
            OcrPercent = ocr,
            OtherPercent = Math.Max(0, 100 - screenCapture - input - fixedWait - ocr)
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
        Input = TimingCategorySummary.Empty,
        Operations = Array.Empty<TimingOperationSummary>(),
        Items = Array.Empty<ItemTimingSummary>(),
        CategoryPercentOfWallClock = new Dictionary<string, double>()
    };
}
