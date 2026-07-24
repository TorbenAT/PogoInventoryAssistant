using PogoInventory.Application;
using PogoInventory.Automation.Timing;

namespace PogoInventory.SelfTest;

/// <summary>
/// Tests for the opt-in cleanup-proof timing instrumentation:
/// <see cref="OperationTimingCollector"/>, <see cref="NullOperationTimingCollector"/>
/// and the runner wiring that writes <c>timing-report.json</c> and the
/// <c>## Timing</c> proof-summary section only when a real collector supplied
/// non-empty samples.
/// </summary>
internal static class OperationTimingCollectorTests
{
    public static async Task RunAsync()
    {
        await CaptureAggregationSumsMillisecondsAndBytesAsync();
        await FixedDelayAccumulatesPerReasonAsync();
        await PerItemAttributionSplitsCaptureDelayOcrAsync();
        await PercentagesAreDerivedAndBoundedAsync();
        await NullCollectorIsInertAndReportEmptyAsync();
        await RunnerWritesTimingReportAndSummarySectionAsync();
        await MarkRunStartRestartsWallClockAsync();
        await AdvanceTimeIsAttributedToTheAdvancingItemAsync();
    }

    public static async Task MarkRunStartRestartsWallClockAsync()
    {
        var collector = new OperationTimingCollector();
        // Simulate pre-run work (unwind/profile loading in the CLI) happening
        // before the collector is told the run has actually started.
        await Task.Delay(250);
        collector.MarkRunStart();
        collector.RecordCapture("screencap", 5, 100);
        var report = collector.BuildReport();
        AssertTrue(
            report.WallClockMilliseconds < 200,
            $"MarkRunStart must restart the wall clock so pre-run work is excluded, got {report.WallClockMilliseconds:F0} ms");
    }

