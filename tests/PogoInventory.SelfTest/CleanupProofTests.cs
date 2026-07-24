using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Exploration.Models;
using PogoInventory.Persistence;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

internal static class CleanupProofTests
{
    public static async Task RunAsync()
    {
        await PartialObservationDoesNotTerminateBatchAsync();
        await UnsafeObservationStopsBeforeCursorAsync();
        await DatabaseRoundTripUsesReloadedRowsAsync();
        ExactInferiorDuplicateProducesDeleteCandidate();
        UnknownProtectionProducesReview();
        FinalGroupMemberIsKept();
        NoDestructiveExecutorIsReachable();
    }

    public static async Task RunValueProofAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = Path.Combine(root, "evidence.png");
            await File.WriteAllBytesAsync(evidence, FixtureBytes());
            var baselineSeen = false;
            var fake = new FakeCleanupOperations(
                evidence,
                partial: true,
                beforeAppraisal: async () =>
                {
                    await using var connection = new SqliteConnection($"Data Source={Path.Combine(root, "cleanup-proof.sqlite")}");
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM Observations;";
                    baselineSeen |= Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
                });
            var result = await RunProofAsync(root, fake);
            AssertTrue(baselineSeen, "baseline row exists before appraisal");

            var service = new InventoryPersistenceService(Path.Combine(root, "cleanup-proof.sqlite"));
            var row = (await service.LoadCleanupProofRowsAsync(result.RunId)).First();
            var reviewed = row.Observation with { Cp = 88, AttackIv = 1, DefenseIv = 2, HpIv = 3 };
            await service.EnrichCleanupSemanticReviewAsync(
                result.RunId,
                row.LocalPokemonId,
                reviewed,
                new Dictionary<string, string> { ["Cp"] = "EvidenceReviewed" },
                "{\"Cp\":{\"source\":\"EvidenceReviewed\"}}");
            var reloaded = (await new InventoryPersistenceService(Path.Combine(root, "cleanup-proof.sqlite"))
                .LoadCleanupProofRowsAsync(result.RunId)).First();
            AssertEqual(88, reloaded.Observation.Cp, "semantic review CP transaction");

            var failedAppraisal = await RunProofAsync(
                Path.Combine(root, "failed-appraisal"),
                new FakeCleanupOperations(
                    await CreateEvidenceAsync(Path.Combine(root, "failed-appraisal")),
                    partial: true,
                    throwAppraisal: true));
            AssertTrue(failedAppraisal.CapturedItems > 0, "appraisal failure retains identity rows");

