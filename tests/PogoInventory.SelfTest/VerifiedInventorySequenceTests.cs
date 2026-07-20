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
            Query = "age0-7", ItemLimit = 2, ApplyTags = false, OutputDirectory = output
        }, "run-sequence-test");
        Assert(result.Checkpoint.Items.Count == 2, "item limit");
        Assert(result.Checkpoint.State == VerifiedSequenceState.Inventory, "inventory checkpoint");
        Assert(File.Exists(result.CheckpointPath), "checkpoint written");
        Assert(fake.Calls.All(call => !call.Contains("adb", StringComparison.OrdinalIgnoreCase)), "no raw adb");
        var resumed = await new VerifiedInventoryTaskSequence(new FakeOperations()).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 2, ApplyTags = false, OutputDirectory = output
        }, "different-run-id");
        Assert(resumed.Checkpoint.Items.Select(item => item.Ordinal).SequenceEqual(new[] { 1, 2 }), "resume keeps ordinals");

        var partial = new FakeOperations { IdentityStatus = PokemonIdentityObservationStatus.Partial };
        var partialResult = await new VerifiedInventoryTaskSequence(partial).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ApplyTags = false, OutputDirectory = Path.Combine(output, "partial")
        }, "run-partial-test");
        Assert(partialResult.Checkpoint.State == VerifiedSequenceState.Partial, "partial stops safely");

        var unknown = new FakeOperations { OpenState = VerifiedSequenceState.Unknown };
        var unknownResult = await new VerifiedInventoryTaskSequence(unknown).RunAsync(new VerifiedSequenceRequest
        {
            Query = "age0-7", ItemLimit = 3, ApplyTags = false, OutputDirectory = Path.Combine(output, "unknown")
        }, "run-unknown-test");
        Assert(unknownResult.Checkpoint.State == VerifiedSequenceState.Unknown, "unknown stop");
        Assert(unknown.Calls.Last() == "open", "no input after unknown");
        var rejectedDelete = false;
        try
        {
            new VerifiedSequenceRequest
            {
                Query = "age0-7", ItemLimit = 1, ApplyTags = true,
                ClassificationTag = "AI-Delete", OutputDirectory = output
            }.Validate();
        }
        catch (ArgumentException) { rejectedDelete = true; }
        Assert(rejectedDelete, "AI-Delete cannot be auto-applied");
    }

    private sealed class FakeOperations : IVerifiedInventoryNamedOperations
    {
        public PokemonIdentityObservationStatus IdentityStatus { get; init; } = PokemonIdentityObservationStatus.Complete;
        public VerifiedSequenceState OpenState { get; init; } = VerifiedSequenceState.PokemonDetails;
        public List<string> Calls { get; } = new();
        public Task<VerifiedSequenceState> EnsureInventoryAsync(string query, CancellationToken cancellationToken) { Calls.Add("ensure"); return Task.FromResult(VerifiedSequenceState.Inventory); }
        public Task<VerifiedSequenceState> OpenNextPokemonAsync(CancellationToken cancellationToken) { Calls.Add("open"); return Task.FromResult(OpenState); }
        public Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken) { Calls.Add("identity"); return Task.FromResult(new PokemonIdentityConsensus { Status = IdentityStatus, StableFingerprintSha256 = IdentityStatus == PokemonIdentityObservationStatus.Complete ? "fp" : "", StableFingerprintBase64 = "AA==", Confidence = 1, Frames = Array.Empty<PokemonIdentityFingerprintObservation>(), EvidenceHashes = new[] { "hash" }, Tags = new PokemonIdentityTagObservation { TagCount = 0, Section = null, IsSeparateFromIdentity = true }, IgnoredFrameCount = 0 }); }
        public Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken) { Calls.Add("appraisal"); return Task.FromResult("Partial"); }
        public Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken) { Calls.Add("exit"); return Task.FromResult(VerifiedSequenceState.PokemonDetails); }
        public Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken) { Calls.Add("return"); return Task.FromResult(VerifiedSequenceState.Inventory); }
        public Task<IReadOnlyList<string>> ReadTagsAsync(CancellationToken cancellationToken) { Calls.Add("tags"); return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()); }
        public Task<IReadOnlyList<string>> ApplyAllowListedTagAsync(string tagName, CancellationToken cancellationToken) { Calls.Add("apply:" + tagName); return Task.FromResult<IReadOnlyList<string>>(new[] { tagName }); }
    }

    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
