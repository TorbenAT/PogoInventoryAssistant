using Microsoft.Data.Sqlite;
using PogoInventory.Application;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Persistence;

namespace PogoInventory.SelfTest;

/// <summary>
/// Covers the semantic identity key, the cross-run matcher and the
/// GroupKey duplicate-grouping fix, plus the SchemaVersion 2 -> 3 migration
/// and persisted-key roundtrip through InventoryPersistenceService.
/// </summary>
internal static class SemanticIdentityTests
{
    public static void RunKeyNormalizationAndUnknownHandling()
    {
        var unknownSpecies = SemanticIdentityKey.FromObservation(Observation("Unknown"));
        AssertTrue(unknownSpecies.ReadableKey.StartsWith("unknown|", StringComparison.Ordinal), "unknown species normalizes to literal token");

        var trimmedAndCased = SemanticIdentityKey.FromObservation(Observation("  PIKACHU  "));
        var lowerTrimmed = SemanticIdentityKey.FromObservation(Observation("pikachu"));
        AssertEqual(lowerTrimmed.FullKey, trimmedAndCased.FullKey, "species is trimmed and lowercased before hashing");

        var missingEverything = SemanticIdentityKey.FromObservation(new PokemonObservation
        {
            ExternalKey = "x",
            SequenceNumber = 1,
            Species = "Pikachu"
        });
        AssertTrue(missingEverything.ReadableKey.Contains("|unknown|"), "null fields normalize to the unknown token");
    }

    public static void RunCompletenessClassification()
    {
        var insufficient = SemanticIdentityKey.FromObservation(new PokemonObservation
        {
            ExternalKey = "a",
            SequenceNumber = 1,
            Species = "Unknown"
        });
        AssertEqual(SemanticKeyCompleteness.Insufficient, insufficient.Completeness, "unknown species is Insufficient");

        var partial = SemanticIdentityKey.FromObservation(new PokemonObservation
        {
            ExternalKey = "b",
            SequenceNumber = 1,
            Species = "Pidgey",
            Cp = 500
        });
        AssertEqual(SemanticKeyCompleteness.Partial, partial.Completeness, "known species with missing IVs is Partial");

        var comparable = SemanticIdentityKey.FromObservation(new PokemonObservation
        {
            ExternalKey = "c",
            SequenceNumber = 1,
            Species = "Pidgey",
            Cp = 500,
            AttackIv = 10,
            DefenseIv = 11,
            HpIv = 12
        });
        AssertEqual(SemanticKeyCompleteness.Comparable, comparable.Completeness, "species + CP + all IVs is Comparable");
    }

    public static void RunMatcherExactMatch()
    {
        var priorKey = SemanticIdentityKey.FromObservation(ComparableObservation("Pidgey", 500, 10, 11, 12));
        var prior = new[]
        {
            new SemanticIdentityRecord { LocalPokemonId = "prior-1", FullKey = priorKey.FullKey, Completeness = priorKey.Completeness }
        };

        var newKey = SemanticIdentityKey.FromObservation(ComparableObservation("Pidgey", 500, 10, 11, 12));
        var result = SemanticIdentityMatcher.Match(prior, new SemanticIdentityRecord
        {
            LocalPokemonId = "new-1",
            FullKey = newKey.FullKey,
            Completeness = newKey.Completeness
        });

        AssertEqual(SemanticMatchOutcome.Matched, result.Outcome, "identical Comparable keys match");
        AssertEqual("prior-1", result.MatchedLocalPokemonId, "matched id is returned");
    }

