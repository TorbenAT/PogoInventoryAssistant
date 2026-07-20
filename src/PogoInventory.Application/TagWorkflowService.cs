using Microsoft.Data.Sqlite;

namespace PogoInventory.Application;

public sealed class TagWorkflowService
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "AI-Indexed",
        "AI-Review"
    };

    private readonly string _databasePath;

    public TagWorkflowService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public async Task RequestAndRecordAsync(
        string localPokemonId,
        TagExecutionResult execution,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentNullException.ThrowIfNull(execution);
        var tagName = execution.TagName;
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!AllowedTags.Contains(tagName))
        {
            throw new InvalidOperationException(
                $"Tag '{tagName}' is not allow-listed. Delete and transfer tags are forbidden.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TagAssignments (LocalPokemonId, TagName, RequestedState, VerifiedState, RequestedAtUtc, VerifiedAtUtc, LastError, ActionExecuted, VisuallyVerified, BeforeScreenshotHash, AfterScreenshotHash, AuditReference)
            VALUES (@id, @tag, 'Requested', @verified, @requested, @verifiedAt, @error, @actionExecuted, @visuallyVerified, @beforeHash, @afterHash, @auditReference)
            ON CONFLICT(LocalPokemonId, TagName) DO UPDATE SET
                RequestedState = excluded.RequestedState,
                VerifiedState = excluded.VerifiedState,
                RequestedAtUtc = excluded.RequestedAtUtc,
                VerifiedAtUtc = excluded.VerifiedAtUtc,
                LastError = excluded.LastError,
                ActionExecuted = excluded.ActionExecuted,
                VisuallyVerified = excluded.VisuallyVerified,
                BeforeScreenshotHash = excluded.BeforeScreenshotHash,
                AfterScreenshotHash = excluded.AfterScreenshotHash,
                AuditReference = excluded.AuditReference;
            """;
        var now = DateTimeOffset.UtcNow.ToString("O");
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@tag", tagName);
        command.Parameters.AddWithValue("@verified", execution.IsCompleteVerification ? "Verified" : "Failed");
        command.Parameters.AddWithValue("@requested", now);
        command.Parameters.AddWithValue("@verifiedAt", execution.IsCompleteVerification ? now : DBNull.Value);
        command.Parameters.AddWithValue("@error", (object?)execution.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("@actionExecuted", execution.ActionExecuted ? 1 : 0);
        command.Parameters.AddWithValue("@visuallyVerified", execution.VisuallyVerified ? 1 : 0);
        command.Parameters.AddWithValue("@beforeHash", (object?)execution.BeforeScreenshotHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@afterHash", (object?)execution.AfterScreenshotHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@auditReference", (object?)execution.AuditReference ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsVerifiedAsync(
        string localPokemonId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!AllowedTags.Contains(tagName))
        {
            throw new InvalidOperationException($"Tag '{tagName}' is not allow-listed.");
        }

        if (!File.Exists(_databasePath))
        {
            return false;
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM TagAssignments
            WHERE LocalPokemonId = @id
              AND TagName = @tag
              AND VerifiedState = 'Verified'
              AND ActionExecuted = 1
              AND VisuallyVerified = 1
              AND NULLIF(TRIM(BeforeScreenshotHash), '') IS NOT NULL
              AND NULLIF(TRIM(AfterScreenshotHash), '') IS NOT NULL
              AND BeforeScreenshotHash <> AfterScreenshotHash
              AND NULLIF(TRIM(AuditReference), '') IS NOT NULL
              AND (LastError IS NULL OR TRIM(LastError) = '')
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@tag", tagName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }
}
