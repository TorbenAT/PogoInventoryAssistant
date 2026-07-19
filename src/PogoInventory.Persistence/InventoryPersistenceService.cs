using Microsoft.Data.Sqlite;
using PogoInventory.Automation.Models;

namespace PogoInventory.Persistence;

public sealed class InventoryPersistenceService
{
    private const int SchemaVersion = 1;
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
            CREATE TABLE IF NOT EXISTS PokemonRecords (LocalPokemonId TEXT PRIMARY KEY, LifecycleState TEXT NOT NULL, FirstSeenRunId TEXT NOT NULL, LastSeenRunId TEXT NOT NULL, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, FormId TEXT, CostumeId TEXT, BackgroundId TEXT, IsShiny INTEGER, ShadowState TEXT, LuckyState TEXT, DynamaxState TEXT, CatchLocation TEXT, IdentityConfidence TEXT NOT NULL, ProtectionConfidence TEXT NOT NULL, CurrentRecommendation TEXT NOT NULL, RecommendationReason TEXT NOT NULL, LastScreenshotPath TEXT, LastScreenshotSha256 TEXT, LastFingerprintSha256 TEXT);
            CREATE TABLE IF NOT EXISTS Observations (ObservationId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, Sequence INTEGER NOT NULL, CapturedAtUtc TEXT NOT NULL, ProviderName TEXT NOT NULL, ObservationStatus TEXT NOT NULL, Confidence REAL NOT NULL, SpeciesName TEXT, Cp INTEGER, AttackIv INTEGER, DefenseIv INTEGER, HpIv INTEGER, CatchLocation TEXT, ScreenshotPath TEXT, ScreenshotSha256 TEXT, FingerprintSha256 TEXT, UNIQUE(RunId, Sequence));
            CREATE TABLE IF NOT EXISTS InventoryEvents (EventId INTEGER PRIMARY KEY AUTOINCREMENT, LocalPokemonId TEXT NOT NULL, RunId TEXT NOT NULL, EventType TEXT NOT NULL, OccurredAtUtc TEXT NOT NULL, DetailJson TEXT);
            CREATE TABLE IF NOT EXISTS TagAssignments (LocalPokemonId TEXT NOT NULL, TagName TEXT NOT NULL, RequestedState TEXT NOT NULL, VerifiedState TEXT NOT NULL, RequestedAtUtc TEXT NOT NULL, VerifiedAtUtc TEXT, LastError TEXT, PRIMARY KEY(LocalPokemonId, TagName));
            INSERT INTO SchemaInfo (Version, AppliedAtUtc) SELECT 1, @now WHERE NOT EXISTS (SELECT 1 FROM SchemaInfo);
            """;
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private SqliteConnection Open() => new($"Data Source={_databasePath}");
}