    public static void RunMatcherAmbiguousCollisionNeverMerges()
    {
        var key = SemanticIdentityKey.FromObservation(ComparableObservation("Pidgey", 500, 10, 11, 12));
        var prior = new[]
        {
            new SemanticIdentityRecord { LocalPokemonId = "prior-1", FullKey = key.FullKey, Completeness = key.Completeness },
            new SemanticIdentityRecord { LocalPokemonId = "prior-2", FullKey = key.FullKey, Completeness = key.Completeness }
        };

        var result = SemanticIdentityMatcher.Match(prior, new SemanticIdentityRecord
        {
            LocalPokemonId = "new-1",
            FullKey = key.FullKey,
            Completeness = key.Completeness
        });

        AssertEqual(SemanticMatchOutcome.AmbiguousCollision, result.Outcome, "multiple prior candidates never auto-merge");
        AssertEqual(2, result.CandidateLocalPokemonIds.Count, "all colliding candidates are recorded");
        AssertTrue(result.MatchedLocalPokemonId is null, "ambiguous collision does not select a matched id");
    }

    public static void RunMatcherInsufficientNeverMatches()
    {
        var priorKey = SemanticIdentityKey.FromObservation(ComparableObservation("Pidgey", 500, 10, 11, 12));
        var prior = new[]
        {
            new SemanticIdentityRecord { LocalPokemonId = "prior-1", FullKey = priorKey.FullKey, Completeness = priorKey.Completeness }
        };

        // Same FullKey text, but the new side is Insufficient (species unknown):
        // it must never be treated as a match even if the hash happens to line up.
        var result = SemanticIdentityMatcher.Match(prior, new SemanticIdentityRecord
        {
            LocalPokemonId = "new-1",
            FullKey = priorKey.FullKey,
            Completeness = SemanticKeyCompleteness.Insufficient
        });
        AssertEqual(SemanticMatchOutcome.NoMatch, result.Outcome, "Insufficient completeness never matches, regardless of key text");

        var partialResult = SemanticIdentityMatcher.Match(prior, new SemanticIdentityRecord
        {
            LocalPokemonId = "new-2",
            FullKey = priorKey.FullKey,
            Completeness = SemanticKeyCompleteness.Partial
        });
        AssertEqual(SemanticMatchOutcome.NoMatch, partialResult.Outcome, "Partial completeness never matches across runs");
    }

    public static void RunGroupKeyGroupsSameSpeciesAndVariant()
    {
        var first = SemanticGroupObservation("a", "Pidgey");
        var second = SemanticGroupObservation("b", "Pidgey");
        AssertEqual(first.GroupKey, second.GroupKey, "same species and known semantic variant fields share a duplicate group");

        var differentForm = second with { Form = "Alolan" };
        AssertTrue(first.GroupKey != differentForm.GroupKey, "different form must not share a duplicate group");
    }

    public static void RunGroupKeyFallsBackForUnknownSpecies()
    {
        var first = SemanticGroupObservation("a", "Unknown");
        var second = SemanticGroupObservation("b", "Unknown");
        AssertTrue(first.GroupKey != second.GroupKey, "unknown species falls back to a per-instance group key");
    }

