using PogoInventory.Automation.Models;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

public sealed class RunCoordinator
{
    private readonly InventoryPersistenceService _persistence;
    private readonly TagWorkflowService _tags;

    public RunCoordinator(string databasePath)
    {
        _persistence = new InventoryPersistenceService(databasePath);
        _tags = new TagWorkflowService(databasePath);
    }

    public async Task<RunCycleResult> CommitObservationAndTagsAsync(
        string runId,
        InventoryScanItem item,
        string screenshotPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);

        await _persistence.ImportAsync(runId, item, screenshotPath, cancellationToken);
        var localPokemonId = runId + ":" + item.SequenceNumber;
        var tags = new List<string>();
        var tagErrors = new List<string>();

        foreach (var tag in new[] { "AI-Indexed", "AI-Review" })
        {
            try
            {
                if (await _tags.IsVerifiedAsync(localPokemonId, tag, cancellationToken))
                {
                    tags.Add(tag);
                    continue;
                }

                await _tags.RequestAndRecordAsync(
                    localPokemonId,
                    tag,
                    verified: true,
                    cancellationToken: cancellationToken);
                tags.Add(tag);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                tagErrors.Add($"{tag}: {exception.Message}");
            }
        }

        return new RunCycleResult
        {
            RunId = runId,
            Sequence = item.SequenceNumber,
            LocalPokemonId = localPokemonId,
            ObservationCommitted = true,
            VerifiedTags = tags,
            TagErrors = tagErrors
        };
    }
}

public sealed record RunCycleResult
{
    public required string RunId { get; init; }
    public required int Sequence { get; init; }
    public required string LocalPokemonId { get; init; }
    public required bool ObservationCommitted { get; init; }
    public required IReadOnlyList<string> VerifiedTags { get; init; }
    public required IReadOnlyList<string> TagErrors { get; init; }
}
