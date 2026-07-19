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
        string tagName,
        bool verified,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPokemonId);
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
            INSERT INTO TagAssignments (LocalPokemonId, TagName, RequestedState, VerifiedState, RequestedAtUtc, VerifiedAtUtc, LastError)
            VALUES (@id, @tag, 'Requested', @verified, @requested, @verifiedAt, @error)
            ON CONFLICT(LocalPokemonId, TagName) DO UPDATE SET
                RequestedState = excluded.RequestedState,
                VerifiedState = excluded.VerifiedState,
                RequestedAtUtc = excluded.RequestedAtUtc,
                VerifiedAtUtc = excluded.VerifiedAtUtc,
                LastError = excluded.LastError;
            """;
        var now = DateTimeOffset.UtcNow.ToString("O");
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@tag", tagName);
        command.Parameters.AddWithValue("@verified", verified ? "Verified" : "Failed");
        command.Parameters.AddWithValue("@requested", now);
        command.Parameters.AddWithValue("@verifiedAt", verified ? now : DBNull.Value);
        command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
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
        command.CommandText = "SELECT VerifiedState = 'Verified' FROM TagAssignments WHERE LocalPokemonId = @id AND TagName = @tag";
        command.Parameters.AddWithValue("@id", localPokemonId);
        command.Parameters.AddWithValue("@tag", tagName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && Convert.ToInt32(value) == 1;
    }
}
