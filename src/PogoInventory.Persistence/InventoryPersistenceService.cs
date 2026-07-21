using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using PogoInventory.Automation.Models;
using PogoInventory.Core.Models;

namespace PogoInventory.Persistence;

public sealed class InventoryPersistenceService
{
    private const int SchemaVersion = 3;
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
            CREATE TABLE IF NOT EXISTS PokemonRecords (LocalPokemonId TEXT PRIMARY KEY, LifecycleState TEXT NOT NULL, FirstSeenRunId TEXT NOT NULL, LastSeenRunId TEXT NOT NULL, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, FormId TEXT, CostumeId TEXT, BackgroundId TEXT, IsShiny INTEGER, ShadowState TEXT, LuckyState TEXT, DynamaxState TEXT, CatchLocation TEXT, IdentityConfidence TEXT NOT NULL, ProtectionConfidence TEXT NOT NULL, CurrentRecommendation TEXT NOT NULL, RecommendationReason TEXT NOT NULL, LastScreenshotPath TEXT, LastScreenshotSha256 TEXT, LastFingerprintSha256 TEXT, ObservationStatus TEXT NOT NULL DEFAULT 'Observed', Nickname TEXT, ExistingTagsJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, VariantJson TEXT);
            CREATE TABLE IF NOT EXISTS Observations (ObservationId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, Sequence INTEGER NOT NULL, CapturedAtUtc TEXT NOT NULL, ProviderName TEXT NOT NULL, ObservationStatus TEXT NOT NULL, Confidence REAL NOT NULL, ProtectionConfidence REAL NOT NULL DEFAULT 0, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, CatchLocation TEXT, ScreenshotPath TEXT, ScreenshotSha256 TEXT, FingerprintSha256 TEXT, ObservationJson TEXT, FieldEvidenceJson TEXT, AppraisalEvidenceJson TEXT, ScreenshotPathsJson TEXT, ScreenshotHashesJson TEXT, UNIQUE(RunId, Sequence));
            CREATE TABLE IF NOT EXISTS InventoryEvents (EventId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, EventType TEXT NOT NULL, OccurredAtUtc TEXT NOT NULL, DetailJson TEXT);
            CREATE TABLE IF NOT EXISTS TagAssignments (LocalPokemonId TEXT NOT NULL, TagName TEXT NOT NULL, RequestedState TEXT NOT NULL, VerifiedState TEXT NOT NULL, RequestedAtUtc TEXT NOT NULL, VerifiedAtUtc TEXT, LastError TEXT, ActionExecuted INTEGER NOT NULL DEFAULT 0, VisuallyVerified INTEGER NOT NULL DEFAULT 0, BeforeScreenshotHash TEXT, AfterScreenshotHash TEXT, AuditReference TEXT, PRIMARY KEY(LocalPokemonId, TagName));
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
            ("Observations", "ProtectionConfidence", "REAL NOT NULL DEFAULT 0"),
            ("Observations", "ObservationJson", "TEXT"),
            ("Observations", "FieldEvidenceJson", "TEXT"),
            ("Observations", "AppraisalEvidenceJson", "TEXT"),
            ("Observations", "ScreenshotPathsJson", "TEXT"),
            ("Observations", "ScreenshotHashesJson", "TEXT"),
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
                     ScreenshotHashesJson, SemanticKey, SemanticKeyCompleteness)
                VALUES (@id, @run, @seq, @captured, 'CleanupProof', @status, @identity,
                        @protection, @species, @cp, @attack, @defense, @hp, @location,
                        @path, @sha, @fingerprint, @observation, @fields, @appraisal,
                        @paths, @hashes, @semanticKey, @semanticKeyCompleteness);
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
                     AppraisalEvidenceJson, VariantJson, ComparatorLocalPokemonId,
                     SemanticKey, SemanticKeyCompleteness)
                VALUES (@id, 'Observed', @run, @run, @at, @at, @species, @cp, @attack,
                        @defense, @hp, @form, @costume, @background, @shiny, @shadow,
                        @lucky, @dynamax, @location, @identity, @protection, 'PENDING',
                        'Recommendation has not been generated.', @path, @sha, @fingerprint,
                        @status, @nickname, @tags, @fields, @appraisal, @variant, NULL,
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
                    ObservationJson = @observation, FieldEvidenceJson = @fields,
                    AppraisalEvidenceJson = @appraisal, SemanticKey = @semanticKey,
                    SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddEnrichmentParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson, appraisalJson);
            if (observationStatus is not null) command.Parameters.AddWithValue("@status", observationStatus);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
                UPDATE PokemonRecords
                SET {statusClause}Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    FieldEvidenceJson = @fields, AppraisalEvidenceJson = @appraisal,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddEnrichmentParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson, appraisalJson);
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
        var fieldsJson = JsonSerializer.Serialize(fieldEvidenceSources, options);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Observations
                SET Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    CatchLocation = @location, ObservationJson = @observation,
                    FieldEvidenceJson = @fields, SemanticKey = @semanticKey,
                    SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddSemanticParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE PokemonRecords
                SET Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    CatchLocation = @location, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddSemanticParameters(command, runId, localPokemonId, observation,
                observationJson, fieldsJson);
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
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentNullException.ThrowIfNull(observation);
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var options = JsonOptions();
        var observationJson = JsonSerializer.Serialize(observation, options);
        var fieldsJson = JsonSerializer.Serialize(fieldEvidenceSources, options);
        var semanticKey = SemanticIdentityKey.FromObservation(observation);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Observations
                SET SpeciesName = @species, Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    ObservationJson = @observation, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND RunId = @run;
                """;
            AddReprocessParameters(command, runId, localPokemonId, observation, observationJson, fieldsJson, semanticKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE PokemonRecords
                SET SpeciesName = @species, Cp = @cp, AttackIv = @attack, DefenseIv = @defense, HpIv = @hp,
                    Nickname = @nickname, FieldEvidenceJson = @fields,
                    SemanticKey = @semanticKey, SemanticKeyCompleteness = @semanticKeyCompleteness
                WHERE LocalPokemonId = @id AND LastSeenRunId = @run;
                """;
            AddReprocessParameters(command, runId, localPokemonId, observation, observationJson, fieldsJson, semanticKey);
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
        SemanticIdentityKey semanticKey)
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
