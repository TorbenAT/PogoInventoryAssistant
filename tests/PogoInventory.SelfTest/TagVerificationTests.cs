using System.Reflection;
using Microsoft.Data.Sqlite;
using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Persistence;

internal static class TagVerificationTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pogo-tag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "inventory.db");
        try
        {
            AssertTrue(typeof(TagWorkflowService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == nameof(TagWorkflowService.RequestAndRecordAsync))
                .SelectMany(method => method.GetParameters())
                .All(parameter => parameter.ParameterType != typeof(bool)), "verified=true API no longer exists");

            await CreateVersionOneDatabaseAsync(databasePath);
            var persistence = new InventoryPersistenceService(databasePath);
            AssertEqual(1L, await persistence.CountObservationsAsync(), "old observation survives migration");
            AssertEqual(2L, await ReadSchemaVersionAsync(databasePath), "schema version migrates to 2");
            await persistence.InitializeAsync();
            AssertEqual(2L, await ReadSchemaVersionAsync(databasePath), "migration is idempotent");

            var service = new TagWorkflowService(databasePath);
            AssertTrue(!await service.IsVerifiedAsync("run:1", "AI-Indexed"), "old Verified row without evidence is rejected");
            await AssertRejectedAsync(service, "missing-audit", new TagExecutionResult
            {
                TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true,
                BeforeScreenshotHash = "before", AfterScreenshotHash = "after"
            });
            await AssertRejectedAsync(service, "missing-before", new TagExecutionResult
            {
                TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true,
                AfterScreenshotHash = "after", AuditReference = "audit"
            });
            await AssertRejectedAsync(service, "missing-after", new TagExecutionResult
            {
                TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true,
                BeforeScreenshotHash = "before", AuditReference = "audit"
            });
            await AssertRejectedAsync(service, "same-hashes", new TagExecutionResult
            {
                TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true,
                BeforeScreenshotHash = "same", AfterScreenshotHash = "same", AuditReference = "audit"
            });
            await AssertRejectedAsync(service, "error", new TagExecutionResult
            {
                TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true,
                BeforeScreenshotHash = "before", AfterScreenshotHash = "after", AuditReference = "audit", Error = "failed"
            });

            var valid = new TagExecutionResult
            {
                TagName = "AI-Review", ActionExecuted = true, VisuallyVerified = true,
                BeforeScreenshotHash = "before", AfterScreenshotHash = "after", AuditReference = "audit"
            };
            await service.RequestAndRecordAsync("valid", valid);
            AssertTrue(await service.IsVerifiedAsync("valid", "AI-Review"), "complete persistent evidence verifies");
            var stored = await ReadTagRowAsync(databasePath, "valid", "AI-Review");
            AssertEqual(1L, stored.ActionExecuted, "action flag persisted");
            AssertEqual(1L, stored.VisuallyVerified, "visual flag persisted");
            AssertEqual("before", stored.BeforeHash, "before hash persisted");
            AssertEqual("after", stored.AfterHash, "after hash persisted");
            AssertEqual("audit", stored.AuditReference, "audit reference persisted");

            var item = new InventoryScanItem
            {
                SequenceNumber = 1, CapturedAtUtc = DateTimeOffset.UtcNow, ScreenshotFileName = "frame.png",
                ScreenshotSha256 = "screen", IdentityFingerprintBase64 = "fingerprint", IdentityFingerprintSha256 = "fingerprint",
                ScreenStateConfidence = 1
            };
            var oldCoordinator = new RunCoordinator(databasePath);
            var oldRowCycle = await oldCoordinator.CommitObservationAndTagsAsync("run", item, "frame.png");
            AssertEqual(0, oldRowCycle.VerifiedTags.Count, "database-only Verified row is not accepted");

            var throwingCoordinator = new RunCoordinator(databasePath, new ThrowingExecutor());
            var throwingCycle = await throwingCoordinator.CommitObservationAndTagsAsync("throw", item, "frame.png");
            AssertEqual(0, throwingCycle.VerifiedTags.Count, "executor exception cannot verify");
            AssertEqual(2, throwingCycle.TagErrors.Count, "executor exception is visible for both tags");
            AssertTrue(throwingCycle.TagErrors.All(error => error.Contains("executor failure", StringComparison.Ordinal)), "executor error is preserved");
        }
        finally
        {
            // SQLite may retain a pooled handle briefly on Windows; the unique temp directory is left for cleanup.
        }
    }

    private static async Task AssertRejectedAsync(TagWorkflowService service, string id, TagExecutionResult result)
    {
        await service.RequestAndRecordAsync(id, result);
        AssertTrue(!await service.IsVerifiedAsync(id, result.TagName), $"{id} must not verify");
    }

    private static async Task CreateVersionOneDatabaseAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE SchemaInfo (Version INTEGER NOT NULL, AppliedAtUtc TEXT NOT NULL);
            INSERT INTO SchemaInfo VALUES (1, '2026-01-01T00:00:00Z');
            CREATE TABLE Observations (ObservationId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, Sequence INTEGER NOT NULL, CapturedAtUtc TEXT NOT NULL, ProviderName TEXT NOT NULL, ObservationStatus TEXT NOT NULL, Confidence REAL NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, CatchLocation TEXT, ScreenshotPath TEXT, ScreenshotSha256 TEXT, FingerprintSha256 TEXT, UNIQUE(RunId, Sequence));
            INSERT INTO Observations (LocalPokemonId, RunId, Sequence, CapturedAtUtc, ProviderName, ObservationStatus, Confidence) VALUES ('old:1', 'old', 1, '2026-01-01T00:00:00Z', 'test', 'Candidate', 1.0);
            CREATE TABLE TagAssignments (LocalPokemonId TEXT NOT NULL, TagName TEXT NOT NULL, RequestedState TEXT NOT NULL, VerifiedState TEXT NOT NULL, RequestedAtUtc TEXT NOT NULL, VerifiedAtUtc TEXT, LastError TEXT, PRIMARY KEY(LocalPokemonId, TagName));
            INSERT INTO TagAssignments VALUES ('run:1', 'AI-Indexed', 'Requested', 'Verified', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadSchemaVersionAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version FROM SchemaInfo LIMIT 1";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<TagRow> ReadTagRowAsync(string path, string id, string tag)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ActionExecuted, VisuallyVerified, BeforeScreenshotHash, AfterScreenshotHash, AuditReference FROM TagAssignments WHERE LocalPokemonId = @id AND TagName = @tag";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@tag", tag);
        await using var reader = await command.ExecuteReaderAsync();
        AssertTrue(await reader.ReadAsync(), "tag row exists");
        return new TagRow(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
    }

    private sealed record TagRow(long ActionExecuted, long VisuallyVerified, string BeforeHash, string AfterHash, string AuditReference);

    private sealed class ThrowingExecutor : IPokemonGoTagExecutor
    {
        public Task<TagExecutionResult> ExecuteAsync(string localPokemonId, string tagName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("executor failure");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}
