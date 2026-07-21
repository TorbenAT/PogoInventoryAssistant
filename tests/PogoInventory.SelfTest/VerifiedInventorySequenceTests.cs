using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class VerifiedInventorySequenceTests
{
    public static async Task RunAsync()
    {
        var fake = new FakeOperations();
        var output = Path.Combine(Path.GetTempPath(), "pogo-sequence-selftest", Guid.NewGuid().ToString("N"));
        var result = await new VerifiedInventoryTaskSequence(fake).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 2, ApplyClassificationTag = false, OutputDirectory = output
        }, "run-sequence-test");
        Assert(result.Checkpoint.Items.Count == 2, "item limit");
        Assert(result.Checkpoint.State == VerifiedSequenceState.Completed, "completed checkpoint");
        Assert(File.Exists(result.CheckpointPath), "checkpoint written");
        Assert(fake.Calls.All(call => !call.Contains("adb", StringComparison.OrdinalIgnoreCase)), "no raw adb");
        Assert(fake.Calls.Count(call => call == "open-first") == 1, "first card opened once");
        Assert(fake.Calls.Count(call => call == "advance") == 1, "one cursor advance for two items");
        Assert(fake.Calls.Count(call => call == "return") == 1, "normal loop returns only at end");
        var resumed = await new VerifiedInventoryTaskSequence(new FakeOperations()).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 2, ApplyClassificationTag = false, OutputDirectory = output
        }, "different-run-id");
        Assert(resumed.Checkpoint.Items.Select(item => item.Ordinal).SequenceEqual(new[] { 1, 2 }), "resume keeps ordinals");

        var mismatchOutput = Path.Combine(output, "mismatch-resume");
        await new VerifiedInventoryTaskSequence(new FakeOperations()).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ControlledStopAfter = 2, Resume = false,
            ApplyClassificationTag = false, OutputDirectory = mismatchOutput
        }, "mismatch-run-id");
        var mismatch = new FakeOperations { Fingerprint = "different" };
        var mismatchResult = await new VerifiedInventoryTaskSequence(mismatch).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ApplyClassificationTag = false, OutputDirectory = mismatchOutput
        }, "different-run-id");
        Assert(mismatchResult.Checkpoint.State == VerifiedSequenceState.TerminalUnknown, "resume overlap mismatch stops");
        Assert(mismatch.Calls.Count(call => call == "advance") == 1,
            "resume mismatch performs only the verified replay swipe");

        var partial = new FakeOperations
        {
            IdentityStatus = PokemonIdentityObservationStatus.Partial,
            ReturnState = VerifiedSequenceState.Partial,
            AdvanceState = VerifiedSequenceState.Partial
        };
        var partialResult = await new VerifiedInventoryTaskSequence(partial).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ApplyClassificationTag = false, OutputDirectory = Path.Combine(output, "partial")
        }, "run-partial-test");
        Assert(partialResult.Checkpoint.State == VerifiedSequenceState.TerminalFailure, "partial stops safely");

        var partialThenComplete = new FakeOperations
        {
            IdentitySequence = new[]
            {
                PokemonIdentityObservationStatus.Partial,
                PokemonIdentityObservationStatus.Complete
            }
        };
        var continued = await new VerifiedInventoryTaskSequence(partialThenComplete).RunAsync(
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 2, ApplyClassificationTag = false,
                OutputDirectory = Path.Combine(output, "partial-continue")
            },
            "run-partial-continue-test");
        Assert(continued.Checkpoint.Items.Count == 2, "partial item does not stop later items");
        Assert(continued.Checkpoint.Items[0].State == VerifiedSequenceState.Partial,
            "partial item is preserved");
        Assert(continued.Checkpoint.State == VerifiedSequenceState.Completed,
            "partial continuation completes safely");
        Assert(partialThenComplete.Calls.Count(call => call == "advance") == 1,
            "partial and complete items advance on the details cursor");
        Assert(partialThenComplete.Calls.Count(call => call == "open-first") == 1,
            "first card is opened once");

        var unknown = new FakeOperations { OpenState = VerifiedSequenceState.Unknown };
        var unknownResult = await new VerifiedInventoryTaskSequence(unknown).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ApplyClassificationTag = false, OutputDirectory = Path.Combine(output, "unknown")
        }, "run-unknown-test");
        Assert(unknownResult.Checkpoint.State == VerifiedSequenceState.TerminalUnknown, "unknown stop");
        Assert(unknown.Calls.Last() == "open-first", "no input after unknown");
        var rejectedDelete = false;
        try
        {
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 1, ApplyClassificationTag = true,
                ClassificationTag = "AI-Delete", OutputDirectory = output
            }.Validate();
        }
        catch (ArgumentException) { rejectedDelete = true; }
        Assert(rejectedDelete, "AI-Delete cannot be auto-applied");

        var controlledOutput = Path.Combine(output, "controlled-resume");
        var firstStop = await new VerifiedInventoryTaskSequence(new FakeOperations()).RunAsync(
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 3, ControlledStopAfter = 2,
                Resume = false, ApplyClassificationTag = false, OutputDirectory = controlledOutput
            }, "controlled-resume-test");
        Assert(firstStop.Checkpoint.State == VerifiedSequenceState.ControlledStopped,
            "controlled stop is resumable");
        Assert(firstStop.Checkpoint.Items.Select(item => item.Ordinal).SequenceEqual(new[] { 1, 2 }),
            "controlled stop records only ordinals one and two");
        var resumeOperations = new FakeOperations();
        var resumedControlled = await new VerifiedInventoryTaskSequence(resumeOperations).RunAsync(
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 3, Resume = true,
                ApplyClassificationTag = false, OutputDirectory = controlledOutput
            }, "ignored-run-id");
        Assert(resumedControlled.Checkpoint.State == VerifiedSequenceState.Completed,
            "controlled resume completes");
        Assert(resumedControlled.Checkpoint.Items.Select(item => item.Ordinal).SequenceEqual(new[] { 1, 2, 3 }),
            "controlled resume keeps exact ordinals");
        Assert(resumedControlled.Checkpoint.Items.Select(item => item.StableFingerprintSha256)
            .Distinct(StringComparer.Ordinal).Count() == 1 &&
            resumedControlled.Checkpoint.Items.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count() == 3,
            "identical fingerprints remain separate resumed instances");
        Assert(resumeOperations.Calls.Count(call => call == "open-first") == 1,
            "controlled resume opens first card once for replay");
        Assert(resumeOperations.Calls.Count(call => call == "advance") == 2,
            "controlled resume has one replay and one progression swipe");
        Assert(resumeOperations.Calls.Count(call => call == "return") == 1,
            "controlled resume returns only after completion");

        var completedReplay = new FakeOperations();
        var completedAgain = await new VerifiedInventoryTaskSequence(completedReplay).RunAsync(
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 3, Resume = true,
                ApplyClassificationTag = false, OutputDirectory = controlledOutput
            }, "ignored-run-id");
        Assert(completedAgain.Checkpoint.Items.Count == 3 && completedReplay.Calls.Count == 0,
            "completed checkpoints are idempotent");
    }

    private sealed class FakeOperations : IVerifiedInventoryNamedOperations
    {
        public PokemonIdentityObservationStatus IdentityStatus { get; init; } = PokemonIdentityObservationStatus.Complete;
        public IReadOnlyList<PokemonIdentityObservationStatus>? IdentitySequence { get; init; }
        public VerifiedSequenceState OpenState { get; init; } = VerifiedSequenceState.PokemonDetails;
        public VerifiedSequenceState AdvanceState { get; init; } = VerifiedSequenceState.PokemonDetails;
        public VerifiedSequenceState ReturnState { get; init; } = VerifiedSequenceState.Inventory;
        public string Fingerprint { get; init; } = "fp";
        public List<string> Calls { get; } = new();
        private int _identityIndex;
        public Task<VerifiedSequenceState> EnsureFilteredInventoryAsync(string query, CancellationToken cancellationToken) { Calls.Add("ensure"); return Task.FromResult(VerifiedSequenceState.Inventory); }
        public Task<VerifiedSequenceState> OpenFirstPokemonAsync(CancellationToken cancellationToken) { Calls.Add("open-first"); return Task.FromResult(OpenState); }
        public Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken)
        {
            Calls.Add("identity");
            var status = IdentitySequence is { Count: > 0 } && _identityIndex < IdentitySequence.Count
                ? IdentitySequence[_identityIndex++]
                : IdentityStatus;
            return Task.FromResult(new PokemonIdentityConsensus
            {
                Status = status,
                StableFingerprintSha256 = status == PokemonIdentityObservationStatus.Complete ? Fingerprint : "",
                StableFingerprintBase64 = "AA==",
                Confidence = 1,
                Frames = Array.Empty<PokemonIdentityFingerprintObservation>(),
                EvidenceHashes = new[] { "hash" },
                Tags = new PokemonIdentityTagObservation
                {
                    TagCount = 0, Section = null, IsSeparateFromIdentity = true
                },
                IgnoredFrameCount = 0
            });
        }
        public Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken) { Calls.Add("appraisal"); return Task.FromResult("AppraisalBarsObserved"); }
        public Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken) { Calls.Add("exit"); return Task.FromResult(VerifiedSequenceState.PokemonDetails); }
        public Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken) { Calls.Add("return"); return Task.FromResult(ReturnState); }
        public Task<VerifiedTagObservation> ReadTagObservationAsync(CancellationToken cancellationToken)
        {
            Calls.Add("tags");
            return Task.FromResult(new VerifiedTagObservation { TagCount = 0, NamesComplete = true, Section = null });
        }
        public Task<VerifiedSequenceState> AdvanceToNextPokemonAsync(PokemonIdentityConsensus previous, CancellationToken cancellationToken)
        {
            Calls.Add("advance");
            return Task.FromResult(AdvanceState);
        }
        public Task<IReadOnlyList<string>> ApplyIndexTagAsync(string tagName, CancellationToken cancellationToken) { Calls.Add("apply-index:" + tagName); return Task.FromResult<IReadOnlyList<string>>(new[] { tagName }); }
        public Task<IReadOnlyList<string>> ApplyClassificationTagAsync(string tagName, CancellationToken cancellationToken) { Calls.Add("apply-classification:" + tagName); return Task.FromResult<IReadOnlyList<string>>(new[] { tagName }); }
    }

    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
