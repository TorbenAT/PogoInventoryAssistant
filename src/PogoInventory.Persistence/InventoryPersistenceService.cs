using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using PogoInventory.Automation.Models;
using PogoInventory.Core.Models;

namespace PogoInventory.Persistence;

public sealed class InventoryPersistenceService
{
    private const int SchemaVersion = 6;
    private readonly string _databasePath;

    public InventoryPersistenceService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS SchemaInfo (Version INTEGER NOT NULL, AppliedAtUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ScanRuns (RunId TEXT PRIMARY KEY, RunType TEXT NOT NULL, SearchQuery TEXT, StartedAtUtc TEXT NOT NULL, EndedAtUtc TEXT, Status TEXT NOT NULL, StopReason TEXT, DeviceSerial TEXT, ConnectionMode TEXT, ObservationProvider TEXT, RequestedItems INTEGER, ActualItems INTEGER NOT NULL DEFAULT 0, SourceDirectory TEXT);
            CREATE TABLE IF NOT EXISTS PokemonRecords (LocalPokemonId TEXT PRIMARY KEY, LifecycleState TEXT NOT NULL, FirstSeenRunId TEXT NOT NULL, LastSeenRunId TEXT NOT NULL, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, FormId TEXT, CostumeId TEXT, BackgroundId TEXT, IsShiny INTEGER, ShadowState TEXT, LuckyState TEXT, DynamaxState TEXT, CatchLocation TEXT, IdentityConfidence TEXT NOT NULL, ProtectionConfidence TEXT NOT NULL, CurrentRecommendation TEXT NOT NULL, RecommendationReason TEXT NOT NULL, LastScreenshotPath TEXT, LastScreenshotSha256 TEXT, LastFingerprintSha256 TEXT, ObservationStatus TEXT NOT NULL DEFAULT 'Observed', Nickname TEXT, ExistingTagsJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, VariantJson TEXT, ProtectionJson TEXT);
            CREATE TABLE IF NOT EXISTS Observations (ObservationId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, Sequence INTEGER NOT NULL, CapturedAtUtc TEXT NOT NULL, ProviderName TEXT NOT NULL, ObservationStatus TEXT NOT NULL, Confidence REAL NOT NULL, ProtectionConfidence REAL NOT NULL DEFAULT 0, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, CatchLocation TEXT, ScreenshotPath TEXT, ScreenshotSha256 TEXT, FingerprintSha256 TEXT, ObservationJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, ScreenshotPathsJson TEXT, ScreenshotHashesJson TEXT, ProtectionJson TEXT, UNIQUE(RunId, Sequence));
            CREATE TABLE IF NOT EXISTS InventoryEvents (EventId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, EventType TEXT NOT NULL, OccurredAtUtc TEXT NOT NULL, DetailJson TEXT);
            CREATE TABLE IF NOT EXISTS TagAssignments (LocalPokemonId TEXT NOT NULL, TagName TEXT NOT NULL, RequestedState TEXT NOT NULL, VerifiedState TEXT NOT NULL, RequestedAtUtc TEXT NOT NULL, VerifiedAtUtc TEXT, LastError TEXT, ActionExecuted INTEGER NOT NULL DEFAULT 0, VisuallyVerified INTEGER NOT NULL DEFAULT 0, BeforeScreenshotHash TEXT, AfterScreenshotHash TEXT, AuditReference TEXT, PRIMARY KEY(LocalPokemonId, TagName));
            CREATE TABLE IF NOT EXISTS WorkBuckets (LogicalBucketId TEXT PRIMARY KEY, AbsoluteDateStart TEXT NOT NULL, AbsoluteDateEnd TEXT NOT NULL, PokedexStart INTEGER, PokedexEnd INTEGER, DerivedPhoneQuery TEXT NOT NULL, Status TEXT NOT NULL, StartedAtUtc TEXT, CompletedAtUtc TEXT, ItemsObserved INTEGER NOT NULL DEFAULT 0, ItemsIndexed INTEGER NOT NULL DEFAULT 0, ItemsReview INTEGER NOT NULL DEFAULT 0, ItemsDeleteCandidate INTEGER NOT NULL DEFAULT 0, Failures INTEGER NOT NULL DEFAULT 0, Retries INTEGER NOT NULL DEFAULT 0, LastSuccessfulItem TEXT, CompletionEvidenceJson TEXT);
            CREATE TABLE IF NOT EXISTS WorkItems (LogicalBucketId TEXT NOT NULL, LocalPokemonId TEXT NOT NULL, State TEXT NOT NULL, Disposition TEXT NOT NULL, ExactBindingEvidence TEXT, UpdatedAtUtc TEXT NOT NULL, LastError TEXT, PRIMARY KEY(LogicalBucketId, LocalPokemonId), FOREIGN KEY(LogicalBucketId) REFERENCES WorkBuckets(LogicalBucketId));
            CREATE TABLE IF NOT EXISTS WorkAttempts (AttemptId INTEGER PRIMARY KEY AUTOINCREMENT, LogicalBucketId TEXT NOT NULL, LocalPokemonId TEXT NOT NULL, AttemptKind TEXT NOT NULL, Result TEXT NOT NULL, OccurredAtUtc TEXT NOT NULL, DetailJson TEXT, FOREIGN KEY(LogicalBucketId, LocalPokemonId) REFERENCES WorkItems(LogicalBucketId, LocalPokemonId));
            CREATE TABLE IF NOT EXISTS SearchOracleEvidence (EvidenceId INTEGER PRIMARY KEY AUTOINCREMENT, LogicalBucketId TEXT, RunId TEXT, Query TEXT NOT NULL, Outcome TEXT NOT NULL, ExpectedResultCount INTEGER, ObservedResultCount INTEGER, EmptyVerified INTEGER NOT NULL DEFAULT 0, ObservedAtUtc TEXT NOT NULL, EvidencePath TEXT, DetailJson TEXT, FOREIGN KEY(LogicalBucketId) REFERENCES WorkBuckets(LogicalBucketId));
            CREATE INDEX IF NOT EXISTS IX_WorkBuckets_Frontier ON WorkBuckets(Status, AbsoluteDateStart, AbsoluteDateEnd, LogicalBucketId);
            CREATE INDEX IF NOT EXISTS IX_WorkItems_State ON WorkItems(LogicalBucketId, State, UpdatedAtUtc);
            CREATE INDEX IF NOT EXISTS IX_SearchOracleEvidence_Bucket ON SearchOracleEvidence(LogicalBucketId, ObservedAtUtc);
            INSERT INTO SchemaInfo (Version, AppliedAtUtc) SELECT 1, @now WHERE NOT EXISTS (SELECT 1 FROM SchemaInfo);
            """;
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var columnsCommand = connection.CreateCommand())
        {
            columnsCommand.CommandText = "PRAGMA table_info(TagAssignments);";
            await using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var migrationStatements = new[]
        {
            ("ActionExecuted", "ALTER TABLE TagAssignments ADD COLUMN ActionExecuted INTEGER NOT NULL DEFAULT 0;"),
            ("VisuallyVerified", "ALTER TABLE TagAssignments ADD COLUMN VisuallyVerified INTEGER NOT NULL DEFAULT 0;"),
            ("BeforeScreenshotHash", "ALTER TABLE TagAssignments ADD COLUMN BeforeScreenshotHash TEXT;"),
            ("AfterScreenshotHash", "ALTER TABLE TagAssignments ADD COLUMN AfterScreenshotHash TEXT;"),
            ("AuditReference", "ALTER TABLE TagAssignments ADD COLUMN AuditReference TEXT;")
        };
        foreach (var (column, statement) in migrationStatements)
        {
            if (!existingColumns.Contains(column))
            {
                await using var migrationCommand = connection.CreateCommand();
                migrationCommand.CommandText = statement;
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        foreach (var (table, column, declaration) in new[]
        {
            ("PokemonRecords", "ObservationStatus", "TEXT NOT NULL DEFAULT 'Observed'"),
            ("PokemonRecords", "Nickname", "TEXT"),
            ("PokemonRecords", "ExistingTagsJson", "TEXT"),
            ("PokemonRecords", "FieldEvidenceJson", "TEXT"),
            ("PokemonRecords", "AppraisalEvidenceJson", "TEXT"),
            ("PokemonRecords", "VariantJson", "TEXT"),
            ("PokemonRecords", "ComparatorLocalPokemonId", "TEXT"),
            ("PokemonRecords", "ProtectionJson", "TEXT"),
            ("Observations", "ProtectionConfidence", "REAL NOT NULL DEFAULT 0"),
            ("Observations", "ObservationJson", "TEXT"),
            ("Observations", "FieldEvidenceJson", "TEXT"),
            ("Observations", "AppraisalEvidenceJson", "TEXT"),
            ("Observations", "ScreenshotPathsJson", "TEXT"),
            ("Observations", "ScreenshotHashesJson", "TEXT"),
            ("Observations", "ProtectionJson", "TEXT"),
            ("PokemonRecords", "SemanticKey", "TEXT"),
            ("PokemonRecords", "SemanticKeyCompleteness", "TEXT"),
            ("Observations", "SemanticKey", "TEXT"),
            ("Observations", "SemanticKeyCompleteness", "TEXT")
        })
        {
            await EnsureColumnAsync(connection, table, column, declaration, cancellationToken);
        }

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "UPDATE SchemaInfo SET Version = @version, AppliedAtUtc = @now WHERE Version < @version;";
        versionCommand.Parameters.AddWithValue("@version", SchemaVersion);
        versionCommand.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        await versionCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertWorkBucketAsync(
        PersistentWorkBucket bucket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        ValidateBucket(bucket);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WorkBuckets (LogicalBucketId, AbsoluteDateStart, AbsoluteDateEnd, PokedexStart, PokedexEnd, DerivedPhoneQuery, Status, StartedAtUtc, CompletedAtUtc, ItemsObserved, ItemsIndexed, ItemsReview, ItemsDeleteCandidate, Failures, Retries, LastSuccessfulItem, CompletionEvidenceJson)
            VALUES (@id,@start,@end,@dexStart,@dexEnd,@query,@status,@started,@completed,@observed,@indexed,@review,@delete,@failures,@retries,@last,@evidence)
            ON CONFLICT(LogicalBucketId) DO UPDATE SET AbsoluteDateStart=excluded.AbsoluteDateStart, AbsoluteDateEnd=excluded.AbsoluteDateEnd, PokedexStart=excluded.PokedexStart, PokedexEnd=excluded.PokedexEnd, DerivedPhoneQuery=excluded.DerivedPhoneQuery;
            """;
        AddBucket(command, bucket);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PersistentWorkBucket?> LoadOldestUnfinishedWorkBucketAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LogicalBucketId, AbsoluteDateStart, AbsoluteDateEnd, PokedexStart, PokedexEnd, DerivedPhoneQuery, Status, StartedAtUtc, CompletedAtUtc, ItemsObserved, ItemsIndexed, ItemsReview, ItemsDeleteCandidate, Failures, Retries, LastSuccessfulItem, CompletionEvidenceJson FROM WorkBuckets WHERE Status <> 'Complete' ORDER BY AbsoluteDateStart, AbsoluteDateEnd, LogicalBucketId LIMIT 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBucket(reader) : null;
    }

