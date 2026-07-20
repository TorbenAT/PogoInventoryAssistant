using System.Security.Cryptography;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class PokemonIdentityTests
{
    public static Task RunHashAndInstanceAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var png = PngEncoder.Encode(Frame("Eevee", 0, 0, false));
        var result = analyzer.Analyze(png);
        Assert(result.Status == PokemonIdentityObservationStatus.Complete, "complete identity");
        Assert(result.EvidenceSha256 == Hex(SHA256.HashData(png)), "evidence hash is PNG hash");
        Assert(result.EvidenceSha256 != result.StableFingerprintSha256, "hashes are separate");
        var consensus = analyzer.Consensus(new[] { FrameOf(png), FrameOf(png), FrameOf(png) });
        var first = PokemonIdentityInstance.Create("run-test", 1, consensus);
        var second = PokemonIdentityInstance.Create("run-test", 2, consensus);
        Assert(first.InstanceId != second.InstanceId, "ordinal instances remain separate");
        return Task.CompletedTask;
    }

    public static Task RunTagLayoutAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var results = new[] { 0, 1, 2 }.Select(tags =>
            analyzer.Analyze(PngEncoder.Encode(Frame("Eevee", 0, tags, false)))).ToArray();
        var decoded = PngDecoder.Decode(PngEncoder.Encode(Frame("Eevee", 0, 1, false)));
        Assert(decoded.GetPixel(50, 105).G > decoded.GetPixel(50, 105).R, "tag pixels survive PNG");
        Assert(results.All(item => item.Status == PokemonIdentityObservationStatus.Complete), "tag frames complete");
        Assert(results.Select(item => item.Tags.TagCount).SequenceEqual(new[] { 0, 1, 2 }),
            $"tag count: {string.Join(',', results.Select(item => item.Tags.TagCount))}");
        Assert(results.Select(item => item.StableFingerprintSha256).Distinct().Count() == 1, "tags excluded");
        Assert(results.Select(item => item.EvidenceSha256).Distinct().Count() == 3, "full hashes differ");
        Assert(results.All(item => item.Tags.IsSeparateFromIdentity), "tag state separate");
        return Task.CompletedTask;
    }

    public static Task RunAnchorAlignmentAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var noTags = analyzer.Analyze(PngEncoder.Encode(Frame("Eevee", 0, 0, false)));
        var twoTags = analyzer.Analyze(PngEncoder.Encode(Frame("Eevee", 0, 2, false)));
        Assert(noTags.StableFingerprintSha256 == twoTags.StableFingerprintSha256, "lower content aligned");
        Assert(noTags.LowerAnchorY != twoTags.LowerAnchorY, "anchor moved dynamically");
        return Task.CompletedTask;
    }

    public static Task RunSeparationAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var baseFrame = analyzer.Analyze(PngEncoder.Encode(Frame("Eevee", 0, 0, false)));
        var changedSpecies = analyzer.Analyze(PngEncoder.Encode(Frame("Pikachu", 0, 0, false)));
        var changedCp = analyzer.Analyze(PngEncoder.Encode(Frame("Eevee", 1, 0, false)));
        Assert(baseFrame.StableFingerprintSha256 != changedSpecies.StableFingerprintSha256, "species separated");
        Assert(baseFrame.StableFingerprintSha256 != changedCp.StableFingerprintSha256, "CP separated");
        return Task.CompletedTask;
    }

    public static Task RunConsensusAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var frames = Enumerable.Range(0, 3).Select(_ => new PokemonIdentityFrame
        {
            ScreenshotPng = PngEncoder.Encode(Frame("Eevee", 0, 1, false))
        }).ToArray();
        var result = analyzer.Consensus(frames);
        Assert(result.Status == PokemonIdentityObservationStatus.Complete, "three frame consensus");
        Assert(result.EvidenceHashes.Distinct().Count() == 1, "same evidence is retained");
        Assert(result.Frames.Count == 3, "all frames retained");
        return Task.CompletedTask;
    }

    private static PokemonIdentityFrame FrameOf(byte[] png) => new() { ScreenshotPng = png };

    private static PixelImage Frame(string species, int cpVariant, int tagCount, bool special)
    {
        const int width = 160;
        const int height = 300;
        var rgba = Enumerable.Repeat((byte)245, width * height * 4).ToArray();
        Fill(rgba, width, 0, 20, 160, 50, species == "Eevee" ? (30, 70, 190) : (190, 50, 40));
        Fill(rgba, width, 18, 70, 142, 78, cpVariant == 0 ? (20, 20, 20) : (80, 20, 20));
        for (var tag = 0; tag < tagCount; tag++)
        {
            var top = 102 + tag * 16;
            Fill(rgba, width, 28, top, 132, top + 10, tag == 0 ? (40, 160, 100) : (150, 100, 40));
        }
        var shift = tagCount * 16;
        Fill(rgba, width, 18, 148 + shift, 142, 153 + shift, special ? (15, 15, 15) : (40, 40, 40));
        Fill(rgba, width, 25, 174 + shift, 135, 184 + shift, (70, 70, 70));
        Fill(rgba, width, 32, 204 + shift, 128, 214 + shift, (90, 90, 90));
        return new PixelImage(width, height, rgba);
    }

    private static void Fill(byte[] rgba, int width, int left, int top, int right, int bottom,
        (int R, int G, int B) color)
    {
        for (var y = Math.Max(0, top); y < Math.Min(300, bottom); y++)
        for (var x = left; x < right; x++)
        {
            var offset = (y * width + x) * 4;
            rgba[offset] = (byte)color.R; rgba[offset + 1] = (byte)color.G;
            rgba[offset + 2] = (byte)color.B; rgba[offset + 3] = 255;
        }
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