    public static async Task RunSchemaMigrationAndKeyRoundtripAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pogo-semantic-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "legacy-v2.db");
        try
        {
            await CreateVersionTwoDatabaseAsync(databasePath);
            AssertEqual(2L, await ReadSchemaVersionAsync(databasePath), "fixture starts at schema version 2");

            var persistence = new InventoryPersistenceService(databasePath);
            await persistence.InitializeAsync();
            AssertEqual(3L, await ReadSchemaVersionAsync(databasePath), "schema migrates from 2 to 3 cleanly");

            var observation = ComparableObservation("Pidgey", 500, 10, 11, 12);
            var record = new CleanupProofObservationRecord
            {
                RunId = "run-a",
                Ordinal = 1,
                LocalPokemonId = "run-a:000001",
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Observation = observation,
                ObservationStatus = "Complete",
                IdentityConfidenceValue = 0.9,
                ProtectionConfidenceValue = 0.1,
                StableFingerprint = "fingerprint-a",
                ScreenshotPaths = Array.Empty<string>(),
                ScreenshotHashes = Array.Empty<string>(),
                AppraisalEvidence = Array.Empty<string>(),
                FieldEvidenceSources = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            await persistence.StartCleanupRunAsync(new CleanupProofRunStart
            {
                RunId = "run-a",
                SearchQuery = "Pidgey",
                StartedAtUtc = DateTimeOffset.UtcNow,
                DeviceSerial = "synthetic",
                RequestedItems = 1,
                SourceDirectory = root
            });
            await persistence.RecordCleanupObservationAsync(record);

            var expectedKey = SemanticIdentityKey.FromObservation(observation);
            var rows = await persistence.LoadCleanupProofRowsAsync("run-a");
            var row = rows.Single();
            AssertEqual(expectedKey.FullKey, row.SemanticKey, "persisted Observations.SemanticKey matches computed key");
            AssertEqual(expectedKey.Completeness.ToString(), row.SemanticKeyCompleteness, "persisted Observations.SemanticKeyCompleteness matches computed completeness");

            var records = await persistence.LoadAllPokemonRecordsAsync();
            var persistedRecord = records.Single();
            AssertEqual(expectedKey.FullKey, persistedRecord.SemanticKey, "persisted PokemonRecords.SemanticKey matches computed key");
            AssertEqual(expectedKey.Completeness.ToString(), persistedRecord.SemanticKeyCompleteness, "persisted PokemonRecords.SemanticKeyCompleteness matches computed completeness");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    public static async Task RunReidentificationRunnerAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pogo-reidentification-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var databaseA = Path.Combine(root, "a.sqlite");
            var databaseB = Path.Combine(root, "b.sqlite");
            var outputDirectory = Path.Combine(root, "out");

            var serviceA = new InventoryPersistenceService(databaseA);
            var serviceB = new InventoryPersistenceService(databaseB);

            // Same real Pokémon rescanned: identical semantic fields on both sides.
            await SeedRunAsync(serviceA, "run-a-1", "same-1", ComparableObservation("Pidgey", 500, 10, 11, 12));
            // A second individual only ever seen in database A.
            await SeedRunAsync(serviceA, "run-a-2", "only-a", ComparableObservation("Rattata", 300, 1, 2, 3));
            // The same Pidgey rescanned in database B.
            await SeedRunAsync(serviceB, "run-b-1", "same-2", ComparableObservation("Pidgey", 500, 10, 11, 12));
            // Species unknown in B: must never match anything.
            await SeedRunAsync(serviceB, "run-b-2", "unknown-b", Observation("Unknown"));

            var report = await new ReidentificationRunner().RunAsync(databaseA, databaseB, outputDirectory);

            AssertEqual(2, report.TotalA, "total A records");
            AssertEqual(2, report.TotalB, "total B records");
            AssertEqual(1, report.MatchedCount, "one real re-identified match");
            AssertEqual(1, report.UnmatchedCount, "the Insufficient B record cannot match");
            AssertEqual(0, report.AmbiguousCollisionCount, "no collisions in this fixture");
            AssertTrue(report.ReMatchRatePercent > 0, "re-match rate is positive when a match exists");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "reidentification-report.json")), "JSON report is written");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "reidentification-report.md")), "Markdown report is written");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SeedRunAsync(
        InventoryPersistenceService service,
        string runId,
        string localSuffix,
        PokemonObservation observation)
    {
        await service.StartCleanupRunAsync(new CleanupProofRunStart
        {
            RunId = runId,
            SearchQuery = observation.Species,
            StartedAtUtc = DateTimeOffset.UtcNow,
            DeviceSerial = "synthetic",
            RequestedItems = 1,
            SourceDirectory = Path.GetTempPath()
        });
        await service.RecordCleanupObservationAsync(new CleanupProofObservationRecord
        {
            RunId = runId,
            Ordinal = 1,
            LocalPokemonId = $"{runId}:{localSuffix}",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Observation = observation,
            ObservationStatus = "Complete",
            IdentityConfidenceValue = 0.9,
            ProtectionConfidenceValue = 0.1,
            StableFingerprint = "fingerprint-" + localSuffix,
            ScreenshotPaths = Array.Empty<string>(),
            ScreenshotHashes = Array.Empty<string>(),
            AppraisalEvidence = Array.Empty<string>(),
            FieldEvidenceSources = new Dictionary<string, string>(StringComparer.Ordinal)
        });
    }

    private static async Task CreateVersionTwoDatabaseAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE SchemaInfo (Version INTEGER NOT NULL, AppliedAtUtc TEXT NOT NULL);
            INSERT INTO SchemaInfo VALUES (2, '2026-01-01T00:00:00Z');
            CREATE TABLE ScanRuns (RunId TEXT PRIMARY KEY, RunType TEXT NOT NULL, SearchQuery TEXT, StartedAtUtc TEXT NOT NULL, EndedAtUtc TEXT, Status TEXT NOT NULL, StopReason TEXT, DeviceSerial TEXT, ConnectionMode TEXT, ObservationProvider TEXT, RequestedItems INTEGER, ActualItems INTEGER NOT NULL DEFAULT 0, SourceDirectory TEXT);
            CREATE TABLE PokemonRecords (LocalPokemonId TEXT PRIMARY KEY, LifecycleState TEXT NOT NULL, FirstSeenRunId TEXT NOT NULL, LastSeenRunId TEXT NOT NULL, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, FormId TEXT, CostumeId TEXT, BackgroundId TEXT, IsShiny INTEGER, ShadowState TEXT, LuckyState TEXT, DynamaxState TEXT, CatchLocation TEXT, IdentityConfidence TEXT NOT NULL, ProtectionConfidence TEXT NOT NULL, CurrentRecommendation TEXT NOT NULL, RecommendationReason TEXT NOT NULL, LastScreenshotPath TEXT, LastScreenshotSha256 TEXT, LastFingerprintSha256 TEXT, ObservationStatus TEXT NOT NULL DEFAULT 'Observed', Nickname TEXT, ExistingTagsJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, VariantJson TEXT, ComparatorLocalPokemonId TEXT);
            CREATE TABLE Observations (ObservationId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, Sequence INTEGER NOT NULL, CapturedAtUtc TEXT NOT NULL, ProviderName TEXT NOT NULL, ObservationStatus TEXT NOT NULL, Confidence REAL NOT NULL, ProtectionConfidence REAL NOT NULL DEFAULT 0, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, CatchLocation TEXT, ScreenshotPath TEXT, ScreenshotSha256 TEXT, FingerprintSha256 TEXT, ObservationJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, ScreenshotPathsJson TEXT, ScreenshotHashesJson TEXT, UNIQUE(RunId, Sequence));
            CREATE TABLE InventoryEvents (EventId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, EventType TEXT NOT NULL, OccurredAtUtc TEXT NOT NULL, DetailJson TEXT);
            CREATE TABLE TagAssignments (LocalPokemonId TEXT NOT NULL, TagName TEXT NOT NULL, RequestedState TEXT NOT NULL, VerifiedState TEXT NOT NULL, RequestedAtUtc TEXT NOT NULL, VerifiedAtUtc TEXT, LastError TEXT, ActionExecuted INTEGER NOT NULL DEFAULT 0, VisuallyVerified INTEGER NOT NULL DEFAULT 0, BeforeScreenshotHash TEXT, AfterScreenshotHash TEXT, AuditReference TEXT, PRIMARY KEY(LocalPokemonId, TagName));
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadSchemaVersionAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version FROM SchemaInfo LIMIT 1";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static PokemonObservation Observation(string species) => new()
    {
        ExternalKey = "key",
        SequenceNumber = 1,
        Species = species
    };

    private static PokemonObservation ComparableObservation(string species, int cp, int attack, int defense, int hp) => new()
    {
        ExternalKey = species + "-key",
        SequenceNumber = 1,
        Species = species,
        Cp = cp,
        AttackIv = attack,
        DefenseIv = defense,
        HpIv = hp
    };

    private static PokemonObservation SemanticGroupObservation(string externalKey, string species) => new()
    {
        ExternalKey = externalKey,
        SequenceNumber = 1,
        Species = species,
        Form = "Normal",
        Costume = "None",
        IsShiny = false,
        IsShadow = false,
        IsPurified = false,
        IsBackground = false
    };

    private static void AssertTrue(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {message} to be '{expected}', got '{actual}'.");
    }
}
