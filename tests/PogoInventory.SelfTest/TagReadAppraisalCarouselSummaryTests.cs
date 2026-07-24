using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.Persistence;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task 2, runner-level guard: when <c>ReadTagObservationAsync</c> reports the
/// Appraisal-carousel short circuit for ordinal-&gt;1 items, the run still
/// completes and <c>proof-summary.md</c> surfaces a skipped-tag-read count.
/// </summary>
internal static class TagReadAppraisalCarouselSummaryTests
{
    public static async Task RunAsync()
    {
        var root = CleanupProofTests.CreateTemporaryDirectory();
        try
        {
            var evidence = await CleanupProofTests.CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(
                evidence,
                partial: true,
                tagObservationOverride: ordinal => ordinal == 1
                    ? new VerifiedTagObservation { TagCount = 0, NamesComplete = true, Section = null }
                    : new VerifiedTagObservation
                    {
                        TagCount = 0,
                        NamesComplete = false,
                        Section = null,
                        Evidence = new[]
                        {
                            AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason
                        }
                    });
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

            AssertEqual(6, result.CapturedItems, "run completes with all items despite skipped tag reads");
            var rows = await new InventoryPersistenceService(request.DatabasePath)
                .LoadCleanupProofRowsAsync(result.RunId);
            var skippedRows = rows.Where(row =>
                row.Ordinal > 1 &&
                row.FieldEvidenceSources.TryGetValue("ExistingTags", out var existingTags) &&
                existingTags == AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason)
                .ToArray();
            AssertEqual(5, skippedRows.Length, "every ordinal>1 row is marked with the skip reason");
            var firstRow = rows.Single(row => row.Ordinal == 1);
            AssertTrue(
                firstRow.FieldEvidenceSources["ExistingTags"] !=
                    AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason,
                "item 1 (Details path) is not marked as skipped");

            var summary = await File.ReadAllTextAsync(Path.Combine(root, "proof-summary.md"));
            AssertTrue(
                summary.Contains("- Tag reads skipped (appraisal carousel): 5", StringComparison.Ordinal),
                "proof summary reports the skipped tag-read count");
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
