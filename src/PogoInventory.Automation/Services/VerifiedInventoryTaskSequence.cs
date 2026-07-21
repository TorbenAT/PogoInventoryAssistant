using System.Text.Json;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Services;

public interface IVerifiedInventoryNamedOperations
{
    Task<VerifiedSequenceState> EnsureFilteredInventoryAsync(string query, CancellationToken cancellationToken);
    Task<VerifiedSequenceState> OpenFirstPokemonAsync(CancellationToken cancellationToken);
    Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken);
    Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken);
    Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken);
    Task<VerifiedTagObservation> ReadTagObservationAsync(CancellationToken cancellationToken);
    Task<VerifiedSequenceState> AdvanceToNextPokemonAsync(
        PokemonIdentityConsensus previous,
        CancellationToken cancellationToken);
    Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ApplyIndexTagAsync(string tagName, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ApplyClassificationTagAsync(string tagName, CancellationToken cancellationToken);
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
        var existing = request.Resume ? await LoadAsync(checkpointPath, cancellationToken) : null;
        if (existing is not null)
        {
            if (existing.Query != request.Query || existing.ItemLimit != request.ItemLimit ||
                existing.ApplyIndexTag != request.ApplyIndexTag || existing.IndexTag != request.IndexTag ||
                existing.ApplyClassificationTag != request.ApplyClassificationTag ||
                existing.ClassificationTag != request.ClassificationTag)
                throw new InvalidOperationException(
                    $"Existing sequence checkpoint does not match the request: " +
                    $"query={existing.Query}/{request.Query}, limit={existing.ItemLimit}/{request.ItemLimit}, " +
                    $"index={existing.ApplyIndexTag}/{request.ApplyIndexTag}:{existing.IndexTag}/{request.IndexTag}, " +
                    $"classification={existing.ApplyClassificationTag}/{request.ApplyClassificationTag}:{existing.ClassificationTag}/{request.ClassificationTag}.");
            runId = existing.ScanRunId;
            if (existing.State is VerifiedSequenceState.Unknown or VerifiedSequenceState.Stopped)
                return new VerifiedSequenceResult { Checkpoint = existing, CheckpointPath = checkpointPath };
        }
        var items = existing?.Items.ToList() ?? new List<VerifiedSequenceItem>();
        var checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.Inventory, "EnsureFilteredInventory");
        await SaveAsync(output, checkpoint, cancellationToken);

        var inventory = await _operations.EnsureFilteredInventoryAsync(request.Query, cancellationToken);
        if (inventory != VerifiedSequenceState.Inventory)
            return await StopAsync(output, checkpoint, items, inventory, "EnsureFilteredInventory did not restore Inventory", cancellationToken);

        var currentIdentity = default(PokemonIdentityConsensus);
        var opened = await _operations.OpenFirstPokemonAsync(cancellationToken);
        if (opened == VerifiedSequenceState.Unknown)
            return await StopAsync(output, checkpoint, items, opened, "Unknown after open; no further input", cancellationToken);
        if (opened != VerifiedSequenceState.PokemonDetails)
            return await StopAsync(output, checkpoint, items, opened, "PokemonDetails was not verified", cancellationToken);

        if (items.Count > 0)
        {
            // Resume is fail-closed: replay only the already verified cursor and
            // require an identity overlap before allowing another swipe.
            for (var ordinal = 1; ordinal <= items.Count; ordinal++)
            {
                currentIdentity = await _operations.CaptureIdentityAsync(cancellationToken);
                if (ordinal == items.Count)
                {
                    var expected = items[^1].StableFingerprintSha256;
                    if (string.IsNullOrWhiteSpace(expected) ||
                        !string.Equals(expected, currentIdentity.StableFingerprintSha256, StringComparison.Ordinal))
                        return await StopAsync(output, checkpoint, items, VerifiedSequenceState.Unknown,
                            "Resume identity overlap mismatch; no further swipe", cancellationToken);
                }
                if (ordinal < items.Count)
                {
                    var replay = await _operations.AdvanceToNextPokemonAsync(currentIdentity, cancellationToken);
                    if (replay != VerifiedSequenceState.PokemonDetails)
                        return await StopAsync(output, checkpoint, items, replay,
                            "Resume replay did not verify Details", cancellationToken);
                }
            }
        }
        else
        {
            currentIdentity = await _operations.CaptureIdentityAsync(cancellationToken);
        }

        while (items.Count < request.ItemLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = currentIdentity!;
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
                    IdentityStatus = identity.Status,
                    TagObservation = ToTagObservation(identity.Tags),
                    Detail = "Identity consensus was Partial/Unavailable; item preserved."
                };
                items.Add(partial);
                checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.PokemonDetails,
                    "CheckpointAfterPartial", identity);
                await SaveAsync(output, checkpoint, cancellationToken);
            }
            else
            {
                var appraisal = await _operations.CaptureAppraisalAsync(cancellationToken);
                var afterAppraisal = await _operations.ExitAppraisalAsync(cancellationToken);
                if (afterAppraisal == VerifiedSequenceState.Unknown)
                    return await StopAsync(output, checkpoint, items, afterAppraisal, "Unknown during appraisal exit", cancellationToken);
                if (afterAppraisal != VerifiedSequenceState.PokemonDetails)
                    return await StopAsync(output, checkpoint, items, afterAppraisal, "Appraisal exit did not restore Details", cancellationToken);

                var tagObservation = await _operations.ReadTagObservationAsync(cancellationToken);
                if (request.ApplyIndexTag)
                    await _operations.ApplyIndexTagAsync(request.IndexTag, cancellationToken);
                if (request.ApplyClassificationTag)
                    await _operations.ApplyClassificationTagAsync(request.ClassificationTag!, cancellationToken);
                items.Add(new VerifiedSequenceItem
                {
                    Ordinal = items.Count + 1,
                    InstanceId = $"{runId}:{items.Count + 1:D6}",
                    StableFingerprintSha256 = identity.StableFingerprintSha256,
                    EvidenceHashes = identity.EvidenceHashes,
                    State = VerifiedSequenceState.PokemonDetails,
                    Query = request.Query,
                    IdentityStatus = identity.Status,
                    TagObservation = tagObservation,
                    AppraisalStatus = appraisal
                });
                checkpoint = NewCheckpoint(runId, request, items, VerifiedSequenceState.PokemonDetails,
                    "CheckpointAfterItem", identity, tagObservation);
            }
            await SaveAsync(output, checkpoint, cancellationToken);

            if (request.ControlledStopAfter == items.Count)
            {
                var stopped = await _operations.ReturnToInventoryAsync(cancellationToken);
                checkpoint = checkpoint with { State = VerifiedSequenceState.Stopped,
                    LastVerifiedState = stopped, NextAction = "ControlledStopAfterItem" };
                return await SaveResultAsync(output, checkpoint, cancellationToken);
            }
            if (items.Count >= request.ItemLimit) break;

            var advanced = await _operations.AdvanceToNextPokemonAsync(identity, cancellationToken);
            checkpoint = NewCheckpoint(runId, request, items, advanced, "CheckpointAfterSwipe", identity);
            await SaveAsync(output, checkpoint, cancellationToken);
            if (advanced != VerifiedSequenceState.PokemonDetails)
                return await StopAsync(output, checkpoint, items, advanced,
                    advanced == VerifiedSequenceState.Unknown ? "Swipe result Unknown; no further input" : "Swipe did not verify Details",
                    cancellationToken);
            currentIdentity = await _operations.CaptureIdentityAsync(cancellationToken);
        }

        var restored = await _operations.ReturnToInventoryAsync(cancellationToken);
        checkpoint = NewCheckpoint(runId, request, items, restored, "Complete", currentIdentity);
        return await SaveResultAsync(output, checkpoint, cancellationToken);
    }

    private static VerifiedSequenceCheckpoint NewCheckpoint(string runId, VerifiedSequenceRequest request,
        IReadOnlyList<VerifiedSequenceItem> items, VerifiedSequenceState state, string nextAction,
        PokemonIdentityConsensus? identity = null, VerifiedTagObservation? tagObservation = null) => new()
    {
        ScanRunId = runId, Query = request.Query, ItemLimit = request.ItemLimit,
        ApplyIndexTag = request.ApplyIndexTag, IndexTag = request.IndexTag,
        ApplyClassificationTag = request.ApplyClassificationTag, ClassificationTag = request.ClassificationTag,
        CurrentOrdinal = items.Count, LastCompletedOrdinal = items.Count,
        PreviousStableFingerprint = items.Count > 1 ? items[^2].StableFingerprintSha256 : null,
        CurrentStableFingerprint = identity?.StableFingerprintSha256 ?? items.LastOrDefault()?.StableFingerprintSha256,
        LastVerifiedState = state, IdentityStatus = identity?.Status,
        EvidenceHashes = identity?.EvidenceHashes ?? items.LastOrDefault()?.EvidenceHashes ?? Array.Empty<string>(),
        TagObservation = tagObservation ?? items.LastOrDefault()?.TagObservation,
        State = state, Items = items.ToArray(), UpdatedAtUtc = DateTimeOffset.UtcNow, NextAction = nextAction
    };

    private static VerifiedTagObservation ToTagObservation(PokemonIdentityTagObservation tags) => new()
    {
        TagCount = tags.TagCount, KnownTagNames = tags.TagNames, NamesComplete = tags.TagNames.Count >= tags.TagCount,
        Section = tags.Section, Evidence = new[] { "identity-tag-observation" }
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