    public async Task SetWorkBucketStatusAsync(
        string logicalBucketId,
        PersistentWorkBucketStatus status,
        string? completionEvidenceJson = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalBucketId);
        if (status == PersistentWorkBucketStatus.Complete && string.IsNullOrWhiteSpace(completionEvidenceJson))
            throw new InvalidOperationException("A completed work bucket requires empty-query and reconciliation evidence.");
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE WorkBuckets SET Status=@status, StartedAtUtc=CASE WHEN @status='Active' AND StartedAtUtc IS NULL THEN @now ELSE StartedAtUtc END, CompletedAtUtc=CASE WHEN @status='Complete' THEN @now ELSE NULL END, CompletionEvidenceJson=CASE WHEN @status='Complete' THEN @evidence ELSE CompletionEvidenceJson END WHERE LogicalBucketId=@id;";
        command.Parameters.AddWithValue("@status", status.ToString()); command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("@evidence", (object?)completionEvidenceJson ?? DBNull.Value); command.Parameters.AddWithValue("@id", logicalBucketId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException($"Unknown work bucket '{logicalBucketId}'.");
    }

    public async Task RecordWorkItemAsync(PersistentWorkItem item, PersistentWorkAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(attempt);
        if (!string.Equals(item.LogicalBucketId, attempt.LogicalBucketId, StringComparison.Ordinal) || !string.Equals(item.LocalPokemonId, attempt.LocalPokemonId, StringComparison.Ordinal)) throw new InvalidOperationException("Work item and attempt must identify the same DB record.");
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO WorkItems (LogicalBucketId,LocalPokemonId,State,Disposition,ExactBindingEvidence,UpdatedAtUtc,LastError) VALUES (@bucket,@item,@state,@disposition,@binding,@updated,@error) ON CONFLICT(LogicalBucketId,LocalPokemonId) DO UPDATE SET State=excluded.State,Disposition=excluded.Disposition,ExactBindingEvidence=excluded.ExactBindingEvidence,UpdatedAtUtc=excluded.UpdatedAtUtc,LastError=excluded.LastError;";
            AddItem(command, item);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO WorkAttempts (LogicalBucketId,LocalPokemonId,AttemptKind,Result,OccurredAtUtc,DetailJson) VALUES (@bucket,@item,@kind,@result,@at,@detail);";
            command.Parameters.AddWithValue("@bucket", attempt.LogicalBucketId); command.Parameters.AddWithValue("@item", attempt.LocalPokemonId); command.Parameters.AddWithValue("@kind", attempt.AttemptKind); command.Parameters.AddWithValue("@result", attempt.Result); command.Parameters.AddWithValue("@at", attempt.OccurredAtUtc.ToString("O")); command.Parameters.AddWithValue("@detail", (object?)attempt.DetailJson ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordSearchOracleEvidenceAsync(
        PersistentSearchOracleEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.Query);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.Outcome);
        if (evidence.EmptyVerified && evidence.ObservedResultCount is not 0)
            throw new InvalidOperationException("An empty oracle proof must have observed result count zero.");
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO SearchOracleEvidence (LogicalBucketId,RunId,Query,Outcome,ExpectedResultCount,ObservedResultCount,EmptyVerified,ObservedAtUtc,EvidencePath,DetailJson) VALUES (@bucket,@run,@query,@outcome,@expected,@observed,@empty,@at,@path,@detail);";
        command.Parameters.AddWithValue("@bucket", (object?)evidence.LogicalBucketId ?? DBNull.Value);
        command.Parameters.AddWithValue("@run", (object?)evidence.RunId ?? DBNull.Value);
        command.Parameters.AddWithValue("@query", evidence.Query);
        command.Parameters.AddWithValue("@outcome", evidence.Outcome);
        command.Parameters.AddWithValue("@expected", (object?)evidence.ExpectedResultCount ?? DBNull.Value);
        command.Parameters.AddWithValue("@observed", (object?)evidence.ObservedResultCount ?? DBNull.Value);
        command.Parameters.AddWithValue("@empty", evidence.EmptyVerified ? 1 : 0);
        command.Parameters.AddWithValue("@at", evidence.ObservedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@path", (object?)evidence.EvidencePath ?? DBNull.Value);
        command.Parameters.AddWithValue("@detail", (object?)evidence.DetailJson ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> FindOtherPokemonIdsByFingerprintAsync(
        string fingerprintSha256,
        string localPokemonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprintSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LocalPokemonId FROM PokemonRecords WHERE LastFingerprintSha256=@fingerprint AND LocalPokemonId<>@id ORDER BY LocalPokemonId;";
        command.Parameters.AddWithValue("@fingerprint", fingerprintSha256);
        command.Parameters.AddWithValue("@id", localPokemonId);
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task UpdateWorkBucketProgressAsync(
        string logicalBucketId, int observed, int review, string? lastSuccessfulItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalBucketId);
        if (observed < 0 || review < 0) throw new ArgumentOutOfRangeException(nameof(observed));
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE WorkBuckets SET ItemsObserved=ItemsObserved+@observed, ItemsIndexed=ItemsIndexed+@observed, ItemsReview=ItemsReview+@review, LastSuccessfulItem=COALESCE(@last,LastSuccessfulItem) WHERE LogicalBucketId=@id;";
        command.Parameters.AddWithValue("@observed", observed);
        command.Parameters.AddWithValue("@review", review);
        command.Parameters.AddWithValue("@last", (object?)lastSuccessfulItem ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", logicalBucketId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException($"Unknown work bucket '{logicalBucketId}'.");
    }

    public async Task ImportAsync(string runId, InventoryScanItem item, string screenshotPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO Observations " +
            "(LocalPokemonId, RunId, Sequence, CapturedAtUtc, ProviderName, " +
            "ObservationStatus, Confidence, SpeciesName, Cp, AttackIv, DefenseIv, " +
            "HpIv, CatchLocation, ScreenshotPath, ScreenshotSha256, FingerprintSha256) " +
            "VALUES (@id, @run, @seq, @at, @provider, @status, @confidence, " +
            "@species, @cp, @atk, @def, @hp, @location, @path, @sha, @fingerprint);";
        command.Parameters.AddWithValue("@id", runId + ":" + item.SequenceNumber);
        command.Parameters.AddWithValue("@run", runId);
        command.Parameters.AddWithValue("@seq", item.SequenceNumber);
        command.Parameters.AddWithValue("@at", item.CapturedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@provider", item.Observation.ProviderName);
        command.Parameters.AddWithValue("@status", item.Observation.Status.ToString());
        command.Parameters.AddWithValue("@confidence", item.Observation.Confidence);
        command.Parameters.AddWithValue("@species", (object?)item.Observation.Species ?? DBNull.Value);
        command.Parameters.AddWithValue("@cp", (object?)item.Observation.Cp ?? DBNull.Value);
        command.Parameters.AddWithValue("@atk", (object?)item.Observation.AttackIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@def", (object?)item.Observation.DefenseIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@hp", (object?)item.Observation.HpIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@location", (object?)item.Observation.CatchLocation ?? DBNull.Value);
        command.Parameters.AddWithValue("@path", screenshotPath);
        command.Parameters.AddWithValue("@sha", item.ScreenshotSha256);
        command.Parameters.AddWithValue("@fingerprint", item.IdentityFingerprintSha256);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> CountObservationsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Observations";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    public async Task StartCleanupRunAsync(
        CleanupProofRunStart run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScanRuns
                (RunId, RunType, SearchQuery, StartedAtUtc, Status, DeviceSerial,
                 RequestedItems, ActualItems, SourceDirectory)
            VALUES (@run, 'CleanupProof', @query, @started, 'Running', @serial,
                    @requested, 0, @source);
            """;
        command.Parameters.AddWithValue("@run", run.RunId);
        command.Parameters.AddWithValue("@query", run.SearchQuery);
        command.Parameters.AddWithValue("@started", run.StartedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@serial", run.DeviceSerial);
        command.Parameters.AddWithValue("@requested", run.RequestedItems);
        command.Parameters.AddWithValue("@source", run.SourceDirectory);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordCleanupObservationAsync(
        CleanupProofObservationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var jsonOptions = JsonOptions();
        var observationJson = JsonSerializer.Serialize(record.Observation, jsonOptions);
        var protectionJson = JsonSerializer.Serialize(record.Observation.Protection, jsonOptions);
        var fieldEvidenceJson = JsonSerializer.Serialize(record.FieldEvidenceSources, jsonOptions);
        var appraisalEvidenceJson = JsonSerializer.Serialize(record.AppraisalEvidence, jsonOptions);
        var screenshotPathsJson = JsonSerializer.Serialize(record.ScreenshotPaths, jsonOptions);
        var screenshotHashesJson = JsonSerializer.Serialize(record.ScreenshotHashes, jsonOptions);
        var variantJson = JsonSerializer.Serialize(record.Observation.VariantIdentity, jsonOptions);
        var semanticKey = SemanticIdentityKey.FromObservation(record.Observation);

        await using (var observation = connection.CreateCommand())
        {
            observation.Transaction = transaction;
            observation.CommandText = """
                INSERT INTO Observations
                    (LocalPokemonId, RunId, Sequence, CapturedAtUtc, ProviderName,
                     ObservationStatus, Confidence, ProtectionConfidence, SpeciesName,
                     Cp, AttackIv, DefenseIv, HpIv, CatchLocation, ScreenshotPath,
                     ScreenshotSha256, FingerprintSha256, ObservationJson,
                     FieldEvidenceJson, AppraisalEvidenceJson, ScreenshotPathsJson,
                     ScreenshotHashesJson, ProtectionJson, SemanticKey, SemanticKeyCompleteness)
                VALUES (@id, @run, @seq, @captured, 'CleanupProof', @status, @identity,
                        @protection, @species, @cp, @attack, @defense, @hp, @location,
                        @path, @sha, @fingerprint, @observation, @fields, @appraisal,
                        @paths, @hashes, @protectionJson, @semanticKey, @semanticKeyCompleteness);
                """;
            observation.Parameters.AddWithValue("@id", record.LocalPokemonId);
            observation.Parameters.AddWithValue("@run", record.RunId);
            observation.Parameters.AddWithValue("@seq", record.Ordinal);
            observation.Parameters.AddWithValue("@captured", record.CapturedAtUtc.ToString("O"));
            observation.Parameters.AddWithValue("@status", record.ObservationStatus);
            observation.Parameters.AddWithValue("@identity", record.IdentityConfidenceValue);
            observation.Parameters.AddWithValue("@protection", record.ProtectionConfidenceValue);
            observation.Parameters.AddWithValue("@species", (object?)record.Observation.Species ?? DBNull.Value);
            observation.Parameters.AddWithValue("@cp", (object?)record.Observation.Cp ?? DBNull.Value);
            observation.Parameters.AddWithValue("@attack", (object?)record.Observation.AttackIv ?? DBNull.Value);
            observation.Parameters.AddWithValue("@defense", (object?)record.Observation.DefenseIv ?? DBNull.Value);
            observation.Parameters.AddWithValue("@hp", (object?)record.Observation.HpIv ?? DBNull.Value);
            observation.Parameters.AddWithValue("@location", (object?)record.Observation.CatchLocation ?? DBNull.Value);
            observation.Parameters.AddWithValue("@path", record.ScreenshotPaths.FirstOrDefault() ?? string.Empty);
            observation.Parameters.AddWithValue("@sha", record.ScreenshotHashes.FirstOrDefault() ?? string.Empty);
            observation.Parameters.AddWithValue("@fingerprint", record.StableFingerprint);
            observation.Parameters.AddWithValue("@observation", observationJson);
            observation.Parameters.AddWithValue("@fields", fieldEvidenceJson);
            observation.Parameters.AddWithValue("@appraisal", appraisalEvidenceJson);
            observation.Parameters.AddWithValue("@paths", screenshotPathsJson);
            observation.Parameters.AddWithValue("@hashes", screenshotHashesJson);
            observation.Parameters.AddWithValue("@protectionJson", protectionJson);
            observation.Parameters.AddWithValue("@semanticKey", semanticKey.FullKey);
            observation.Parameters.AddWithValue("@semanticKeyCompleteness", semanticKey.Completeness.ToString());
            await observation.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var recordCommand = connection.CreateCommand())
        {
            recordCommand.Transaction = transaction;
            recordCommand.CommandText = """
                INSERT INTO PokemonRecords
                    (LocalPokemonId, LifecycleState, FirstSeenRunId, LastSeenRunId,
                     FirstSeenAtUtc, LastSeenAtUtc, SpeciesName, Cp, AttackIv, DefenseIv,
                     HpIv, FormId, CostumeId, BackgroundId, IsShiny, ShadowState,
                     LuckyState, DynamaxState, CatchLocation, IdentityConfidence,
                     ProtectionConfidence, CurrentRecommendation, RecommendationReason,
                     LastScreenshotPath, LastScreenshotSha256, LastFingerprintSha256,
                     ObservationStatus, Nickname, ExistingTagsJson, FieldEvidenceJson,
                     AppraisalEvidenceJson, VariantJson, ProtectionJson, ComparatorLocalPokemonId,
                     SemanticKey, SemanticKeyCompleteness)
                VALUES (@id, 'Observed', @run, @run, @at, @at, @species, @cp, @attack,
                        @defense, @hp, @form, @costume, @background, @shiny, @shadow,
                        @lucky, @dynamax, @location, @identity, @protection, 'PENDING',
                        'Recommendation has not been generated.', @path, @sha, @fingerprint,
                        @status, @nickname, @tags, @fields, @appraisal, @variant, @protectionJson, NULL,
                        @semanticKey, @semanticKeyCompleteness);
                """;
            recordCommand.Parameters.AddWithValue("@id", record.LocalPokemonId);
            recordCommand.Parameters.AddWithValue("@run", record.RunId);
            recordCommand.Parameters.AddWithValue("@at", record.CapturedAtUtc.ToString("O"));
            recordCommand.Parameters.AddWithValue("@species", (object?)record.Observation.Species ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@cp", (object?)record.Observation.Cp ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@attack", (object?)record.Observation.AttackIv ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@defense", (object?)record.Observation.DefenseIv ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@hp", (object?)record.Observation.HpIv ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@form", (object?)record.Observation.Form ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@costume", (object?)record.Observation.Costume ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@background", record.Observation.IsBackground is true ? "background" : DBNull.Value);
            recordCommand.Parameters.AddWithValue("@shiny", BoolValue(record.Observation.IsShiny));
            recordCommand.Parameters.AddWithValue("@shadow", StateValue(record.Observation.IsShadow, "shadow"));
            recordCommand.Parameters.AddWithValue("@lucky", StateValue(record.Observation.IsLucky, "lucky"));
            recordCommand.Parameters.AddWithValue("@dynamax", StateValue(record.Observation.IsDynamax, "dynamax"));
            recordCommand.Parameters.AddWithValue("@location", (object?)record.Observation.CatchLocation ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@identity", record.IdentityConfidenceValue.ToString(CultureInfo.InvariantCulture));
            recordCommand.Parameters.AddWithValue("@protection", record.ProtectionConfidenceValue.ToString(CultureInfo.InvariantCulture));
            recordCommand.Parameters.AddWithValue("@path", record.ScreenshotPaths.FirstOrDefault() ?? string.Empty);
            recordCommand.Parameters.AddWithValue("@sha", record.ScreenshotHashes.FirstOrDefault() ?? string.Empty);
            recordCommand.Parameters.AddWithValue("@fingerprint", record.StableFingerprint);
            recordCommand.Parameters.AddWithValue("@status", record.ObservationStatus);
            recordCommand.Parameters.AddWithValue("@nickname", (object?)record.Observation.Nickname ?? DBNull.Value);
            recordCommand.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(record.Observation.Tags, jsonOptions));
            recordCommand.Parameters.AddWithValue("@fields", fieldEvidenceJson);
            recordCommand.Parameters.AddWithValue("@appraisal", appraisalEvidenceJson);
            recordCommand.Parameters.AddWithValue("@variant", variantJson);
            recordCommand.Parameters.AddWithValue("@protectionJson", protectionJson);
            recordCommand.Parameters.AddWithValue("@semanticKey", semanticKey.FullKey);
            recordCommand.Parameters.AddWithValue("@semanticKeyCompleteness", semanticKey.Completeness.ToString());
            await recordCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = "INSERT INTO InventoryEvents (LocalPokemonId, RunId, EventType, OccurredAtUtc, DetailJson) VALUES (@id, @run, 'Observed', @at, @detail);";
            eventCommand.Parameters.AddWithValue("@id", record.LocalPokemonId);
            eventCommand.Parameters.AddWithValue("@run", record.RunId);
            eventCommand.Parameters.AddWithValue("@at", record.CapturedAtUtc.ToString("O"));
            eventCommand.Parameters.AddWithValue("@detail", JsonSerializer.Serialize(new
            {
                record.Ordinal,
                record.ObservationStatus,
                record.StableFingerprint,
                record.ScreenshotPaths,
                record.ScreenshotHashes,
                record.AppraisalEvidence,
                record.FieldEvidenceSources
            }, jsonOptions));
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteCleanupRunAsync(
        string runId,
        int actualItems,
        string status,
        string stopReason,
        DateTimeOffset endedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScanRuns SET ActualItems = @actual, EndedAtUtc = @ended, Status = @status, StopReason = @reason WHERE RunId = @run;";
        command.Parameters.AddWithValue("@actual", actualItems);
        command.Parameters.AddWithValue("@ended", endedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@reason", stopReason);
        command.Parameters.AddWithValue("@run", runId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnrichCleanupAppraisalAsync(
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        CleanupProofAppraisalCapture appraisal,
        IReadOnlyDictionary<string, string> fieldEvidenceSources,
        CancellationToken cancellationToken = default,
        string? observationStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(appraisal);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var options = JsonOptions();
        var observationJson = JsonSerializer.Serialize(observation, options);
        var protectionJson = JsonSerializer.Serialize(observation.Protection, options);
        var fieldsJson = JsonSerializer.Serialize(fieldEvidenceSources, options);
        var appraisalJson = JsonSerializer.Serialize(
            appraisal.EvidencePaths.Count == 0
                ? new[] { "AppraisalStatus:" + appraisal.Status }
                : appraisal.EvidencePaths,
            options);
        var appraisalDetailJson = JsonSerializer.Serialize(appraisal, options);
        var statusClause = observationStatus is null ? string.Empty : "ObservationStatus = @status, ";
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
                UPDATE Observations
                SET {statusClause}Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    ObservationJson = @observation, ProtectionJson = @protectionJson, FieldEvidenceJson = @fields,
                    AppraisalEvidenceJson = @appraisal, SemanticKey = @semanticKey,
                    SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddEnrichmentParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson, appraisalJson);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            if (observationStatus is not null) command.Parameters.AddWithValue("@status", observationStatus);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
                UPDATE PokemonRecords
                SET {statusClause}Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    ProtectionJson = @protectionJson, FieldEvidenceJson = @fields, AppraisalEvidenceJson = @appraisal,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddEnrichmentParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson, appraisalJson);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            if (observationStatus is not null) command.Parameters.AddWithValue("@status", observationStatus);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertCleanupEventAsync(connection, transaction, localPokemonId, runId,
            "AppraisalEnriched", appraisalDetailJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task EnrichCleanupSemanticReviewAsync(
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        IReadOnlyDictionary<string, string> fieldEvidenceSources,
        string reviewJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewJson);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var options = JsonOptions();
        var observationJson = JsonSerializer.Serialize(observation, options);
        var protectionJson = JsonSerializer.Serialize(observation.Protection, options);
        var fieldsJson = JsonSerializer.Serialize(fieldEvidenceSources, options);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Observations
                SET Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    CatchLocation = @location, ObservationJson = @observation, ProtectionJson = @protectionJson,
                    FieldEvidenceJson = @fields, SemanticKey = @semanticKey,
                    SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddSemanticParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE PokemonRecords
                SET Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    CatchLocation = @location, ProtectionJson = @protectionJson, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddSemanticParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertCleanupEventAsync(connection, transaction, localPokemonId, runId,
            "SemanticReviewEnriched", reviewJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void AddEnrichmentParameters(
        SqliteCommand command,
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        string observationJson,
        string fieldsJson,
        string appraisalJson)
    {
        command.Parameters.AddWithValue("@run", runId);
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@cp", (object?)observation.Cp ?? DBNull.Value);
        command.Parameters.AddWithValue("@attack", (object?)observation.AttackIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@defense", (object?)observation.DefenseIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@hp", (object?)observation.HpIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@observation", observationJson);
        command.Parameters.AddWithValue("@fields", fieldsJson);
        command.Parameters.AddWithValue("@appraisal", appraisalJson);
        var semanticKey = SemanticIdentityKey.FromObservation(observation);
        command.Parameters.AddWithValue("@semanticKey", semanticKey.FullKey);
        command.Parameters.AddWithValue("@semanticKeyCompleteness", semanticKey.Completeness.ToString());
    }

    private static void AddSemanticParameters(
        SqliteCommand command,
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        string observationJson,
        string fieldsJson)
    {
        command.Parameters.AddWithValue("@run", runId);
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@cp", (object?)observation.Cp ?? DBNull.Value);
        command.Parameters.AddWithValue("@attack", (object?)observation.AttackIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@defense", (object?)observation.DefenseIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@hp", (object?)observation.HpIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@location", (object?)observation.CatchLocation ?? DBNull.Value);
        command.Parameters.AddWithValue("@observation", observationJson);
        command.Parameters.AddWithValue("@fields", fieldsJson);
        var semanticKey = SemanticIdentityKey.FromObservation(observation);
        command.Parameters.AddWithValue("@semanticKey", semanticKey.FullKey);
        command.Parameters.AddWithValue("@semanticKeyCompleteness", semanticKey.Completeness.ToString());
    }

    private static async Task InsertCleanupEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string localPokemonId,
        string runId,
        string eventType,
        string detailJson,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO InventoryEvents (LocalPokemonId, RunId, EventType, OccurredAtUtc, DetailJson) VALUES (@id, @run, @event, @at, @detail);";
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@run", runId);
        command.Parameters.AddWithValue("@event", eventType);
        command.Parameters.AddWithValue("@at", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@detail", detailJson);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CleanupProofDatabaseRow>> LoadCleanupProofRowsAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.RunId, o.Sequence, o.LocalPokemonId, o.CapturedAtUtc,
                   o.ObservationStatus, o.Confidence, o.ProtectionConfidence,
                   o.FingerprintSha256, o.ObservationJson, o.FieldEvidenceJson,
                   o.AppraisalEvidenceJson, o.ScreenshotPathsJson, o.ScreenshotHashesJson,
                   p.CurrentRecommendation, p.RecommendationReason, p.ComparatorLocalPokemonId,
                   o.SemanticKey, o.SemanticKeyCompleteness
            FROM Observations o
            JOIN PokemonRecords p ON p.LocalPokemonId = o.LocalPokemonId
            WHERE o.RunId = @run
            ORDER BY o.Sequence;
            """;
        command.Parameters.AddWithValue("@run", runId);
        var rows = new List<CleanupProofDatabaseRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var options = JsonOptions();
            var observation = JsonSerializer.Deserialize<PokemonObservation>(reader.GetString(8), options)
                ?? throw new InvalidOperationException("Cleanup proof observation JSON was empty.");
            rows.Add(new CleanupProofDatabaseRow
            {
                RunId = reader.GetString(0),
                Ordinal = reader.GetInt32(1),
                LocalPokemonId = reader.GetString(2),
                CapturedAtUtc = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Observation = observation,
                ObservationStatus = reader.GetString(4),
                IdentityConfidenceValue = reader.GetDouble(5),
                ProtectionConfidenceValue = reader.GetDouble(6),
                StableFingerprint = reader.GetString(7),
                ScreenshotPaths = DeserializeStringArray(reader.IsDBNull(11) ? null : reader.GetString(11), options),
                ScreenshotHashes = DeserializeStringArray(reader.IsDBNull(12) ? null : reader.GetString(12), options),
                AppraisalEvidence = DeserializeStringArray(reader.IsDBNull(10) ? null : reader.GetString(10), options),
                FieldEvidenceSources = DeserializeDictionary(reader.IsDBNull(9) ? null : reader.GetString(9), options),
                CurrentRecommendation = reader.GetString(13),
                RecommendationReason = reader.GetString(14),
                ComparatorLocalPokemonId = reader.IsDBNull(15) ? null : reader.GetString(15),
                SemanticKey = reader.IsDBNull(16) ? null : reader.GetString(16),
                SemanticKeyCompleteness = reader.IsDBNull(17) ? null : reader.GetString(17)
            });
        }
        return rows;
    }

    /// <summary>
    /// Loads a lightweight, run-independent view of every PokemonRecord in this
    /// database. Used for offline cross-run re-identification: no observation
    /// evidence blobs are loaded, only the fields needed to compare semantic
    /// identity keys.
    /// </summary>
    public async Task<IReadOnlyList<PokemonRecordSemanticRow>> LoadAllPokemonRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT LocalPokemonId, SpeciesName, Cp, AttackIv, DefenseIv, HpIv,
                   SemanticKey, SemanticKeyCompleteness, FirstSeenRunId, LastSeenRunId
            FROM PokemonRecords
            ORDER BY LocalPokemonId;
            """;
        var rows = new List<PokemonRecordSemanticRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PokemonRecordSemanticRow
            {
                LocalPokemonId = reader.GetString(0),
                SpeciesName = reader.IsDBNull(1) ? null : reader.GetString(1),
                Cp = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                AttackIv = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DefenseIv = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                HpIv = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                SemanticKey = reader.IsDBNull(6) ? null : reader.GetString(6),
                SemanticKeyCompleteness = reader.IsDBNull(7) ? null : reader.GetString(7),
                FirstSeenRunId = reader.GetString(8),
                LastSeenRunId = reader.GetString(9)
            });
        }
        return rows;
    }

    public async Task UpdateRecommendationAsync(
        string runId,
        PokemonDecision decision,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var label = decision.Category switch
        {
            DecisionCategory.Keep => "KEEP",
            DecisionCategory.Review => "REVIEW",
            DecisionCategory.Delete => "DELETE-CANDIDATE",
            _ => decision.Category.ToString().ToUpperInvariant()
        };
        var reason = string.Join(" ", decision.Reasons.Select(item => $"[{item.Code}] {item.Message}"));
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE PokemonRecords SET CurrentRecommendation = @recommendation, RecommendationReason = @reason, ComparatorLocalPokemonId = @comparator WHERE LocalPokemonId = @id AND LastSeenRunId = @run;";
            command.Parameters.AddWithValue("@recommendation", label);
            command.Parameters.AddWithValue("@reason", reason);
            command.Parameters.AddWithValue("@id", decision.ExternalKey);
            command.Parameters.AddWithValue("@run", runId);
            command.Parameters.AddWithValue("@comparator", (object?)decision.BetterDuplicateExternalKey ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = "INSERT INTO InventoryEvents (LocalPokemonId, RunId, EventType, OccurredAtUtc, DetailJson) VALUES (@id, @run, 'RecommendationGenerated', @at, @detail);";
            eventCommand.Parameters.AddWithValue("@id", decision.ExternalKey);
            eventCommand.Parameters.AddWithValue("@run", runId);
            eventCommand.Parameters.AddWithValue("@at", DateTimeOffset.UtcNow.ToString("O"));
            eventCommand.Parameters.AddWithValue("@detail", JsonSerializer.Serialize(new
            {
                Recommendation = label,
                decision.Reasons,
                decision.BetterDuplicateExternalKey
            }, JsonOptions()));
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Lists every CleanupProof run id present in this database, ordered for
    /// deterministic reprocessing. Used by the offline
    /// <c>analyze-cleanup-evidence</c> reprocess command, which does not know
    /// in advance how many runs an existing database contains.
    /// </summary>
    public async Task<IReadOnlyList<string>> LoadAllCleanupRunIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RunId FROM ScanRuns WHERE RunType = 'CleanupProof' ORDER BY RunId;";
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetString(0));
        return ids;
    }

    /// <summary>Reads the original <c>--species</c> search query recorded for a run.</summary>
    public async Task<string> ReadCleanupRunSearchQueryAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SearchQuery FROM ScanRuns WHERE RunId = @run;";
        command.Parameters.AddWithValue("@run", runId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? string.Empty : (string)value;
    }

    /// <summary>
    /// Transactionally overwrites Species/Cp/IVs/Nickname/FieldEvidence and the
    /// semantic identity key for one row's Observation and PokemonRecord, used
    /// by the offline <c>analyze-cleanup-evidence</c> reprocess command after
    /// re-running header OCR / IV consensus against stored evidence. Unlike
    /// <see cref="EnrichCleanupSemanticReviewAsync"/> this also rewrites the
    /// <c>SpeciesName</c> and <c>Nickname</c> columns, since reprocessing can
    /// change species from the original (possibly incorrect) raw-query value.
    /// </summary>
    public async Task ReprocessCleanupSemanticsAsync(
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        IReadOnlyDictionary<string, string> fieldEvidenceSources,
        string observationStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationStatus);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var options = JsonOptions();
        var observationJson = JsonSerializer.Serialize(observation, options);
        var protectionJson = JsonSerializer.Serialize(observation.Protection, options);
        var fieldsJson = JsonSerializer.Serialize(fieldEvidenceSources, options);
        var semanticKey = SemanticIdentityKey.FromObservation(observation);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Observations
                SET SpeciesName = @species, Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    ObservationStatus = @status, ObservationJson = @observation, ProtectionJson = @protectionJson, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddReprocessParameters(command, runId, localPokemonId, observation, observationJson, fieldsJson, semanticKey, observationStatus);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE PokemonRecords
                SET SpeciesName = @species, Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    Nickname = @nickname, ObservationStatus = @status, ProtectionJson = @protectionJson, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddReprocessParameters(command, runId, localPokemonId, observation, observationJson, fieldsJson, semanticKey, observationStatus);
            command.Parameters.AddWithValue("@protectionJson", protectionJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertCleanupEventAsync(connection, transaction, localPokemonId, runId,
            "SemanticReprocessed", fieldsJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void AddReprocessParameters(
        SqliteCommand command,
        string runId,
        string localPokemonId,
        PokemonObservation observation,
        string observationJson,
        string fieldsJson,
        SemanticIdentityKey semanticKey,
        string observationStatus)
    {
        command.Parameters.AddWithValue("@run", runId);
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@species", (object?)observation.Species ?? DBNull.Value);
        command.Parameters.AddWithValue("@nickname", (object?)observation.Nickname ?? DBNull.Value);
        command.Parameters.AddWithValue("@cp", (object?)observation.Cp ?? DBNull.Value);
        command.Parameters.AddWithValue("@attack", (object?)observation.AttackIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@defense", (object?)observation.DefenseIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@hp", (object?)observation.HpIv ?? DBNull.Value);
        command.Parameters.AddWithValue("@observation", observationJson);
        command.Parameters.AddWithValue("@fields", fieldsJson);
        command.Parameters.AddWithValue("@semanticKey", semanticKey.FullKey);
        command.Parameters.AddWithValue("@semanticKeyCompleteness", semanticKey.Completeness.ToString());
        command.Parameters.AddWithValue("@status", observationStatus);
    }

    public async Task<CleanupProofSqlSummary> ReadCleanupProofSqlSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        var integrity = await ScalarStringAsync(connection, "PRAGMA integrity_check;", cancellationToken);
        var scans = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM ScanRuns;", cancellationToken);
        var observations = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM Observations;", cancellationToken);
        var records = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM PokemonRecords;", cancellationToken);
        var events = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM InventoryEvents;", cancellationToken);
        var recommendations = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CurrentRecommendation, COUNT(*) FROM PokemonRecords GROUP BY CurrentRecommendation;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            recommendations[reader.GetString(0)] = reader.GetInt64(1);
        return new CleanupProofSqlSummary
        {
            IntegrityCheck = integrity,
            ScanRunCount = scans,
            ObservationCount = observations,
            PokemonRecordCount = records,
            InventoryEventCount = events,
            RecommendationCounts = recommendations
        };
    }

    private static void ValidateBucket(PersistentWorkBucket bucket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket.LogicalBucketId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket.DerivedPhoneQuery);
        if (bucket.AbsoluteDateEnd < bucket.AbsoluteDateStart) throw new InvalidOperationException("Work bucket end date precedes its start date.");
        if (bucket.PokedexStart is <= 0 || bucket.PokedexEnd is <= 0 || bucket.PokedexStart > bucket.PokedexEnd) throw new InvalidOperationException("Work bucket Pokédex bounds are invalid.");
    }

    private static void AddBucket(SqliteCommand command, PersistentWorkBucket bucket)
    {
        command.Parameters.AddWithValue("@id", bucket.LogicalBucketId); command.Parameters.AddWithValue("@start", bucket.AbsoluteDateStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("@end", bucket.AbsoluteDateEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("@dexStart", (object?)bucket.PokedexStart ?? DBNull.Value); command.Parameters.AddWithValue("@dexEnd", (object?)bucket.PokedexEnd ?? DBNull.Value); command.Parameters.AddWithValue("@query", bucket.DerivedPhoneQuery); command.Parameters.AddWithValue("@status", bucket.Status.ToString()); command.Parameters.AddWithValue("@started", bucket.StartedAtUtc?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("@completed", bucket.CompletedAtUtc?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("@observed", bucket.ItemsObserved); command.Parameters.AddWithValue("@indexed", bucket.ItemsIndexed); command.Parameters.AddWithValue("@review", bucket.ItemsReview); command.Parameters.AddWithValue("@delete", bucket.ItemsDeleteCandidate); command.Parameters.AddWithValue("@failures", bucket.Failures); command.Parameters.AddWithValue("@retries", bucket.Retries); command.Parameters.AddWithValue("@last", (object?)bucket.LastSuccessfulItem ?? DBNull.Value); command.Parameters.AddWithValue("@evidence", (object?)bucket.CompletionEvidenceJson ?? DBNull.Value);
    }

    private static PersistentWorkBucket ReadBucket(SqliteDataReader row) => new()
    {
        LogicalBucketId = row.GetString(0), AbsoluteDateStart = DateOnly.Parse(row.GetString(1), CultureInfo.InvariantCulture), AbsoluteDateEnd = DateOnly.Parse(row.GetString(2), CultureInfo.InvariantCulture), PokedexStart = row.IsDBNull(3) ? null : row.GetInt32(3), PokedexEnd = row.IsDBNull(4) ? null : row.GetInt32(4), DerivedPhoneQuery = row.GetString(5), Status = Enum.Parse<PersistentWorkBucketStatus>(row.GetString(6), true), StartedAtUtc = row.IsDBNull(7) ? null : DateTimeOffset.Parse(row.GetString(7), CultureInfo.InvariantCulture), CompletedAtUtc = row.IsDBNull(8) ? null : DateTimeOffset.Parse(row.GetString(8), CultureInfo.InvariantCulture), ItemsObserved = row.GetInt32(9), ItemsIndexed = row.GetInt32(10), ItemsReview = row.GetInt32(11), ItemsDeleteCandidate = row.GetInt32(12), Failures = row.GetInt32(13), Retries = row.GetInt32(14), LastSuccessfulItem = row.IsDBNull(15) ? null : row.GetString(15), CompletionEvidenceJson = row.IsDBNull(16) ? null : row.GetString(16)
    };

    private static void AddItem(SqliteCommand command, PersistentWorkItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item.LogicalBucketId); ArgumentException.ThrowIfNullOrWhiteSpace(item.LocalPokemonId); ArgumentException.ThrowIfNullOrWhiteSpace(item.Disposition);
        command.Parameters.AddWithValue("@bucket", item.LogicalBucketId); command.Parameters.AddWithValue("@item", item.LocalPokemonId); command.Parameters.AddWithValue("@state", item.State.ToString()); command.Parameters.AddWithValue("@disposition", item.Disposition); command.Parameters.AddWithValue("@binding", (object?)item.ExactBindingEvidence ?? DBNull.Value); command.Parameters.AddWithValue("@updated", item.UpdatedAtUtc.ToString("O")); command.Parameters.AddWithValue("@error", (object?)item.LastError ?? DBNull.Value);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static IReadOnlyList<string> DeserializeStringArray(string? json, JsonSerializerOptions options) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(json, options) ?? Array.Empty<string>();

    private static IReadOnlyDictionary<string, string> DeserializeDictionary(string? json, JsonSerializerOptions options) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json, options)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private static object BoolValue(bool? value) => value is null ? DBNull.Value : value.Value ? 1 : 0;

    private static object StateValue(bool? value, string state) => value is true ? state : DBNull.Value;

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string declaration,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        var exists = false;
        await using (var reader = await check.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                exists |= string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase);
        }
        if (exists) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private SqliteConnection Open() => new($"Data Source={_databasePath}");
}
