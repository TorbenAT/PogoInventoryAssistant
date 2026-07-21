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
        Assert(decoded.GetPixel(50, 165).G == decoded.GetPixel(50, 165).R, "tag pixels survive PNG");
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

    public static Task RunConsensusContractAsync()
    {
        var analyzer = new PokemonDetailsIdentityAnalyzer();
        var baseImage = Frame("Eevee", 0, 0, false);
        var basePng = PngEncoder.Encode(baseImage);
        var one = analyzer.Consensus(new[] { FrameOf(basePng) });
        var two = analyzer.Consensus(new[] { FrameOf(basePng), FrameOf(basePng) });
        Assert(one.Status == PokemonIdentityObservationStatus.Partial, "one frame is partial");
        Assert(two.Status == PokemonIdentityObservationStatus.Partial, "two frames are partial");

        var three = ConsensusOf(analyzer, baseImage, baseImage, baseImage);
        Assert(three.Status == PokemonIdentityObservationStatus.Complete, "three frames are complete");

        var unavailable = analyzer.Consensus(new[]
        {
            FrameOf(basePng),
            FrameOf(basePng),
            new PokemonIdentityFrame { ScreenshotPng = new byte[] { 1, 2, 3 } }
        });
        Assert(unavailable.Status == PokemonIdentityObservationStatus.Partial,
            "two usable plus unavailable is partial");
        Assert(unavailable.IgnoredFrameCount == 1, "unavailable frame is ignored");

        var animated = WithFill(baseImage, 72, 130, 84, 134, (45, 85, 175));
        var orderedA = ConsensusOf(analyzer, baseImage, animated, baseImage);
        var orderedB = ConsensusOf(analyzer, animated, baseImage, baseImage);
        Assert(orderedA.Status == PokemonIdentityObservationStatus.Complete,
            "small animation remains complete");
        Assert(orderedA.StableFingerprintSha256 == orderedB.StableFingerprintSha256,
            "frame order does not change canonical fingerprint");
        Assert(orderedA.StableFingerprintSha256 == three.StableFingerprintSha256,
            "small animation is removed by canonical fingerprint");
        Assert(orderedA.EvidenceHashes.Distinct().Count() == 2,
            "evidence hashes remain separate");

        var changedCp = ConsensusOf(analyzer, Frame("Eevee", 1, 0, false),
            Frame("Eevee", 1, 0, false), Frame("Eevee", 1, 0, false));
        var changedSpecies = ConsensusOf(analyzer, Frame("Pikachu", 0, 0, false),
            Frame("Pikachu", 0, 0, false), Frame("Pikachu", 0, 0, false));
        Assert(changedCp.StableFingerprintSha256 != three.StableFingerprintSha256,
            "changed CP changes consensus fingerprint");
        Assert(changedSpecies.StableFingerprintSha256 != three.StableFingerprintSha256,
            "changed species changes consensus fingerprint");

        var tagged = ConsensusOf(analyzer, Frame("Eevee", 0, 0, false),
            Frame("Eevee", 0, 1, false), Frame("Eevee", 0, 2, false));
        Assert(tagged.Status == PokemonIdentityObservationStatus.Complete,
            "tagged frames are complete");
        Assert(tagged.StableFingerprintSha256 == three.StableFingerprintSha256,
            "mutable tags are excluded from consensus fingerprint");
        return Task.CompletedTask;
    }

    private static PokemonIdentityFrame FrameOf(byte[] png) => new() { ScreenshotPng = png };

    private static PokemonIdentityConsensus ConsensusOf(
        PokemonDetailsIdentityAnalyzer analyzer,
        params PixelImage[] images) => analyzer.Consensus(images.Select(image => new PokemonIdentityFrame
        {
            ScreenshotPng = PngEncoder.Encode(image)
        }).ToArray());

    private static PixelImage WithFill(PixelImage source, int left, int top, int right, int bottom,
        (int R, int G, int B) color)
    {
        var rgba = source.RgbaBytes.ToArray();
        Fill(rgba, source.Width, left, top, right, bottom, color);
        return new PixelImage(source.Width, source.Height, rgba);
    }

    private static PixelImage Frame(string species, int cpVariant, int tagCount, bool special)
    {
        const int width = 160;
        const int height = 340;
        var rgba = Enumerable.Repeat((byte)245, width * height * 4).ToArray();
        Fill(rgba, width, 0, 128, 160, 145, species == "Eevee" ? (30, 70, 190) : (190, 50, 40));
        Fill(rgba, width, 18, 150, 142, 158, cpVariant == 0 ? (20, 20, 20) : (80, 20, 20));
        for (var tag = 0; tag < tagCount; tag++)
        {
            var top = 162 + tag * 16;
            Fill(rgba, width, 40, top, 120, top + 10, tag == 0 ? (180, 180, 180) : (200, 200, 200));
        }
        var shift = tagCount * 16;
        Fill(rgba, width, 10, 190 + shift, 150, 191 + shift, (205, 205, 205));
        Fill(rgba, width, 18, 210 + shift, 142, 215 + shift, special ? (15, 15, 15) : (40, 40, 40));
        Fill(rgba, width, 25, 230 + shift, 135, 240 + shift, (70, 70, 70));
        Fill(rgba, width, 32, 260 + shift, 128, 270 + shift, (90, 90, 90));
        return new PixelImage(width, height, rgba);
    }

    private static void Fill(byte[] rgba, int width, int left, int top, int right, int bottom,
        (int R, int G, int B) color)
    {
        for (var y = Math.Max(0, top); y < Math.Min(340, bottom); y++)
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
