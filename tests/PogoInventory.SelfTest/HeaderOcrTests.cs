using PogoInventory.HeaderText;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class HeaderOcrTests
{
    private static readonly ISpeciesReference SpeciesReference =
        new StaticSpeciesReference(new[] { "Pidgey", "Eevee", "Bulbasaur", "Nidoran♀", "Nidoran♂" });

    public static async Task RunSpeciesConsensusAsync()
    {
        var agreeing = new[]
        {
            await Analyze("Eevee", "CP500"),
            await Analyze("Eevee", "CP500"),
            await Analyze("Pikachu-ish", "CP500")
        };
        var consensus = PokemonHeaderAnalyzer.Consensus(agreeing);
        Assert(consensus.Species == "Eevee", "species consensus should accept 2-of-3 agreement");
        Assert(!consensus.FailureReasons.Contains("SPECIES_CONSENSUS_NOT_REACHED"),
            "accepted species consensus should not report a failure reason");

        var conflicting = new[]
        {
            await Analyze("Eevee", "CP500"),
            await Analyze("Bulbasaur", "CP500"),
            await Analyze("Pidgey", "CP500")
        };
        var conflictConsensus = PokemonHeaderAnalyzer.Consensus(conflicting);
        Assert(conflictConsensus.Species is null, "conflicting species must not reach consensus (Unknown)");
        Assert(conflictConsensus.FailureReasons.Contains("SPECIES_CONSENSUS_NOT_REACHED"),
            "conflicting species failure reason");
    }

    public static async Task RunUiLabelsRejectedAsync()
    {
        foreach (var label in new[]
        {
            "CP", "Appraise", "Attack", "Defense", "HP", "Cancel", "Done", "Power Up", "Evolve", "Transfer"
        })
        {
            var result = await Analyze(label, "CP500");
            Assert(result.Species is null, $"UI label '{label}' must not be treated as species");
            Assert(result.FailureReasons.Contains("HEADER_TEXT_IS_UI_LABEL"),
                $"UI label '{label}' should report HEADER_TEXT_IS_UI_LABEL");
        }
    }

    public static async Task RunNicknameFallbackAsync()
    {
        var result = await Analyze("Buddy", "CP500");
        Assert(result.Species is null, "unknown header text must resolve to Species = Unknown (null)");
        Assert(result.Nickname == "Buddy", "unknown header text becomes the nickname");
        Assert(result.FailureReasons.Contains("HEADER_TEXT_NOT_SPECIES"),
            "nickname fallback should report HEADER_TEXT_NOT_SPECIES");
    }

    public static async Task RunCpParsingAsync()
    {
        var ok = await Analyze("Eevee", "CP1234");
        Assert(ok.Cp == 1234, "CP1234 should parse to 1234");
        Assert(ok.CpConfidence > 0, "parsed CP should carry positive confidence");

        var missing = await Analyze("Eevee", "CP");
        Assert(missing.Cp is null, "'CP' alone (no digits) must be rejected");
        Assert(missing.FailureReasons.Contains("CP_NOT_READ"), "missing CP should report CP_NOT_READ");

        var outOfRange = await Analyze("Eevee", "CP9999");
        Assert(outOfRange.Cp is null, "out-of-range CP (9999) must be rejected");

        var tooLow = await Analyze("Eevee", "CP5");
        Assert(tooLow.Cp is null, "out-of-range CP (5) must be rejected");

        var frames = new[]
        {
            await Analyze("Eevee", "CP1234"),
            await Analyze("Eevee", "CP1234"),
            await Analyze("Eevee", "CP1200")
        };
        var consensus = PokemonHeaderAnalyzer.Consensus(frames);
        Assert(consensus.Cp == 1234, "CP consensus should accept 2-of-3 agreement");
    }

    public static async Task RunTolerantSpeciesNormalizationAsync()
    {
        var caseInsensitive = await Analyze("eevee", "CP500");
        Assert(caseInsensitive.Species == "Eevee", "species matching must be case-insensitive");

        var oneCharNoise = await Analyze("Eevbe", "CP500");
        Assert(oneCharNoise.Species == "Eevee", "single-character OCR noise should still normalize");

        var genderGlyphNoise = await Analyze("Nidoran", "CP500");
        Assert(genderGlyphNoise.Species is not null,
            "Nidoran without its gender glyph should still normalize");
    }

    public static Task RunBitmapTransformGeometryAsync()
    {
        const int imageWidth = 1080;
        const int imageHeight = 2340;
        var roi = new NormalizedRegion { X = 0.30, Y = 0.03, Width = 0.40, Height = 0.07 };
        var pixelRoi = roi.ToPixels(imageWidth, imageHeight);

        foreach (var upscale in new[] { 1, 2, 3, 4 })
        {
            var transform = HeaderOcrGeometry.ComputeTransform(imageWidth, imageHeight, pixelRoi, upscale);

            Assert(transform.BoundsX + transform.BoundsWidth <= transform.ScaledWidth,
                $"upscale {upscale}: Bounds must lie fully within ScaledWidth");
            Assert(transform.BoundsY + transform.BoundsHeight <= transform.ScaledHeight,
                $"upscale {upscale}: Bounds must lie fully within ScaledHeight");
            Assert(transform.OutputWidth == (uint)(pixelRoi.Width * upscale),
                $"upscale {upscale}: output width should equal ROI width x upscale");
            Assert(transform.OutputHeight == (uint)(pixelRoi.Height * upscale),
                $"upscale {upscale}: output height should equal ROI height x upscale");
        }

        // Regression: the pre-fix code set ScaledWidth/Height from the ROI crop
        // size (not the full image) while leaving Bounds in original,
        // unscaled pixel coordinates. Reproduce that exact shape here and
        // confirm it would have failed (X + Width > ScaledWidth), then confirm
        // the real ComputeTransform output does not.
        const int regressionUpscale = 1;
        var brokenScaledWidth = (uint)(pixelRoi.Width * regressionUpscale);
        var brokenBoundsX = (uint)pixelRoi.X;
        var brokenBoundsWidth = (uint)pixelRoi.Width;
        Assert(brokenBoundsX + brokenBoundsWidth > brokenScaledWidth,
            "regression: the pre-fix Bounds/ScaledWidth shape must fall outside the scaled image");

        var fixedTransform = HeaderOcrGeometry.ComputeTransform(imageWidth, imageHeight, pixelRoi, regressionUpscale);
        Assert(fixedTransform.BoundsX + fixedTransform.BoundsWidth <= fixedTransform.ScaledWidth,
            "regression: the fixed transform must keep Bounds within ScaledWidth");

        return Task.CompletedTask;
    }

    public static Task RunUpscaleSelectionAsync()
    {
        Assert(HeaderOcrGeometry.ComputeUpscale(0, 100) == 1, "zero width/height must not upscale");
        Assert(HeaderOcrGeometry.ComputeUpscale(100, 0) == 1, "zero width/height must not upscale");
        Assert(HeaderOcrGeometry.ComputeUpscale(10, 10) == 4, "smallest side < 15 should upscale 4x");
        Assert(HeaderOcrGeometry.ComputeUpscale(14, 500) == 4, "boundary just below 15 should upscale 4x");
        Assert(HeaderOcrGeometry.ComputeUpscale(20, 500) == 3, "smallest side < 30 should upscale 3x");
        Assert(HeaderOcrGeometry.ComputeUpscale(29, 500) == 3, "boundary just below 30 should upscale 3x");
        // Spike-measured: the CP region at 1080x2340 crops to ~164px on its
        // smallest side and previously fell through the old 60px cutoff to 1x.
        Assert(HeaderOcrGeometry.ComputeUpscale(475, 164) == 2, "164px CP crop should upscale 2x, not 1x");
        Assert(HeaderOcrGeometry.ComputeUpscale(219, 500) == 2, "boundary just below 220 should upscale 2x");
        Assert(HeaderOcrGeometry.ComputeUpscale(220, 500) == 1, "boundary at 220 should not upscale");
        Assert(HeaderOcrGeometry.ComputeUpscale(1000, 1000) == 1, "large crops should not upscale");
        return Task.CompletedTask;
    }

    public static Task RunCpBinarizationAsync()
    {
        Assert(HeaderOcrBinarization.Luminance(255, 255, 255) == 255, "white luminance should be 255");
        Assert(HeaderOcrBinarization.Luminance(0, 0, 0) == 0, "black luminance should be 0");
        Assert(HeaderOcrBinarization.Luminance(0, 255, 0) == 150,
            "green-only luminance should follow the 0.587 Rec.601 weight");

        // Two flat clusters (dark background, bright text) -- Otsu should
        // split cleanly between them regardless of cluster size imbalance.
        var luminances = new List<byte>();
        for (var i = 0; i < 90; i++) luminances.Add(40);
        for (var i = 0; i < 10; i++) luminances.Add(220);
        var threshold = HeaderOcrBinarization.ComputeOtsuThreshold(luminances);
        Assert(threshold > 40 && threshold < 220, "Otsu threshold should fall between the two clusters");

        Assert(HeaderOcrBinarization.ComputeOtsuThreshold(Array.Empty<byte>()) == 128,
            "empty input should fall back to the midpoint threshold rather than throw");

        // 2x1 BGRA8 buffer: one dark pixel, one bright pixel, packed stride
        // (no row padding).
        var bgra = new byte[]
        {
            40, 40, 40, 255,
            220, 220, 220, 255
        };
        var binarized = HeaderOcrBinarization.Binarize(bgra, width: 2, height: 1, stride: 8, threshold: 128);
        Assert(binarized[0] == 255 && binarized[1] == 255 && binarized[2] == 255,
            "below-threshold (dark background) pixel should become white");
        Assert(binarized[4] == 0 && binarized[5] == 0 && binarized[6] == 0,
            "at/above-threshold (bright text) pixel should become black");
        Assert(binarized[3] == 255 && binarized[7] == 255, "alpha must always be forced fully opaque");

        var combinedThreshold = HeaderOcrBinarization.ComputeThreshold(bgra, width: 2, height: 1, stride: 8);
        Assert(combinedThreshold > 40 && combinedThreshold < 220,
            "ComputeThreshold should derive the same kind of split as ComputeOtsuThreshold directly");

        return Task.CompletedTask;
    }

    public static Task RunSearchQueryClassifierAsync()
    {
        AssertExactSpecies("pidgey", "Pidgey");
        AssertExactSpecies("pidgey&age0-365", "Pidgey");
        AssertBroadFilter("age0-1825");
        AssertBroadFilter("0*,1*,2*");
        AssertBroadFilter("!favorite");
        AssertBroadFilter("#Trade");
        AssertBroadFilter("someunknownword");
        return Task.CompletedTask;
    }

    private static void AssertExactSpecies(string query, string expectedSpecies)
    {
        var result = SearchQueryClassifier.Classify(query, SpeciesReference);
        Assert(result.Kind == SearchQueryKind.ExactSpecies, $"'{query}' should classify as ExactSpecies");
        Assert(result.Species == expectedSpecies, $"'{query}' should resolve species '{expectedSpecies}'");
    }

    private static void AssertBroadFilter(string query)
    {
        var result = SearchQueryClassifier.Classify(query, SpeciesReference);
        Assert(result.Kind == SearchQueryKind.BroadFilter, $"'{query}' should classify as BroadFilter");
    }

    private static async Task<PokemonHeaderResult> Analyze(string nameText, string cpText)
    {
        var analyzer = new PokemonHeaderAnalyzer(
            FakeTextRecognizer.WithCpAndName(cpText, nameText),
            SpeciesReference);
        return await analyzer.AnalyzeAsync(new byte[] { 1 }, HeaderScreenType.PokemonDetails);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
