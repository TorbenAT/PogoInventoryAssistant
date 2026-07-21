using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Core.Reference;

namespace PogoInventory.SelfTest;

internal static class SpeciesReferenceAndPolicyLoaderTests
{
    public static void RunSpeciesReferenceLoaderRejectsEmptyDocument()
    {
        var json = """
            {
              "version": "v1",
              "source": "curated",
              "species": [],
              "cpRange": { "min": 10, "max": 6000 }
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => SpeciesReferenceLoader.LoadFromJson(json),
            "empty species list");
    }

    public static void RunSpeciesReferenceLoaderRejectsMissingVersion()
    {
        var json = """
            {
              "source": "curated",
              "species": [ { "name": "Bulbasaur", "dexNumber": 1, "classification": "Ordinary" } ],
              "cpRange": { "min": 10, "max": 6000 }
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => SpeciesReferenceLoader.LoadFromJson(json),
            "missing version");
    }

    public static void RunSpeciesReferenceLoaderRejectsDuplicateNames()
    {
        var json = """
            {
              "version": "v1",
              "source": "curated",
              "species": [
                { "name": "Bulbasaur", "dexNumber": 1, "classification": "Ordinary" },
                { "name": "bulbasaur", "dexNumber": 2, "classification": "Ordinary" }
              ],
              "cpRange": { "min": 10, "max": 6000 }
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => SpeciesReferenceLoader.LoadFromJson(json),
            "duplicate species names");
    }

    public static void RunSpeciesReferenceLookupIsCaseAndDiacriticInsensitive()
    {
        var data = LoadSampleReferenceData();

        AssertTrue(data.IsKnownSpecies("mewtwo"), "lowercase lookup should match");
        AssertTrue(data.IsKnownSpecies("MEWTWO"), "uppercase lookup should match");
        AssertTrue(data.IsKnownSpecies("  Mewtwo  "), "whitespace should be trimmed");
        AssertTrue(data.IsKnownSpecies("Flabebe"), "diacritic-insensitive lookup should match");
        AssertEqual(
            SpeciesClassification.Mythical,
            data.Classification("mew") ?? throw new InvalidOperationException("mew should be known"),
            "mew classification");
    }

    public static void RunUnknownSpeciesIsConservative()
    {
        var data = LoadSampleReferenceData();

        AssertTrue(!data.IsKnownSpecies("Not-A-Real-Species-123"), "unknown species should not be known");
        AssertEqual(
            (SpeciesClassification?)null,
            data.Classification("Not-A-Real-Species-123"),
            "unknown species classification");
        AssertEqual(
            (bool?)null,
            data.IsProtectedRarity("Not-A-Real-Species-123"),
            "unknown species must never resolve to a false 'not protected' result");
    }

    public static void RunKnownOrdinarySpeciesIsNotProtected()
    {
        var data = LoadSampleReferenceData();
        AssertEqual((bool?)false, data.IsProtectedRarity("Bulbasaur"), "ordinary species should not be protected");
        AssertEqual((bool?)true, data.IsProtectedRarity("Mewtwo"), "legendary species should be protected");
        AssertEqual((bool?)true, data.IsProtectedRarity("Mew"), "mythical species should be protected");
        AssertEqual((bool?)true, data.IsProtectedRarity("Nihilego"), "ultra beast species should be protected");
    }

    public static void RunFullSpeciesReferenceFileLoadsAndCoversGenerations()
    {
        var data = SpeciesReferenceLoader.LoadFromFile(
            RepositoryPath("data", "reference", "species-reference.json"));

        AssertTrue(data.Species.Count >= 1025, "reference data should cover dex 1-1025");
        AssertTrue(data.IsKnownSpecies("Bulbasaur"), "gen 1 species should be known");
        AssertTrue(data.IsKnownSpecies("Pecharunt"), "gen 9 species should be known");
        AssertEqual((bool?)true, data.IsProtectedRarity("Arceus"), "Arceus should be legendary");
    }

    public static void RunRulePolicyLoaderRoundTripsDefault()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "policy.json");
            RulePolicyLoader.WriteDefault(path);

            var loaded = RulePolicyLoader.LoadFromFile(path);
            var expected = new RulePolicy();

            AssertEqual(expected.Version, loaded.Version, "policy version");
            AssertEqual(expected.OldPokemonCutoff, loaded.OldPokemonCutoff, "old Pokémon cutoff");
            AssertEqual(
                expected.MinimumOrdinaryCopiesPerSpeciesForm,
                loaded.MinimumOrdinaryCopiesPerSpeciesForm,
                "minimum ordinary copies");
            AssertEqual(
                expected.DeleteRequiresExactIdentity,
                loaded.DeleteRequiresExactIdentity,
                "delete requires exact identity");
            AssertEqual(
                expected.DeleteRequiresStrictlyBetterDuplicate,
                loaded.DeleteRequiresStrictlyBetterDuplicate,
                "delete requires strictly better duplicate");
            AssertTrue(
                expected.TradeTagNames.SequenceEqual(loaded.TradeTagNames),
                "trade tag names should round-trip");
            AssertTrue(
                expected.TradeNicknameFragments.SequenceEqual(loaded.TradeNicknameFragments),
                "trade nickname fragments should round-trip");
            AssertEqual(expected.PvpHeuristic.Enabled, loaded.PvpHeuristic.Enabled, "pvp enabled");
            AssertEqual(expected.PvpHeuristic.MaximumAttackIv, loaded.PvpHeuristic.MaximumAttackIv, "pvp max attack");
            AssertEqual(expected.PvpHeuristic.MinimumDefenseIv, loaded.PvpHeuristic.MinimumDefenseIv, "pvp min defense");
            AssertEqual(expected.PvpHeuristic.MinimumHpIv, loaded.PvpHeuristic.MinimumHpIv, "pvp min hp");
            AssertEqual(
                expected.PvpHeuristic.PreserveBestCandidatePerGroup,
                loaded.PvpHeuristic.PreserveBestCandidatePerGroup,
                "pvp preserve best candidate");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    public static void RunCommittedDefaultSampleMatchesRulePolicyDefaults()
    {
        var loaded = RulePolicyLoader.LoadFromFile(
            RepositoryPath("data", "reference", "rule-policy.default.json"));
        var expected = new RulePolicy();

        AssertEqual(expected.Version, loaded.Version, "sample policy version");
        AssertEqual(expected.OldPokemonCutoff, loaded.OldPokemonCutoff, "sample old Pokémon cutoff");
        AssertEqual(
            expected.MinimumOrdinaryCopiesPerSpeciesForm,
            loaded.MinimumOrdinaryCopiesPerSpeciesForm,
            "sample minimum ordinary copies");
        AssertEqual(
            expected.DeleteRequiresExactIdentity,
            loaded.DeleteRequiresExactIdentity,
            "sample delete requires exact identity");
        AssertEqual(
            expected.DeleteRequiresStrictlyBetterDuplicate,
            loaded.DeleteRequiresStrictlyBetterDuplicate,
            "sample delete requires strictly better duplicate");
        AssertTrue(
            expected.TradeTagNames.SequenceEqual(loaded.TradeTagNames),
            "sample trade tag names");
        AssertTrue(
            expected.TradeNicknameFragments.SequenceEqual(loaded.TradeNicknameFragments),
            "sample trade nickname fragments");
    }

    public static void RunRulePolicyLoaderFailsClosedOnUnknownTopLevelField()
    {
        var json = """
            {
              "schemaVersion": "1",
              "version": "0.1.0",
              "oldPokemonCutoff": "2018-12-31",
              "minimumOrdinaryCopiesPerSpeciesForm": 1,
              "deleteRequiresExactIdentity": true,
              "deleteRequiresStrictlyBetterDuplicate": true,
              "tradeTagNames": ["Trade"],
              "tradeNicknameFragments": ["Trade distan"],
              "pvpHeuristic": {
                "enabled": true,
                "maximumAttackIv": 5,
                "minimumDefenseIv": 10,
                "minimumHpIv": 10,
                "preserveBestCandidatePerGroup": true
              },
              "someFutureField": true
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => RulePolicyLoader.LoadFromJson(json),
            "unknown top-level field");
    }

