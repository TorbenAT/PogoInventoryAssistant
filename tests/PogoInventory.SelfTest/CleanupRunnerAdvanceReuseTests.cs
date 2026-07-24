namespace PogoInventory.SelfTest;

/// <summary>
/// Task 3, runner-level guard: <c>CleanupProofRunner</c> must hand the
/// just-captured appraisal (from <c>CaptureCleanupAppraisalAsync</c> /
/// <c>CaptureCurrentCleanupAppraisalAsync</c>) into
/// <c>AdvanceToNextPokemonInAppraisalAsync</c> as the pre-swipe reference for
/// the very next advance in the same per-item loop iteration, and the run
/// still completes with the same item/evidence outputs as before this wiring
/// existed (see <see cref="CleanupProofTests.RunCarouselLifecycleAsync"/>).
/// </summary>
internal static class CleanupRunnerAdvanceReuseTests
{
    public static async Task RunAsync()
    {
        var root = CleanupProofTests.CreateTemporaryDirectory();
        try
        {
            var evidence = await CleanupProofTests.CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var request = new PogoInventory.Application.CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = System.IO.Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true
            };
            var result = await new PogoInventory.Application.CleanupProofRunner().RunAsync(fake, request);

            AssertEqual(6, result.CapturedItems, "run still completes with all items");
            AssertEqual(5, fake.AdvanceCount, "same number of cursor progressions as before this change");
            AssertEqual(5, fake.ConfirmedPreSwipeCaptures.Count, "one confirmed-capture argument per advance call");

            // Every advance must receive the appraisal capture the runner just
            // captured for that same item, one step earlier in the loop -
            // never null, never a stale/different item's capture.
            for (var index = 0; index < fake.ConfirmedPreSwipeCaptures.Count; index++)
            {
                AssertTrue(
                    fake.ConfirmedPreSwipeCaptures[index] is not null,
                    $"advance #{index + 1} must receive the just-captured appraisal, not null");
                AssertTrue(
                    ReferenceEquals(fake.ConfirmedPreSwipeCaptures[index], fake.AppraisalCapturesReturned[index]),
                    $"advance #{index + 1} must receive the same appraisal object captured immediately before it in the same iteration");
            }
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
