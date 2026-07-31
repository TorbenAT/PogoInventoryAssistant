using PogoInventory.Semantics;

namespace PogoInventory.SelfTest;

internal static class StreamProofReporterTests
{
    public static Task IncompleteHundredOf120FailsIntegrityAsync()
    {
        var runId = "stream-test-incomplete";
        var records = Enumerable.Range(1, 100)
            .Select(ordinal => Record(runId, ordinal))
            .ToArray();
        var handoffs = records.Select((record, index) =>
            Handoff(record, index == 0 ? null : records[index - 1].ItemFingerprint))
            .ToArray();
        var report = StreamPokemonProofReporter.Validate(
            EmptyDirectory(), Context(runId, StreamProofRunStatus.SafeStopped),
            records, handoffs);
        AssertFailed(report, "CompletedRequestedItems");
        return Task.CompletedTask;
    }

    public static Task SemanticFallbackNeedsKnownReasonAsync()
    {
        var runId = "stream-test-semantic-fallback";
        var records = new[] { Record(runId, 1), Record(runId, 2) };
        var handoffs = new[]
        {
            Handoff(records[0], null),
            Handoff(
                records[1],
                records[0].ItemFingerprint,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["SemanticProgressionProofRequired"] = 1
                })
        };
        var report = StreamPokemonProofReporter.Validate(
            EmptyDirectory(), Context(runId, StreamProofRunStatus.CompletedRequestedItems),
            records, handoffs);
        AssertFailed(report, "ProgressionAlignmentAndSemanticFallback");
        return Task.CompletedTask;
    }

    public static Task RunIdMismatchFailsIntegrityAsync()
    {
        var record = Record("wrong-run", 1);
        var report = StreamPokemonProofReporter.Validate(
            EmptyDirectory(),
            Context("expected-run", StreamProofRunStatus.CompletedRequestedItems),
            [record],
            [Handoff(record, null)]);
        AssertFailed(report, "RunIdConsistent");
        return Task.CompletedTask;
    }

    public static Task SemanticIdentityDisambiguatesVisualCollisionAsync()
    {
        var first = Record("run", 1);
        var second = Record("run", 2);
        var visual = "AppraisalPanel:0000000000000001";
        var firstFingerprint =
            StreamPokemonProofReporter.BuildItemFingerprint(
                visual, first.Result);
        var secondFingerprint =
            StreamPokemonProofReporter.BuildItemFingerprint(
                visual, second.Result);
        if (string.Equals(
            firstFingerprint, secondFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Known semantic identity did not disambiguate a visual collision.");
        }

        var repeatedFingerprint =
            StreamPokemonProofReporter.BuildItemFingerprint(
                visual, first.Result);
        if (!string.Equals(
            firstFingerprint, repeatedFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Composite item fingerprint is not deterministic.");
        }

        return Task.CompletedTask;
    }

    private static StreamProofRecord Record(string runId, int ordinal)
    {
        var captured = DateTimeOffset.Parse(
            $"2026-07-30T12:{ordinal % 60:00}:10Z",
            System.Globalization.CultureInfo.InvariantCulture);
        var frameIds = new[]
        {
            ordinal * 10L + 1,
            ordinal * 10L + 3,
            ordinal * 10L + 5
        };
        var timestamps = frameIds.Select((_, index) =>
            captured.AddMilliseconds(index * 100)).ToArray();
        var knownString = new SemanticFieldResult<string>(
            "Pikachu", SemanticFieldStatus.Known, 1, frameIds, ["a", "b", "c"], []);
        var knownInt = new SemanticFieldResult<int?>(
            100 + ordinal, SemanticFieldStatus.Known, 1, frameIds, ["a", "b", "c"], []);
        var result = new PokemonItemSemanticResult(
            $"stream:{ordinal:000000}",
            knownString,
            knownInt,
            knownInt with { Value = 10 },
            knownInt with { Value = 11 },
            knownInt with { Value = 12 },
            true,
            new Dictionary<string, double>(StringComparer.Ordinal));
        var root = $"items/item-{ordinal:000}";
        return new(
            runId,
            ordinal,
            captured.AddMilliseconds(300),
            $"Header:{ordinal:x16}",
            result,
            result,
            ["cp100", "Pikachu"],
            10,
            10,
            20,
            frameIds,
            timestamps,
            ["a", "b", "c"],
            [$"{root}/a.png", $"{root}/b.png", $"{root}/c.png"],
            $"{root}/a.png",
            $"{root}/header.png",
            $"{root}/appraisal.png",
            $"{root}/attack.png",
            $"{root}/defense.png",
            $"{root}/hp.png",
            100,
            ordinal == 1 ? null : 200,
            new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static StreamProofHandoff Handoff(
        StreamProofRecord record,
        string? previous,
        IReadOnlyDictionary<string, int>? reasons = null) =>
        new(
            record.Ordinal,
            3,
            0,
            0,
            reasons ?? new Dictionary<string, int>(StringComparer.Ordinal),
            3,
            100,
            previous,
            record.ItemFingerprint,
            previous is null ? null : record.ItemFingerprint,
            record.FrameIds[0] - 1,
            record.FrameTimestampsUtc[0].AddSeconds(-2),
            record.FrameTimestampsUtc[0].AddSeconds(-1),
            null);

    private static StreamProofContext Context(
        string runId,
        StreamProofRunStatus status) =>
        new(
            runId,
            "commit",
            "device",
            "cp10-",
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-30T13:00:00Z"),
            120,
            status,
            status == StreamProofRunStatus.CompletedRequestedItems ? null : "test-stop",
            1,
            0,
            0,
            0,
            new(
                300,
                300,
                0,
                120,
                1,
                1,
                886,
                1920,
                0,
                false,
                "Stopped",
                0,
                "Clean",
                null));

    private static string EmptyDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "pogo-stream-proof-selftest",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertFailed(
        StreamProofIntegrity report,
        string checkName)
    {
        var check = report.Checks.Single(x =>
            string.Equals(x.Name, checkName, StringComparison.Ordinal));
        if (check.Passed)
        {
            throw new InvalidOperationException(
                $"Integrity check '{checkName}' unexpectedly passed.");
        }
    }
}