    public static async Task AdvanceTimeIsAttributedToTheAdvancingItemAsync()
    {
        var root = CleanupProofTests.CreateTemporaryDirectory();
        try
        {
            var evidence = await CleanupProofTests.CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(
                evidence, partial: true,
                beforeAdvance: () => Task.Delay(80));
            var timing = new OperationTimingCollector();
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true
            };
            await new CleanupProofRunner().RunAsync(fake, request, timing: timing);
            var report = timing.BuildReport();
            AssertEqual(6, report.Items.Count, "one item summary per captured item");

            // Items 1-5 each advance to the next Pokémon with an injected
            // 80 ms delay; that delay must land in the item it advances FROM.
            foreach (var item in report.Items.Where(entry => entry.Ordinal < 6))
                AssertTrue(
                    item.TotalMilliseconds >= 70,
                    $"item {item.Ordinal} total must include the advance delay, got {item.TotalMilliseconds:F0} ms");

            // Item 6 is the last item: the loop never advances past it, so it
            // must not pick up any injected advance delay.
            var last = report.Items.Single(entry => entry.Ordinal == 6);
            AssertTrue(
                last.TotalMilliseconds < 70,
                $"the last item never advances and must not include any injected delay, got {last.TotalMilliseconds:F0} ms");
        }
        finally
        {
            CleanupProofTests.DeleteDirectory(root);
        }
    }

    public static Task CaptureAggregationSumsMillisecondsAndBytesAsync()
    {
        var collector = new OperationTimingCollector();
        collector.RecordCapture("screencap", 100, 1000);
        collector.RecordCapture("screencap", 200, 2000);
        var report = collector.BuildReport();
        AssertEqual(2, report.CaptureTransfer.Count, "capture sample count");
        AssertEqual(300.0, report.CaptureTransfer.TotalMilliseconds, "capture total ms");
        AssertEqual(3000L, report.CaptureTransfer.TotalBytes, "capture total bytes");
        AssertEqual(150.0, report.CaptureTransfer.MeanMilliseconds, "capture mean ms");
        return Task.CompletedTask;
    }

    public static Task FixedDelayAccumulatesPerReasonAsync()
    {
        var collector = new OperationTimingCollector();
        collector.RecordFixedDelay("StatePoll", 350);
        collector.RecordFixedDelay("StatePoll", 350);
        collector.RecordFixedDelay("PostActionSettle", 250);
        var report = collector.BuildReport();
        AssertEqual(3, report.FixedDelay.Count, "fixed delay sample count");
        AssertEqual(950.0, report.FixedDelay.TotalMilliseconds, "fixed delay total ms");
        AssertEqual(0, report.Operations.Count, "operations must not include fixed-delay samples");
        return Task.CompletedTask;
    }

    public static Task PerItemAttributionSplitsCaptureDelayOcrAsync()
    {
        var collector = new OperationTimingCollector();
        collector.BeginItem(1);
        collector.RecordCapture("screencap", 40, 500);
        collector.RecordFixedDelay("PostActionSettle", 250);
        using (collector.Measure(TimingCategory.Ocr, "HeaderFrame"))
        {
        }
        collector.EndItem(1);

        collector.BeginItem(2);
        collector.RecordCapture("screencap", 60, 700);
        collector.EndItem(2);

        var report = collector.BuildReport();
        AssertEqual(2, report.Items.Count, "item summary count");
        var first = report.Items.Single(item => item.Ordinal == 1);
        var second = report.Items.Single(item => item.Ordinal == 2);
        AssertEqual(40.0, first.CaptureMilliseconds, "item 1 capture ms");
        AssertEqual(250.0, first.FixedDelayMilliseconds, "item 1 fixed delay ms");
        AssertTrue(first.OcrMilliseconds >= 0, "item 1 ocr ms recorded");
        AssertEqual(60.0, second.CaptureMilliseconds, "item 2 capture ms");
        AssertEqual(0.0, second.FixedDelayMilliseconds, "item 2 has no fixed delay");
        return Task.CompletedTask;
    }

    public static Task PercentagesAreDerivedAndBoundedAsync()
    {
        var collector = new OperationTimingCollector();
        collector.RecordCapture("screencap", 100, 1000);
        collector.RecordFixedDelay("PostActionSettle", 50);
        using (collector.Measure(TimingCategory.Ocr, "HeaderFrame"))
        {
        }
        using (collector.Measure(TimingCategory.NamedOperation, "OpenFirstPokemonAsync"))
        {
        }

        var first = collector.BuildReport();
        var second = collector.BuildReport();
        foreach (var percentage in first.CategoryPercentOfWallClock.Values)
            AssertTrue(percentage >= 0, "percentage must be non-negative");
        AssertEqual(first.CategoryPercentOfWallClock.Count, second.CategoryPercentOfWallClock.Count,
            "percentage key set is deterministic across BuildReport calls");
        AssertEqual(first.CaptureTransfer.TotalMilliseconds, second.CaptureTransfer.TotalMilliseconds,
            "recorded totals are stable across BuildReport calls");
        return Task.CompletedTask;
    }

    public static Task NullCollectorIsInertAndReportEmptyAsync()
    {
        var collector = NullOperationTimingCollector.Instance;
        using (collector.Measure(TimingCategory.Ocr, "HeaderFrame"))
        {
        }
        collector.RecordCapture("screencap", 100, 1000);
        collector.RecordFixedDelay("PostActionSettle", 250);
        collector.BeginItem(1);
        collector.EndItem(1);
        var report = collector.BuildReport();
        AssertTrue(report.IsEmpty, "null collector report must be empty");
        AssertEqual(0, report.Operations.Count, "null collector has no operations");
        AssertEqual(0, report.Items.Count, "null collector has no items");
        return Task.CompletedTask;
    }

    public static async Task RunnerWritesTimingReportAndSummarySectionAsync()
    {
        var withTimingRoot = CleanupProofTests.CreateTemporaryDirectory();
        var withoutTimingRoot = CleanupProofTests.CreateTemporaryDirectory();
        try
        {
            var evidence = Path.Combine(withTimingRoot, "evidence.png");
            await File.WriteAllBytesAsync(evidence, CleanupProofTests.FixtureBytes());
            var fakeWithTiming = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var timing = new OperationTimingCollector();
            var requestWithTiming = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(withTimingRoot, "cleanup-proof.sqlite"),
                OutputDirectory = withTimingRoot,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true
            };
            await new CleanupProofRunner().RunAsync(fakeWithTiming, requestWithTiming, timing: timing);
            var timingReportPath = Path.Combine(withTimingRoot, "timing-report.json");
            var proofSummaryPath = Path.Combine(withTimingRoot, "proof-summary.md");
            AssertTrue(File.Exists(timingReportPath), "timing-report.json must exist when a collector is supplied");
            var summary = await File.ReadAllTextAsync(proofSummaryPath);
            AssertTrue(summary.Contains("## Timing", StringComparison.Ordinal), "proof-summary.md must contain a Timing section");

            var evidenceNoTiming = Path.Combine(withoutTimingRoot, "evidence.png");
            await File.WriteAllBytesAsync(evidenceNoTiming, CleanupProofTests.FixtureBytes());
            var fakeNoTiming = new CleanupProofTests.FakeCleanupOperations(evidenceNoTiming, partial: true);
            var requestNoTiming = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(withoutTimingRoot, "cleanup-proof.sqlite"),
                OutputDirectory = withoutTimingRoot,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true
            };
            await new CleanupProofRunner().RunAsync(fakeNoTiming, requestNoTiming);
            AssertTrue(
                !File.Exists(Path.Combine(withoutTimingRoot, "timing-report.json")),
                "timing-report.json must not exist without a collector");
            var summaryNoTiming = await File.ReadAllTextAsync(Path.Combine(withoutTimingRoot, "proof-summary.md"));
            AssertTrue(
                !summaryNoTiming.Contains("## Timing", StringComparison.Ordinal),
                "proof-summary.md must not contain a Timing section without a collector");
        }
        finally
        {
            CleanupProofTests.DeleteDirectory(withTimingRoot);
            CleanupProofTests.DeleteDirectory(withoutTimingRoot);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {message} to be '{expected}', got '{actual}'.");
    }
}
