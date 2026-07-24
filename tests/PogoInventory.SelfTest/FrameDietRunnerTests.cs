using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.Persistence;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task D, runner-level guard: for ordinal&gt;1 items <c>CleanupProofRunner</c>
/// must (a) hand the just-captured appraisal-identity result into
/// <c>CaptureCurrentCleanupAppraisalAsync</c> so IV analysis reuses the
/// identity frames instead of opening a second identical capture window, and
/// (b) skip the <c>ReadTagObservationAsync</c> probe entirely (the identity
/// capture already confirmed the carousel), synthesizing the exact same
/// unavailable observation the probe's own short circuit would have
/// returned. Ordinal 1 (the Details flow) is unaffected: it still performs
/// the real tag probe/read. The run must still complete with the same
/// rows/evidence semantics as before this change (see
/// <see cref="CleanupProofTests.RunCarouselLifecycleAsync"/> and
/// <see cref="TagReadAppraisalCarouselSummaryTests"/>).
/// </summary>
internal static class FrameDietRunnerTests
{
    public static async Task RunAsync()
    {
        var root = CleanupProofTests.CreateTemporaryDirectory();
        try
        {
            var evidence = await CleanupProofTests.CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);

            AssertEqual(6, result.CapturedItems, "run still completes with all items");

            // Change 1b: the probe/read must fire only for ordinal 1 (the
            // Details flow); every later item skips it entirely because the
            // identity capture one step earlier already confirmed the
            // carousel.
            AssertEqual(1, fake.TagReadCallCount, "tag probe/read must be called exactly once (ordinal 1 only)");

            // Change 1a: every ordinal>1 current-appraisal capture must
            // receive the identity capture the runner just captured for that
            // same item, one step earlier in the loop - never null, never a
            // stale/different item's capture.
            AssertEqual(5, fake.ReceivedIdentityCaptures.Count, "one identity-capture argument per shared-frame IV call");
            AssertEqual(5, fake.AppraisalIdentityCapturesReturned.Count, "one appraisal-identity capture per ordinal>1 item");
            for (var index = 0; index < fake.ReceivedIdentityCaptures.Count; index++)
            {
                AssertTrue(
                    fake.ReceivedIdentityCaptures[index] is not null,
                    $"shared-frame IV call #{index + 1} must receive the just-captured identity, not null");
                AssertTrue(
                    ReferenceEquals(fake.ReceivedIdentityCaptures[index], fake.AppraisalIdentityCapturesReturned[index]),
                    $"shared-frame IV call #{index + 1} must receive the same identity object captured immediately before it in the same iteration");
            }

            var rows = await new InventoryPersistenceService(request.DatabasePath)
                .LoadCleanupProofRowsAsync(result.RunId);
            AssertEqual(6, rows.Count, "reloaded SQLite rows");
            var skippedRows = rows.Where(row =>
                row.Ordinal > 1 &&
                row.FieldEvidenceSources.TryGetValue("ExistingTags", out var existingTags) &&
                existingTags == AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason)
                .ToArray();
            AssertEqual(5, skippedRows.Length, "every ordinal>1 row is still marked with the skip reason despite the probe never running");
            var firstRow = rows.Single(row => row.Ordinal == 1);
            AssertTrue(
                firstRow.FieldEvidenceSources["ExistingTags"] != AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason,
                "item 1 (Details path) is not marked as skipped");
            AssertTrue(rows.All(row => !string.IsNullOrEmpty(row.StableFingerprint)), "every row still carries a non-empty stable fingerprint");

            var summary = await File.ReadAllTextAsync(Path.Combine(root, "proof-summary.md"));
            AssertTrue(
                summary.Contains("- Tag reads skipped (appraisal carousel): 5", StringComparison.Ordinal),
                "proof summary still reports the skipped tag-read count via DB rows/evidence, not probe call count");
        }
        finally
        {
            CleanupProofTests.DeleteDirectory(root);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