            var runnerSource = File.ReadAllText(RepositoryPath("src", "PogoInventory.Application", "CleanupProofRunner.cs"));
            AssertTrue(runnerSource.IndexOf("RecordCleanupObservationAsync", StringComparison.Ordinal) <
                runnerSource.IndexOf("CaptureCleanupAppraisalAsync", StringComparison.Ordinal),
                "baseline persistence precedes appraisal");
            var cliSource = File.ReadAllText(RepositoryPath("src", "PogoInventory.Cli", "Program.cs"));
            AssertTrue(cliSource.Contains("CanonicalCloseUnwindService", StringComparison.Ordinal), "cleanup CLI uses canonical close unwind");
            var androidSource = File.ReadAllText(RepositoryPath("src", "PogoInventory.Exploration", "Services", "AndroidVerifiedInventoryNamedOperations.cs"));
            AssertTrue(androidSource.Contains("CapturePostExitDetailsFramesAsync", StringComparison.Ordinal), "post-exit fallback exists");
            AssertTrue(File.ReadAllText(RepositoryPath("src", "PogoInventory.Application", "CleanupProofComparativeModels.cs"))
                .Contains("REQUIRES HUMAN PROTECTION REVIEW", StringComparison.Ordinal), "comparative suggestion is advisory");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static async Task RunCarouselLifecycleAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = await CreateEvidenceAsync(root);
            var fake = new FakeCleanupOperations(evidence, partial: true);
            var result = await RunProofAsync(root, fake);
            AssertEqual(6, result.CapturedItems, "carousel captures three-plus items");
            AssertEqual(1, fake.AppraisalOpenCount, "appraisal opened once");
            AssertEqual(5, fake.CurrentAppraisalCaptureCount, "current appraisal captured without reopening");
            AssertEqual(5, fake.AppraisalCarouselSwipeCount, "one appraisal swipe per following item");
            AssertEqual(0, fake.DetailsAdvanceCount, "no Details swipe between carousel items");
            AssertEqual(1, fake.ExitCount, "appraisal exited once at end");
            AssertTrue(fake.AllRowsPersistedBeforeSwipe, "each row persisted before next swipe");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task PartialObservationDoesNotTerminateBatchAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = Path.Combine(root, "evidence.png");
            await File.WriteAllBytesAsync(evidence, FixtureBytes());
            var fake = new FakeCleanupOperations(evidence, partial: true);
            var result = await RunProofAsync(root, fake);
            AssertEqual(6, result.CapturedItems, "partial batch item count");
            AssertEqual(6, result.PartialItems, "partial batch partial count");
            AssertEqual(5, fake.AdvanceCount, "partial batch cursor progressions");
            AssertTrue(File.Exists(Path.Combine(root, "recommendations.csv")), "partial recommendations CSV");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task UnsafeObservationStopsBeforeCursorAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = Path.Combine(root, "evidence.png");
            await File.WriteAllBytesAsync(evidence, FixtureBytes());
            var fake = new FakeCleanupOperations(evidence, partial: true, unresolved: true);
            var result = await RunProofAsync(root, fake);
            AssertEqual("SafeStopped", result.Status, "unsafe/unresolved stop status");
            AssertEqual(0, result.CapturedItems, "unsafe/unresolved captures");
            AssertEqual(0, fake.AdvanceCount, "unsafe/unresolved cursor inputs");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task DatabaseRoundTripUsesReloadedRowsAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var evidence = Path.Combine(root, "evidence.png");
            await File.WriteAllBytesAsync(evidence, FixtureBytes());
            var result = await RunProofAsync(root, new FakeCleanupOperations(evidence, partial: true));
            var database = Path.Combine(root, "cleanup-proof.sqlite");
            var service = new InventoryPersistenceService(database);
            var rows = await service.LoadCleanupProofRowsAsync(result.RunId);
            AssertEqual(6, rows.Count, "reloaded SQLite rows");
            AssertTrue(rows.All(row => row.CurrentRecommendation == "REVIEW"), "reloaded recommendations");
            AssertEqual(18L, result.SqlSummary.InventoryEventCount, "Observed plus AppraisalEnriched plus RecommendationGenerated events");
            var roundTrip = await File.ReadAllTextAsync(Path.Combine(root, "db-roundtrip.json"));
            AssertTrue(roundTrip.Contains("databaseReopenedBeforeAnalysis", StringComparison.Ordinal), "roundtrip marker");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ExactInferiorDuplicateProducesDeleteCandidate()
    {
        var result = new InventoryAnalyzer().Analyze(
            new[]
            {
                Exact("a", 500, 5, 5, 5, 1),
                Exact("b", 700, 14, 14, 14, 2)
            },
            new RulePolicy());
        AssertEqual(DecisionCategory.Delete, result.Decisions.Single(item => item.ExternalKey == "a").Category, "exact inferior duplicate");
    }

    private static void UnknownProtectionProducesReview()
    {
        var result = new InventoryAnalyzer().Analyze(
            new[] { new PokemonObservation { ExternalKey = "unknown", SequenceNumber = 1, Species = "Pidgey" } },
            new RulePolicy());
        AssertEqual(DecisionCategory.Review, result.Decisions.Single().Category, "unknown protection review");
    }

    private static void FinalGroupMemberIsKept()
    {
        var result = new InventoryAnalyzer().Analyze(new[] { Exact("only", 700, 15, 15, 15, 1) }, new RulePolicy());
        AssertEqual(DecisionCategory.Keep, result.Decisions.Single().Category, "final group member keep");
    }

    private static void NoDestructiveExecutorIsReachable()
    {
        var source = File.ReadAllText(RepositoryPath("src", "PogoInventory.Application", "CleanupProofRunner.cs"));
        AssertTrue(!source.Contains("ApplyIndexTag", StringComparison.Ordinal), "cleanup runner must not apply tags");
        AssertTrue(!source.Contains("Transfer", StringComparison.Ordinal), "cleanup runner must not transfer");
        AssertTrue(!source.Contains("DeleteAsync", StringComparison.Ordinal), "cleanup runner must not delete");
    }

