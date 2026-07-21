using PogoInventory.HeaderText;

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
