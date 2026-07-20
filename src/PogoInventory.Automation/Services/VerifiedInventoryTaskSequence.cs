using System.Text.Json;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Services;

public interface IVerifiedInventoryNamedOperations
{
    Task<VerifiedSequenceState> EnsureInventoryAsync(string query, CancellationToken cancellationToken);
    Task<VerifiedSequenceState> OpenNextPokemonAsync(CancellationToken cancellationToken);
    Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken);
    Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken);
    Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken);
    Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ReadTagsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ApplyAllowListedTagAsync(string tagName, CancellationToken cancellationToken);
}

/// <summary>One bounded sequential task that composes already-validated named operations.</summary>
public sealed class VerifiedInventoryTaskSequence
{
    private readonly IVerifiedInventoryNamedOperations _operations;

    public VerifiedInventoryTaskSequence(IVerifiedInventoryNamedOperations operations)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public async Task<VerifiedSequenceResult> RunAsync(
        VerifiedSequenceRequest request,
        string? scanRunId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var output = Path.GetFullPath(request.OutputDirectory);
        Directory.CreateDirectory(output);
        var runId = string.IsNullOrWhiteSpace(scanRunId)
            ? $"sequence-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..35]
            : scanRunId;
        var checkpointPath = Path.Combine(output, "verified-sequence-checkpoint.json");
        var existing = await LoadAsync(checkpointPath, cancellationToken);
        if (existing is not null)
        {
            if (existing.Query != request.Query || existing.ItemLimit != request.ItemLimit ||
                existing.ApplyTags != request.ApplyTags || existing.ClassificationTag != request.ClassificationTag)
                throw new InvalidOperationException("Existing sequence checkpoint does not match the request.");
            runId = existing.ScanRunId;
            if (existing.State is VerifiedSequenceState.Unknown or VerifiedSequenceState.Partial or VerifiedSequenceState.Stopped)
                return new VerifiedSequenceResult { Checkpoint = existing, CheckpointPath = checkpointPath };
        }
        var items = existing?.Items.ToList() ?? new List<VerifiedSequenceItem>();
        var checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.Inventory, "EnsureInventory");
        await SaveAsync(output, checkpoint, cancellationToken);

        var inventory = await _operations.EnsureInventoryAsync(request.Query, cancellationToken);
        if (inventory != VerifiedSequenceState.Inventory)
            return await StopAsync(output, checkpoint, items, inventory, "EnsureInventory did not restore Inventory", cancellationToken);

        while (items.Count < request.ItemLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var opened = await _operations.OpenNextPokemonAsync(cancellationToken);
            if (opened == VerifiedSequenceState.Unknown)
                return await StopAsync(output, checkpoint, items, opened, "Unknown after open; no further input", cancellationToken);
            if (opened != VerifiedSequenceState.PokemonDetails)
                return await StopAsync(output, checkpoint, items, opened, "PokemonDetails was not verified", cancellationToken);

            var identity = await _operations.CaptureIdentityAsync(cancellationToken);
            if (identity.Status is not PokemonIdentityObservationStatus.Complete ||
                string.IsNullOrWhiteSpace(identity.StableFingerprintSha256))
            {
                var partial = new VerifiedSequenceItem
                {
                    Ordinal = items.Count + 1,
                    InstanceId = $"{runId}:{items.Count + 1:D6}",
                    StableFingerprintSha256 = identity.StableFingerprintSha256,
                    EvidenceHashes = identity.EvidenceHashes,
                    State = VerifiedSequenceState.Partial,
                    Query = request.Query,
                    Tags = identity.Tags.TagNames,
                    Detail = "Identity consensus was Partial/Unavailable; item preserved and run stopped safely."
                };
                items.Add(partial);
                checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.Partial, "RestoreInventory");
                return await SaveResultAsync(output, checkpoint, cancellationToken);
            }

            var appraisal = await _operations.CaptureAppraisalAsync(cancellationToken);
            var afterAppraisal = await _operations.ExitAppraisalAsync(cancellationToken);
            if (afterAppraisal == VerifiedSequenceState.Unknown)
                return await StopAsync(output, checkpoint, items, afterAppraisal, "Unknown during appraisal exit", cancellationToken);
            if (afterAppraisal != VerifiedSequenceState.PokemonDetails)
                return await StopAsync(output, checkpoint, items, afterAppraisal, "Appraisal exit did not restore Details", cancellationToken);

            var tags = request.ApplyTags
                ? await _operations.ApplyAllowListedTagAsync(request.ClassificationTag!, cancellationToken)
                : await _operations.ReadTagsAsync(cancellationToken);
            var item = new VerifiedSequenceItem
            {
                Ordinal = items.Count + 1,
                InstanceId = $"{runId}:{items.Count + 1:D6}",
                StableFingerprintSha256 = identity.StableFingerprintSha256,
                EvidenceHashes = identity.EvidenceHashes,
                State = VerifiedSequenceState.PokemonDetails,
                Query = request.Query,
                Tags = tags,
                AppraisalStatus = appraisal
            };
            items.Add(item);
            var restored = await _operations.ReturnToInventoryAsync(cancellationToken);
            checkpoint = NewCheckpoint(runId, request, items, restored, "CheckpointAfterItem");
            await SaveAsync(output, checkpoint, cancellationToken);
            if (restored != VerifiedSequenceState.Inventory)
                return await StopAsync(output, checkpoint, items, restored, "Inventory was not restored", cancellationToken);
        }

        checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.Inventory, "Complete");
        return await SaveResultAsync(output, checkpoint, cancellationToken);
    }

    private static VerifiedSequenceCheckpoint NewCheckpoint(string runId, VerifiedSequenceRequest request,
        IReadOnlyList<VerifiedSequenceItem> items, VerifiedSequenceState state, string nextAction) => new()
    {
        ScanRunId = runId, Query = request.Query, ItemLimit = request.ItemLimit,
        ApplyTags = request.ApplyTags, ClassificationTag = request.ClassificationTag,
        State = state, Items = items.ToArray(), UpdatedAtUtc = DateTimeOffset.UtcNow, NextAction = nextAction
    };

    private static async Task<VerifiedSequenceResult> StopAsync(string output, VerifiedSequenceCheckpoint original,
        IReadOnlyList<VerifiedSequenceItem> items, VerifiedSequenceState state, string detail,
        CancellationToken cancellationToken) => await SaveResultAsync(output, original with
    {
        State = state, Items = items.ToArray(), NextAction = "ControlledStop:" + detail,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    }, cancellationToken);

    private static async Task<VerifiedSequenceResult> SaveResultAsync(string output,
        VerifiedSequenceCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var path = Path.Combine(output, "verified-sequence-checkpoint.json");
        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
        await AutomationAtomicFile.WriteTextAsync(path, json, cancellationToken);
        return new VerifiedSequenceResult { Checkpoint = checkpoint, CheckpointPath = path };
    }

    private static async Task SaveAsync(string output, VerifiedSequenceCheckpoint checkpoint,
        CancellationToken cancellationToken) => _ = await SaveResultAsync(output, checkpoint, cancellationToken);

    private static async Task<VerifiedSequenceCheckpoint?> LoadAsync(string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<VerifiedSequenceCheckpoint>(json);
    }
}