    private static async Task<CleanupProofRunResult> RunProofAsync(string root, FakeCleanupOperations fake)
    {
        var request = new CleanupProofRequest
        {
            SpeciesQuery = "Pidgey",
            ItemLimit = 6,
            DatabasePath = Path.Combine(root, "cleanup-proof.sqlite"),
            OutputDirectory = root,
            DeviceSerial = "synthetic",
            ContinueOnPartial = true
        };
        return await new CleanupProofRunner().RunAsync(fake, request);
    }

    /// <summary>Internal (not private) so other test files in this assembly (e.g. HeaderSemanticsCleanupTests) can reuse it.</summary>
    internal sealed class FakeCleanupOperations : ICleanupProofNamedOperations
    {
        private readonly string _evidence;
        private readonly bool _partial;
        private readonly bool _unresolved;
        private readonly Func<Task>? _beforeAppraisal;
        private readonly bool _throwAppraisal;
        private readonly Func<CleanupProofAppraisalCapture>? _appraisalOverride;
        private readonly Func<int, VerifiedTagObservation>? _tagObservationOverride;
        private int _tagReadCount;
        public int AdvanceCount { get; private set; }
        public int AppraisalOpenCount { get; private set; }
        public int CurrentAppraisalCaptureCount { get; private set; }
        public int AppraisalCarouselSwipeCount { get; private set; }
        public int DetailsAdvanceCount { get; private set; }
        public int ExitCount { get; private set; }
        public bool AllRowsPersistedBeforeSwipe { get; private set; } = true;

        public FakeCleanupOperations(
            string evidence,
            bool partial,
            bool unresolved = false,
            Func<Task>? beforeAppraisal = null,
            bool throwAppraisal = false,
            Func<CleanupProofAppraisalCapture>? appraisalOverride = null,
            Func<int, VerifiedTagObservation>? tagObservationOverride = null)
        {
            _evidence = evidence;
            _partial = partial;
            _unresolved = unresolved;
            _beforeAppraisal = beforeAppraisal;
            _throwAppraisal = throwAppraisal;
            _appraisalOverride = appraisalOverride;
            _tagObservationOverride = tagObservationOverride;
        }

        public Task<VerifiedSequenceState> EnsureFilteredInventoryAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(VerifiedSequenceState.Inventory);

        public Task<VerifiedSequenceState> OpenFirstPokemonAsync(CancellationToken cancellationToken) =>
            Task.FromResult(VerifiedSequenceState.PokemonDetails);

        public Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Consensus(PokemonIdentityObservationStatus.Complete, "partial-fingerprint"));

        public Task<CleanupProofIdentityCapture> CaptureCleanupIdentityAsync(int maximumFrames, int minimumCompleteFrames, int minimumPartialFrames, CancellationToken cancellationToken) =>
            Task.FromResult(_unresolved
                ? new CleanupProofIdentityCapture
                {
                    Consensus = Consensus(PokemonIdentityObservationStatus.Unavailable, string.Empty),
                    Status = CleanupProofObservationStatus.Unresolved,
                    ScreenshotPaths = Array.Empty<string>(),
                    ScreenshotHashes = Array.Empty<string>(),
                    FailureReasons = new[] { "UnsafeConfirmation:PowerUp" }
                }
                : new CleanupProofIdentityCapture
                {
                    Consensus = Consensus(_partial ? PokemonIdentityObservationStatus.Partial : PokemonIdentityObservationStatus.Complete, "partial-fingerprint"),
                    Status = _partial ? CleanupProofObservationStatus.Partial : CleanupProofObservationStatus.Complete,
                    ScreenshotPaths = new[] { _evidence, _evidence },
                    ScreenshotHashes = new[] { Hash(_evidence), Hash(_evidence) }
                });

        public async Task<CleanupProofAppraisalCapture> CaptureCleanupAppraisalAsync(CancellationToken cancellationToken)
        {
            AppraisalOpenCount++;
            if (_beforeAppraisal is not null)
                await _beforeAppraisal();
            if (_throwAppraisal)
                throw new InvalidOperationException("synthetic appraisal failure");
            return _appraisalOverride?.Invoke() ?? new CleanupProofAppraisalCapture { Status = "Partial", EvidencePaths = new[] { _evidence } };
        }

