using System.Security.Cryptography;
using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.HeaderText;
using PogoInventory.Persistence;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Wave-2 tests for the semantic extraction wired into
/// <see cref="CleanupProofRunner"/> (species/CP header-OCR consensus, IV
/// multi-frame consensus unlocking Complete) and the offline
/// <see cref="CleanupEvidenceReprocessor"/>. Uses <see cref="FakeTextRecognizer"/>
/// style scripted recognizers -- never a real OCR engine -- and
/// <see cref="CleanupProofTests.FakeCleanupOperations"/> so these tests stay
/// independent of PogoInventory.HeaderOcr / PogoInventory.Cli (Windows-only TFM).
/// </summary>
internal static class HeaderSemanticsCleanupTests
{
    public static async Task RunAsync()
    {
        await BroadQueryNeverStoredAsSpeciesAsync();
        await ExactQueryProducesQueryDerivedSpeciesAsync();
        await HeaderConsensusProducesAutomatedSpeciesAndCpAsync();
        await ConflictingHeaderFramesProduceUnknownSpeciesAsync();
        await IvConsensusProducesCompleteObservationAsync();
        await IvDisagreementKeepsPartialAsync();
        await PolicyFileLoadingAffectsRecommendationsAsync();
        await AnalyzeCleanupEvidenceEndToEndAsync();
    }

