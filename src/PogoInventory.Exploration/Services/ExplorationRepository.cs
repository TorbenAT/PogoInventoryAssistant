using System.Text.Json;
using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

public sealed class ExplorationRepository
{
    private readonly string _root;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ExplorationRepository(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    public async Task SaveSessionAsync(ExplorationSession session, CancellationToken cancellationToken = default) =>
        await SaveAsync("sessions", session.SessionId + ".json", session, cancellationToken);

    public async Task SaveObservationAsync(ScreenObservation observation, CancellationToken cancellationToken = default) =>
        await SaveAsync("observations", observation.ObservationId + ".json", observation, cancellationToken);

    public async Task SaveAttemptAsync(string sessionId, ActionAttempt attempt, CancellationToken cancellationToken = default) =>
        await SaveAsync("attempts", $"{sessionId}-{attempt.Sequence:D6}.json", attempt, cancellationToken);

    public async Task SaveTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
    {
        var safeName = string.Join('-', transition.BeforeObservation, transition.ActionName, transition.AfterObservation)
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');
        await SaveAsync("transition-graph", safeName + ".json", transition, cancellationToken);
    }

    public async Task SaveCoverageAsync(string sessionId, ExplorationCoverage coverage, CancellationToken cancellationToken = default) =>
        await SaveAsync("reports", sessionId + "-coverage.json", coverage, cancellationToken);

    private async Task SaveAsync<T>(string folder, string fileName, T value, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, _json), cancellationToken);
        File.Move(temporary, path, overwrite: false);
    }
}
