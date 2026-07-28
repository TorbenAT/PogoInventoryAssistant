using PogoInventory.Semantics;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Semantics;

namespace PogoInventory.SelfTest;

internal static class CanonicalSemanticCoreTests
{
    public static Task PixelBridgePreservesChannelsAndStrideAsync()
    {
        var source = new byte[] { 10, 20, 30, 40, 1, 2, 3, 4, 99, 99, 99, 99 };
        var rgba = BgraPixelBridge.ToTightlyPackedRgba32(source, 2, 1, 12);
        Assert(rgba.SequenceEqual(new byte[] { 30, 20, 10, 40, 3, 2, 1, 4 }), "BGRA-to-RGBA conversion or stride handling failed.");
        return Task.CompletedTask;
    }

    public static Task FreshnessBarrierRejectsStaleAndWrongStateAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var barrier = new FrameBarrier(10, now.AddSeconds(-2), TimeSpan.FromSeconds(3), "AppraisalBars");
        var fresh = Metadata(11, now.AddSeconds(-1), "AppraisalBars");
        var stale = Metadata(10, now.AddSeconds(-1), "AppraisalBars");
        var wrong = Metadata(12, now.AddSeconds(-1), "PokemonDetails");
        Assert(barrier.Accepts(fresh, now), "Fresh frame should pass barrier.");
        Assert(!barrier.Accepts(stale, now), "Stale frame passed barrier.");
        Assert(!barrier.Accepts(wrong, now), "Wrong-state frame passed barrier.");
        return Task.CompletedTask;
    }

    public static Task CanonicalConsensusRequiresDistinctEvidenceAsync()
    {
        var analyzer = new PokemonItemSemanticAnalyzer();
        var evidence = new PokemonItemEvidenceSet("item-1", [Frame(1, "a"), Frame(2, "b")], [Frame(3, "c"), Frame(4, "d")]);
        var result = analyzer.Analyze(evidence,
            [new("Pikachu", .9, 1, "a"), new("Pikachu", .8, 2, "b")],
            [new(402, .9, 1, "a"), new(402, .8, 2, "b")],
            [new((15, 14, 13), .9, 3, "c"), new((15, 14, 13), .8, 4, "d")]);
        Assert(result.Species.Status == SemanticFieldStatus.Known && result.Cp.Status == SemanticFieldStatus.Known, "Two-frame agreement was not accepted.");
        Assert(result.AttackIv.Value == 15 && result.DefenseIv.Value == 14 && result.HpIv.Value == 13 && result.IsComplete, "IV consensus was not accepted.");
        return Task.CompletedTask;
    }

    private static PokemonEvidenceFrame Frame(long id, string hash) => new(id, DateTimeOffset.UtcNow, hash, "AppraisalBars", "test");
    private static FrameMetadata Metadata(long id, DateTimeOffset captured, string state) => new(new FrameId(id), new FrameTimestamp(id, captured, TimeSpan.Zero), new FrameDescriptor(2, 1, 8, FramePixelFormat.Bgra32), FrameQuality.Unknown, new FrameStability(0, 3, TimeSpan.FromMilliseconds(100), true), "test", new Dictionary<string, string> { ["screen"] = state });
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