        public Task<CleanupProofIdentityCapture> CaptureCleanupAppraisalIdentityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CleanupProofIdentityCapture
            {
                Consensus = Consensus(_partial ? PokemonIdentityObservationStatus.Partial : PokemonIdentityObservationStatus.Complete, "appraisal-fingerprint"),
                Status = _partial ? CleanupProofObservationStatus.Partial : CleanupProofObservationStatus.Complete,
                ScreenshotPaths = new[] { _evidence, _evidence, _evidence },
                ScreenshotHashes = new[] { Hash(_evidence), Hash(_evidence), Hash(_evidence) }
            });

        public Task<CleanupProofAppraisalCapture> CaptureCurrentCleanupAppraisalAsync(CancellationToken cancellationToken) =>
            CaptureCurrentAsync();

        private Task<CleanupProofAppraisalCapture> CaptureCurrentAsync()
        {
            CurrentAppraisalCaptureCount++;
            return Task.FromResult(_appraisalOverride?.Invoke() ?? new CleanupProofAppraisalCapture { Status = "Partial", EvidencePaths = new[] { _evidence } });
        }

        public Task<AppraisalCarouselAdvanceResult> AdvanceToNextPokemonInAppraisalAsync(string previousAppraisalFingerprint, CancellationToken cancellationToken)
        {
            if (CurrentAppraisalCaptureCount + 1 != AppraisalCarouselSwipeCount + 1)
                AllRowsPersistedBeforeSwipe = false;
            AppraisalCarouselSwipeCount++;
            AdvanceCount++;
            return Task.FromResult(AppraisalCarouselAdvanceResult.SUCCESS_CHANGED_POKEMON);
        }

        public Task<string> CloseInventoryAsync(CancellationToken cancellationToken) => Task.FromResult("GameplayMap");

        public Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken) => Task.FromResult("Partial");

        public Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken)
        {
            ExitCount++;
            return Task.FromResult(VerifiedSequenceState.PokemonDetails);
        }

        public Task<VerifiedTagObservation> ReadTagObservationAsync(CancellationToken cancellationToken)
        {
            _tagReadCount++;
            return Task.FromResult(_tagObservationOverride?.Invoke(_tagReadCount) ??
                new VerifiedTagObservation { TagCount = 0, NamesComplete = true, Section = null });
        }

        public Task<VerifiedSequenceState> AdvanceToNextPokemonAsync(PokemonIdentityConsensus previous, CancellationToken cancellationToken)
        {
            DetailsAdvanceCount++;
            AdvanceCount++;
            return Task.FromResult(VerifiedSequenceState.PokemonDetails);
        }

        public Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken) => Task.FromResult(VerifiedSequenceState.Inventory);

        public Task<IReadOnlyList<string>> ApplyIndexTagAsync(string tagName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ApplyClassificationTagAsync(string tagName, CancellationToken cancellationToken) => throw new NotSupportedException();

        private PokemonIdentityConsensus Consensus(PokemonIdentityObservationStatus status, string fingerprint) => new()
        {
            Status = status,
            StableFingerprintSha256 = fingerprint,
            StableFingerprintBase64 = fingerprint,
            Confidence = status == PokemonIdentityObservationStatus.Complete ? 0.85 : 0.55,
            Frames = Array.Empty<PokemonIdentityFingerprintObservation>(),
            EvidenceHashes = new[] { Hash(_evidence), Hash(_evidence) },
            Tags = new PokemonIdentityTagObservation { TagCount = 0, Section = null, IsSeparateFromIdentity = true },
            IgnoredFrameCount = 0
        };
    }

    private static PokemonObservation Exact(string key, int cp, int attack, int defense, int hp, int sequence) => new()
    {
        ExternalKey = key,
        SequenceNumber = sequence,
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

    internal static byte[] FixtureBytes() => File.ReadAllBytes(RepositoryPath("data", "screen-fixtures", "PokemonDetails.png"));

    internal static async Task<string> CreateEvidenceAsync(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "evidence.png");
        await File.WriteAllBytesAsync(path, FixtureBytes());
        return path;
    }

    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        return parts.Aggregate(directory.FullName, Path.Combine);
    }

    internal static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PogoInventoryAssistant", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    internal static void DeleteDirectory(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

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