    public static async Task BroadQueryNeverStoredAsSpeciesAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "age0-1825",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = new StaticSpeciesReference(new[] { "Pidgey" })
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            AssertTrue(rows.Count > 0, "rows captured");
            foreach (var row in rows)
            {
                AssertEqual("Unknown", row.Observation.Species, "broad query must never become species");
                AssertEqual("Unknown", row.FieldEvidenceSources["Species"], "broad query species evidence");
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task ExactQueryProducesQueryDerivedSpeciesAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = new StaticSpeciesReference(new[] { "Pidgey" })
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            foreach (var row in rows)
            {
                AssertEqual("Pidgey", row.Observation.Species, "exact species query becomes species");
                AssertEqual("QueryDerived", row.FieldEvidenceSources["Species"], "exact species evidence");
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task HeaderConsensusProducesAutomatedSpeciesAndCpAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var recognizer = new SequencedHeaderRecognizer(
                cpTexts: new[] { "CP1234", "CP1234" },
                nameTexts: new[] { "Bulbasaur", "Bulbasaur" });
            var speciesReference = new StaticSpeciesReference(new[] { "Bulbasaur" });
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "0*,1*,2*", // broad filter: not an exact species
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = speciesReference,
                HeaderAnalyzer = new PokemonHeaderAnalyzer(recognizer, speciesReference)
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            var first = rows.Single(row => row.Ordinal == 1);
            AssertEqual("Bulbasaur", first.Observation.Species, "header consensus species");
            AssertEqual("Automated", first.FieldEvidenceSources["Species"], "header consensus species evidence");
            AssertEqual(1234, first.Observation.Cp, "header consensus CP");
            AssertEqual("Automated", first.FieldEvidenceSources["Cp"], "header consensus CP evidence");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task ConflictingHeaderFramesProduceUnknownSpeciesAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var fake = new CleanupProofTests.FakeCleanupOperations(evidence, partial: true);
            var recognizer = new SequencedHeaderRecognizer(
                cpTexts: new[] { "CP500", "CP500" },
                nameTexts: new[] { "Bulbasaur", "Charmander" });
            var speciesReference = new StaticSpeciesReference(new[] { "Bulbasaur", "Charmander" });
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "age0-1825",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = speciesReference,
                HeaderAnalyzer = new PokemonHeaderAnalyzer(recognizer, speciesReference)
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            var first = rows.Single(row => row.Ordinal == 1);
            AssertEqual("Unknown", first.Observation.Species, "conflicting frames yield unknown species");
            AssertEqual("Unknown", first.FieldEvidenceSources["Species"], "conflicting frames species evidence");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task IvConsensusProducesCompleteObservationAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var recognizer = new SequencedHeaderRecognizer(
                cpTexts: new[] { "CP1234", "CP1234" },
                nameTexts: new[] { "Pidgey", "Pidgey" });
            var speciesReference = new StaticSpeciesReference(new[] { "Pidgey" });
            var fake = new CleanupProofTests.FakeCleanupOperations(
                evidence,
                partial: true,
                appraisalOverride: () => new CleanupProofAppraisalCapture
                {
                    Status = "Partial",
                    EvidencePaths = new[] { evidence },
                    Frames = new[]
                    {
                        Frame(10, 11, 12),
                        Frame(10, 11, 12),
                        Frame(9, 11, 12)
                    }
                });
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = speciesReference,
                HeaderAnalyzer = new PokemonHeaderAnalyzer(recognizer, speciesReference)
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            var first = rows.Single(row => row.Ordinal == 1);
            AssertEqual((int?)10, first.Observation.AttackIv, "IV consensus attack");
            AssertEqual((int?)11, first.Observation.DefenseIv, "IV consensus defense");
            AssertEqual((int?)12, first.Observation.HpIv, "IV consensus hp");
            AssertEqual("Automated", first.FieldEvidenceSources["AttackIv"], "IV consensus attack evidence");
            AssertEqual("Automated", first.FieldEvidenceSources["DefenseIv"], "IV consensus defense evidence");
            AssertEqual("Automated", first.FieldEvidenceSources["HpIv"], "IV consensus hp evidence");
            AssertEqual("Complete", first.ObservationStatus, "species+cp+iv known -> Complete");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task IvDisagreementKeepsPartialAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var recognizer = new SequencedHeaderRecognizer(
                cpTexts: new[] { "CP1234", "CP1234" },
                nameTexts: new[] { "Pidgey", "Pidgey" });
            var speciesReference = new StaticSpeciesReference(new[] { "Pidgey" });
            var fake = new CleanupProofTests.FakeCleanupOperations(
                evidence,
                partial: true,
                appraisalOverride: () => new CleanupProofAppraisalCapture
                {
                    Status = "Partial",
                    EvidencePaths = new[] { evidence },
                    Frames = new[]
                    {
                        Frame(10, 11, 12),
                        Frame(5, 6, 7),
                        Frame(1, 2, 3)
                    }
                });
            var request = new CleanupProofRequest
            {
                SpeciesQuery = "Pidgey",
                ItemLimit = 6,
                DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
                OutputDirectory = root,
                DeviceSerial = "synthetic",
                ContinueOnPartial = true,
                SpeciesReference = speciesReference,
                HeaderAnalyzer = new PokemonHeaderAnalyzer(recognizer, speciesReference)
            };
            var result = await new CleanupProofRunner().RunAsync(fake, request);
            var rows = await new InventoryPersistenceService(request.DatabasePath).LoadCleanupProofRowsAsync(result.RunId);
            var first = rows.Single(row => row.Ordinal == 1);
            AssertEqual((int?)null, first.Observation.AttackIv, "IV disagreement stays unknown, never guessed");
            AssertEqual("Unknown", first.FieldEvidenceSources["AttackIv"], "IV disagreement evidence");
            AssertEqual("Partial", first.ObservationStatus, "IV disagreement keeps Partial identity status");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    /// <summary>
    /// Confirms the previously hardcoded <c>new RulePolicy()</c> in the
    /// analysis path is now genuinely configurable: a synthetic database with
    /// two fully-known (HasKnownCriticalValues, Exact identity) duplicate
    /// Pidgey records is reprocessed twice with different
    /// <c>MinimumOrdinaryCopiesPerSpeciesForm</c> policies, and the KEEP count
    /// changes accordingly (this needs fully-known variant data, which only a
    /// directly-authored synthetic database -- not the live OCR/appraisal
    /// pipeline, which does not yet populate variant booleans -- can provide).
    /// </summary>
    public static async Task PolicyFileLoadingAffectsRecommendationsAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidenceRoot = Path.Combine(root, "evidence");
            Directory.CreateDirectory(evidenceRoot);
            var evidencePath = Path.Combine(evidenceRoot, "frame.png");
            await File.WriteAllBytesAsync(evidencePath, CleanupProofTests.FixtureBytes());

            var sourceDatabase = Path.Combine(root, "source", "cleanup-proof.sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceDatabase)!);
            await BuildSyntheticExactDuplicatePairDatabaseAsync(sourceDatabase, evidencePath);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var speciesReference = new StaticSpeciesReference(new[] { "Pidgey" });

            var permissiveSummary = await CleanupEvidenceReprocessor.ReprocessAsync(new CleanupEvidenceReprocessRequest
            {
                SourceDatabasePath = sourceDatabase,
                EvidenceRoot = evidenceRoot,
                OutputDirectory = Path.Combine(root, "permissive"),
                SpeciesReference = speciesReference,
                Policy = new RulePolicy { MinimumOrdinaryCopiesPerSpeciesForm = 2 }
            });
            var strictSummary = await CleanupEvidenceReprocessor.ReprocessAsync(new CleanupEvidenceReprocessRequest
            {
                SourceDatabasePath = sourceDatabase,
                EvidenceRoot = evidenceRoot,
                OutputDirectory = Path.Combine(root, "strict"),
                SpeciesReference = speciesReference,
                Policy = new RulePolicy { MinimumOrdinaryCopiesPerSpeciesForm = 1 }
            });

            var permissiveRows = await new InventoryPersistenceService(permissiveSummary.NewDatabasePath)
                .LoadCleanupProofRowsAsync("legacy-pair-run-000001");
            var strictRows = await new InventoryPersistenceService(strictSummary.NewDatabasePath)
                .LoadCleanupProofRowsAsync("legacy-pair-run-000001");

            var permissiveKeeps = permissiveRows.Count(row => row.CurrentRecommendation == "KEEP");
            var strictKeeps = strictRows.Count(row => row.CurrentRecommendation == "KEEP");

            AssertEqual(2, permissiveKeeps, "policy allowing 2 ordinary copies keeps both");
            AssertEqual(1, strictKeeps, "policy allowing 1 ordinary copy keeps only the best");
            AssertTrue(strictRows.Any(row => row.CurrentRecommendation == "DELETE-CANDIDATE"),
                "the stricter policy must produce a delete candidate for the inferior duplicate");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task BuildSyntheticExactDuplicatePairDatabaseAsync(string databasePath, string evidencePath)
    {
        var persistence = new InventoryPersistenceService(databasePath);
        const string runId = "legacy-pair-run-000001";
        await persistence.StartCleanupRunAsync(new CleanupProofRunStart
        {
            RunId = runId,
            SearchQuery = "Pidgey",
            StartedAtUtc = DateTimeOffset.UtcNow,
            DeviceSerial = "legacy-device",
            RequestedItems = 2,
            SourceDirectory = Path.GetDirectoryName(databasePath)!
        });
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(evidencePath))).ToLowerInvariant();

        await PersistExactPidgeyAsync(persistence, runId, ordinal: 1, cp: 900, attack: 14, defense: 14, hp: 14, evidencePath, hash);
        await PersistExactPidgeyAsync(persistence, runId, ordinal: 2, cp: 500, attack: 8, defense: 8, hp: 8, evidencePath, hash);

        await persistence.CompleteCleanupRunAsync(runId, 2, "Completed", "ItemLimitReached", DateTimeOffset.UtcNow);
    }

    private static async Task PersistExactPidgeyAsync(
        InventoryPersistenceService persistence,
        string runId,
        int ordinal,
        int cp,
        int attack,
        int defense,
        int hp,
        string evidencePath,
        string hash)
    {
        var observation = new PokemonObservation
        {
            ExternalKey = $"{runId}:{ordinal:D6}",
            SequenceNumber = ordinal,
            Species = "Pidgey",
            Form = "Normal",
            Costume = "None",
            Cp = cp,
            AttackIv = attack,
            DefenseIv = defense,
            HpIv = hp,
            CatchDate = new DateOnly(2026, 7, 1),
            IsShiny = false,
            IsLegendary = false,
            IsMythical = false,
            IsUltraBeast = false,
            IsBackground = false,
            IsFavorite = false,
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
            Tags = Array.Empty<string>(),
            VariantIdentity = new PokemonVariantIdentity
            {
                SpeciesId = 16,
                SpeciesName = "Pidgey",
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
        var record = new CleanupProofObservationRecord
        {
            RunId = runId,
            Ordinal = ordinal,
            LocalPokemonId = $"{runId}:{ordinal:D6}",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Observation = observation,
            ObservationStatus = "Complete",
            IdentityConfidenceValue = 0.95,
            ProtectionConfidenceValue = 0.10,
            StableFingerprint = $"legacy-fingerprint-{ordinal}",
            ScreenshotPaths = new[] { evidencePath, evidencePath },
            ScreenshotHashes = new[] { hash, hash },
            AppraisalEvidence = new[] { evidencePath },
            FieldEvidenceSources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Species"] = "QueryDerived",
                ["Cp"] = "Automated",
                ["AttackIv"] = "Automated",
                ["DefenseIv"] = "Automated",
                ["HpIv"] = "Automated"
            }
        };
        await persistence.RecordCleanupObservationAsync(record);
    }

    public static async Task AnalyzeCleanupEvidenceEndToEndAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidenceRoot = Path.Combine(root, "evidence");
            Directory.CreateDirectory(evidenceRoot);
            var evidencePath = Path.Combine(evidenceRoot, "frame.png");
            await File.WriteAllBytesAsync(evidencePath, CleanupProofTests.FixtureBytes());

            var sourceDatabase = Path.Combine(root, "source", "cleanup-proof.sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceDatabase)!);
            await BuildSyntheticLegacyDatabaseAsync(sourceDatabase, evidencePath);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var sourceBytesBefore = await File.ReadAllBytesAsync(sourceDatabase);

            var speciesReference = new StaticSpeciesReference(new[] { "Bulbasaur" });
            var recognizer = new SequencedHeaderRecognizer(
                cpTexts: new[] { "CP987", "CP987" },
                nameTexts: new[] { "Bulbasaur", "Bulbasaur" });
            var outputDirectory = Path.Combine(root, "reprocessed");

            var summary = await CleanupEvidenceReprocessor.ReprocessAsync(new CleanupEvidenceReprocessRequest
            {
                SourceDatabasePath = sourceDatabase,
                EvidenceRoot = evidenceRoot,
                OutputDirectory = outputDirectory,
                SpeciesReference = speciesReference,
                HeaderAnalyzer = new PokemonHeaderAnalyzer(recognizer, speciesReference)
            });

            // Original database must be byte-for-byte untouched.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var sourceBytesAfter = await File.ReadAllBytesAsync(sourceDatabase);
            AssertTrue(sourceBytesBefore.SequenceEqual(sourceBytesAfter), "original database must never be modified");
            var originalRows = await new InventoryPersistenceService(sourceDatabase).LoadCleanupProofRowsAsync("legacy-run-000001");
            AssertEqual("age0-1825", originalRows.Single().Observation.Species, "original defect data unchanged");

            AssertEqual(1, summary.TotalRows, "coverage total rows");
            AssertEqual(1, summary.SpeciesExtracted, "coverage species extracted");
            AssertEqual(0, summary.SpeciesUnknown, "coverage species unknown");
            AssertEqual(1, summary.CpExtracted, "coverage cp extracted");
            AssertEqual(0, summary.RowsWithQueryAsSpecies, "no row may keep the raw query as species");

            AssertTrue(File.Exists(summary.NewDatabasePath), "new database exists");
            AssertTrue(!string.Equals(Path.GetFullPath(summary.NewDatabasePath), Path.GetFullPath(sourceDatabase), StringComparison.OrdinalIgnoreCase),
                "new database is a distinct file");
            var reprocessedRows = await new InventoryPersistenceService(summary.NewDatabasePath).LoadCleanupProofRowsAsync("legacy-run-000001");
            var reprocessedRow = reprocessedRows.Single();
            AssertEqual("Bulbasaur", reprocessedRow.Observation.Species, "reprocessed species corrected via header OCR consensus");
            AssertEqual("Automated", reprocessedRow.FieldEvidenceSources["Species"], "reprocessed species evidence");
            AssertEqual(987, reprocessedRow.Observation.Cp, "reprocessed CP corrected via header OCR consensus");

            AssertTrue(File.Exists(Path.Combine(outputDirectory, "species-cp-coverage.json")), "coverage report written");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "recommendations.csv")), "recommendations report written");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "comparative-cleanup-suggestions.csv")), "comparative report written");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "group-summary.json")), "group summary written");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "db-roundtrip.json")), "db roundtrip report written");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    /// <summary>
    /// Builds a synthetic v3-schema cleanup-proof database that reproduces the
    /// original documented defect directly via the persistence API (bypassing
    /// the now-fixed <see cref="CleanupProofRunner"/>): a broad-filter query
    /// ("age0-1825") stored verbatim as the species name, CP/IVs unknown.
    /// </summary>
    private static async Task BuildSyntheticLegacyDatabaseAsync(string databasePath, string evidencePath)
    {
        var persistence = new InventoryPersistenceService(databasePath);
        const string runId = "legacy-run-000001";
        await persistence.StartCleanupRunAsync(new CleanupProofRunStart
        {
            RunId = runId,
            SearchQuery = "age0-1825",
            StartedAtUtc = DateTimeOffset.UtcNow,
            DeviceSerial = "legacy-device",
            RequestedItems = 1,
            SourceDirectory = Path.GetDirectoryName(databasePath)!
        });

        var observation = new PokemonObservation
        {
            ExternalKey = $"{runId}:000001",
            SequenceNumber = 1,
            Species = "age0-1825", // the original defect: raw query stored as species
            Tags = Array.Empty<string>()
        };
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(evidencePath))).ToLowerInvariant();
        var record = new CleanupProofObservationRecord
        {
            RunId = runId,
            Ordinal = 1,
            LocalPokemonId = $"{runId}:000001",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Observation = observation,
            ObservationStatus = "Partial",
            IdentityConfidenceValue = 0.55,
            ProtectionConfidenceValue = 0.10,
            StableFingerprint = "legacy-fingerprint",
            ScreenshotPaths = new[] { evidencePath, evidencePath },
            ScreenshotHashes = new[] { hash, hash },
            AppraisalEvidence = new[] { "AppraisalStatus:Pending" },
            FieldEvidenceSources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Species"] = "QueryDerived",
                ["Cp"] = "Unknown"
            }
        };
        await persistence.RecordCleanupObservationAsync(record);
        await persistence.CompleteCleanupRunAsync(runId, 1, "Completed", "ItemLimitReached", DateTimeOffset.UtcNow);
    }

    private static AppraisalFrameIv Frame(int attack, int defense, int hp) => new()
    {
        AttackIv = attack,
        DefenseIv = defense,
        HpIv = hp,
        BarsConfident = true
    };

    private static async Task<string> CreateEvidenceAsync(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "evidence.png");
        await File.WriteAllBytesAsync(path, CleanupProofTests.FixtureBytes());
        return path;
    }

    private static string CreateTemporaryDirectory() => CleanupProofTests.CreateTemporaryDirectory();

    private static void DeleteDirectory(string path) => CleanupProofTests.DeleteDirectory(path);

    private static void AssertTrue(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {message} to be '{expected}', got '{actual}'.");
    }

    /// <summary>
    /// A scripted <see cref="ITextRecognizer"/> that returns successive texts
    /// from separate CP/name queues in call order, so a test can script
    /// disagreement across the frames a single item's multi-frame consensus
    /// will see. After each queue is exhausted, later calls (for later
    /// captured items in a >= 6-item batch) return no text, which is safe
    /// (Unknown) and unasserted by these tests.
    /// </summary>
    private sealed class SequencedHeaderRecognizer : ITextRecognizer
    {
        private readonly Queue<string?> _cpQueue;
        private readonly Queue<string?> _nameQueue;

        public SequencedHeaderRecognizer(IEnumerable<string?> cpTexts, IEnumerable<string?> nameTexts)
        {
            _cpQueue = new Queue<string?>(cpTexts);
            _nameQueue = new Queue<string?>(nameTexts);
        }

        public Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(
            byte[] framePng,
            NormalizedRegion roi,
            CancellationToken cancellationToken = default)
        {
            var queue = roi.Y < 0.2 ? _cpQueue : _nameQueue;
            var text = queue.Count > 0 ? queue.Dequeue() : null;
            return Task.FromResult(LineFor(text));
        }

        private static IReadOnlyList<RecognizedTextLine> LineFor(string? text) =>
            string.IsNullOrEmpty(text)
                ? Array.Empty<RecognizedTextLine>()
                : new[]
                {
                    new RecognizedTextLine
                    {
                        Text = text,
                        Confidence = 0.9,
                        NormalizedBounds = new NormalizedRegion { X = 0.1, Y = 0.1, Width = 0.5, Height = 0.05 }
                    }
                };
    }
}
