using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;

var tests = new (string Name, Action Run)[]
{
    ("Perfect is kept", PerfectIsKept),
    ("Trade nickname is kept", TradeNicknameIsKept),
    ("Old Pokémon is kept", OldPokemonIsKept),
    ("Shadow is reviewed", ShadowIsReviewed),
    ("Inferior duplicate is deleted", InferiorDuplicateIsDeleted),
    ("Preliminary PvP candidate is reviewed", PvpCandidateIsReviewed),
    ("Non-exact identity cannot be deleted", NonExactIdentityCannotBeDeleted)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void PerfectIsKept()
{
    var result = Analyze(
        P("A", "Pikachu", 500, 15, 15, 15));
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void TradeNicknameIsKept()
{
    var result = Analyze(
        P("A", "Bidoof", 100, 1, 1, 1) with { Nickname = "Trade distan" });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void OldPokemonIsKept()
{
    var result = Analyze(
        P("A", "Eevee", 300, 5, 5, 5) with { CatchDate = new DateOnly(2018, 1, 1) });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void ShadowIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 400, 5, 5, 5) with { IsShadow = true });
    AssertCategory(result, "A", DecisionCategory.Review);
}

static void InferiorDuplicateIsDeleted()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 8, 8, 8) with { SequenceNumber = 2 });
    AssertCategory(result, "A", DecisionCategory.Keep);
    AssertCategory(result, "B", DecisionCategory.Delete);
}

static void PvpCandidateIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 0, 15, 15) with { SequenceNumber = 2 });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void NonExactIdentityCannotBeDeleted()
{
    var result = Analyze(
        P("A", "Rattata", 900, 14, 14, 14),
        P("B", "Rattata", 500, 8, 8, 8) with
        {
            SequenceNumber = 2,
            IdentityConfidence = IdentityConfidence.HighConfidence
        });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static InventoryAnalysisResult Analyze(params PokemonObservation[] observations) =>
    new InventoryAnalyzer().Analyze(observations, new RulePolicy());

static PokemonObservation P(
    string key,
    string species,
    int cp,
    int attack,
    int defense,
    int hp) =>
    new()
    {
        ExternalKey = key,
        SequenceNumber = 1,
        Species = species,
        Cp = cp,
        AttackIv = attack,
        DefenseIv = defense,
        HpIv = hp,
        CatchDate = new DateOnly(2026, 7, 1),
        IsShiny = false,
        IsMythical = false,
        IsBackground = false,
        IsFavorite = false,
        IsLegendary = false,
        IsUltraBeast = false,
        IsShadow = false,
        IsPurified = false,
        IsLucky = false,
        IsCostume = false,
        IsDynamax = false,
        IsGigantamax = false,
        HasSpecialMove = false,
        IsXxl = false,
        IsXxs = false,
        IdentityConfidence = IdentityConfidence.Exact
    };

static void AssertCategory(
    InventoryAnalysisResult result,
    string externalKey,
    DecisionCategory expected)
{
    var actual = result.Decisions.Single(x => x.ExternalKey == externalKey).Category;
    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