    public static void RunRulePolicyLoaderFailsClosedOnUnknownPvpField()
    {
        var json = """
            {
              "schemaVersion": "1",
              "version": "0.1.0",
              "oldPokemonCutoff": "2018-12-31",
              "minimumOrdinaryCopiesPerSpeciesForm": 1,
              "deleteRequiresExactIdentity": true,
              "deleteRequiresStrictlyBetterDuplicate": true,
              "tradeTagNames": ["Trade"],
              "tradeNicknameFragments": ["Trade distan"],
              "pvpHeuristic": {
                "enabled": true,
                "maximumAttackIv": 5,
                "minimumDefenseIv": 10,
                "minimumHpIv": 10,
                "preserveBestCandidatePerGroup": true,
                "extraField": 1
              }
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => RulePolicyLoader.LoadFromJson(json),
            "unknown pvpHeuristic field");
    }

    public static void RunRulePolicyLoaderRejectsUnsupportedSchemaVersion()
    {
        var json = """
            {
              "schemaVersion": "999",
              "version": "0.1.0",
              "oldPokemonCutoff": "2018-12-31",
              "minimumOrdinaryCopiesPerSpeciesForm": 1,
              "deleteRequiresExactIdentity": true,
              "deleteRequiresStrictlyBetterDuplicate": true,
              "tradeTagNames": ["Trade"],
              "tradeNicknameFragments": ["Trade distan"],
              "pvpHeuristic": {
                "enabled": true,
                "maximumAttackIv": 5,
                "minimumDefenseIv": 10,
                "minimumHpIv": 10,
                "preserveBestCandidatePerGroup": true
              }
            }
            """;

        AssertThrows<InvalidOperationException>(
            () => RulePolicyLoader.LoadFromJson(json),
            "unsupported schema version");
    }

    public static void RunAnalyzerUsesReferenceDataClassificationWhenProvided()
    {
        var referenceData = LoadSampleReferenceData();
        var observation = Pokemon("A", "Mewtwo", 4000, 14, 14, 14) with
        {
            // The observation itself claims not legendary; reference data (when
            // supplied) must override this because Mewtwo is a known legendary.
            IsLegendary = false
        };

        var withReference = new InventoryAnalyzer(referenceData).Analyze(
            new[] { observation },
            new RulePolicy());
        AssertEqual(
            DecisionCategory.Review,
            withReference.Decisions.Single().Category,
            "legendary species should be reviewed when reference data overrides the field");
        AssertTrue(
            withReference.Decisions.Single().Reasons.Any(r => r.Code == "REVIEW_LEGENDARY"),
            "legendary reason should be present when reference data overrides the field");
    }

    public static void RunAnalyzerLeavesObservationUnchangedWhenReferenceDataAbsent()
    {
        var observation = Pokemon("A", "Mewtwo", 4000, 14, 14, 14) with
        {
            IsLegendary = false
        };

        var withoutReference = new InventoryAnalyzer().Analyze(
            new[] { observation },
            new RulePolicy());

        AssertTrue(
            withoutReference.Decisions.Single().Reasons.All(r => r.Code != "REVIEW_LEGENDARY"),
            "without reference data the observation's own IsLegendary=false must be respected");
    }

    public static void RunAnalyzerLeavesUnknownSpeciesUnchangedWhenReferenceDataProvided()
    {
        var referenceData = LoadSampleReferenceData();
        var observation = Pokemon("A", "Totally-Unknown-Species", 900, 14, 14, 14) with
        {
            IsLegendary = true
        };

        var result = new InventoryAnalyzer(referenceData).Analyze(
            new[] { observation },
            new RulePolicy());

        AssertTrue(
            result.Decisions.Single().Reasons.Any(r => r.Code == "REVIEW_LEGENDARY"),
            "unknown species must keep the observation's own field, never silently cleared");
    }

    private static SpeciesReferenceData LoadSampleReferenceData()
    {
        var json = """
            {
              "version": "unit-test-sample",
              "source": "curated",
              "species": [
                { "name": "Bulbasaur", "dexNumber": 1, "classification": "Ordinary" },
                { "name": "Mewtwo", "dexNumber": 150, "classification": "Legendary" },
                { "name": "Mew", "dexNumber": 151, "classification": "Mythical" },
                { "name": "Nihilego", "dexNumber": 793, "classification": "UltraBeast" },
                { "name": "Arceus", "dexNumber": 493, "classification": "Legendary" },
                { "name": "Flabébé", "dexNumber": 669, "classification": "Ordinary" }
              ],
              "cpRange": { "min": 10, "max": 6000 }
            }
            """;

        return SpeciesReferenceLoader.LoadFromJson(json);
    }

    private static PokemonObservation Pokemon(
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
            Form = "Normal",
            Costume = "None",
            Cp = cp,
            AttackIv = attack,
            DefenseIv = defense,
            HpIv = hp,
            CatchDate = new DateOnly(2026, 7, 1),
            CatchLocation = "Holstebro, Danmark",
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
            IdentityConfidence = IdentityConfidence.Exact,
            VariantIdentity = new PokemonVariantIdentity
            {
                SpeciesId = 1,
                SpeciesName = species,
                FormId = "normal",
                FormName = "Normal",
                CostumeId = "none",
                CostumeName = "None",
                BackgroundId = "none",
                BackgroundName = "None",
                GenderVariant = "not-variant-relevant",
                IsShiny = false,
                ShadowState = "normal",
                LuckyState = "not-lucky",
                DynamaxState = "none",
                SpecialVariantId = "none",
                SpecialVariantName = "None",
                VariantIdentityConfidence = IdentityConfidence.Exact
            }
        };

    private static void AssertThrows<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
            throw new InvalidOperationException($"Expected {description} to fail.");
        }
        catch (TException exception) when (
            exception.Message != $"Expected {description} to fail.")
        {
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected {description} to be '{expected}', got '{actual}'.");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "PogoInventoryAssistant",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root could not be located.");
        }

        return parts.Aggregate(directory.FullName, Path.Combine);
    }
}
