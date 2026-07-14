using System.Text.Json;
using PogoInventory.Automation.Errors;
using PogoInventory.Automation.Models;
using PogoInventory.Observations.Models;

namespace PogoInventory.Automation.Services;

public static class InventoryScanCheckpointRepository
{
    public const string FileName = "inventory-scan-checkpoint.json";
    public const string CurrentSchemaVersion = "2.0";

    public static async Task<InventoryScanCheckpoint?> LoadAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetFullPath(outputDirectory), FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var checkpoint = DeserializeAndMigrate(json, path);
            Validate(checkpoint);
            return checkpoint;
        }
        catch (AutomationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new AutomationException(
                AutomationErrorCode.CheckpointCorrupt,
                $"Could not read checkpoint '{path}'.",
                exception);
        }
    }

    public static async Task<string> SaveAsync(
        string outputDirectory,
        InventoryScanCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Validate(checkpoint);
        var path = Path.Combine(Path.GetFullPath(outputDirectory), FileName);
        var json = JsonSerializer.Serialize(
            checkpoint,
            AutomationJson.CreateOptions(writeIndented: true));
        await AutomationAtomicFile.WriteTextAsync(path, json, cancellationToken);
        return path;
    }

    public static InventoryScanCheckpoint DeserializeAndMigrate(
        string json,
        string sourceName = "checkpoint")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var schemaVersion = document.RootElement.TryGetProperty(
            "schemaVersion",
            out var schemaElement)
            ? schemaElement.GetString()
            : null;

        if (schemaVersion is null or "1.0")
        {
            var legacy = JsonSerializer.Deserialize<InventoryScanCheckpoint>(
                json,
                AutomationJson.CreateOptions(writeIndented: false)) ??
                throw new AutomationException(
                    AutomationErrorCode.CheckpointCorrupt,
                    $"Checkpoint '{sourceName}' contained no data.");

            return legacy with
            {
                SchemaVersion = CurrentSchemaVersion,
                MigratedFromSchemaVersion = schemaVersion ?? "1.0",
                Items = legacy.Items
                    .OrderBy(x => x.SequenceNumber)
                    .Select(item => item with
                    {
                        Observation = CalcyObservation.Unavailable(
                            "CheckpointMigration",
                            "Observation was not collected by checkpoint schema 1.0.")
                    })
                    .ToArray()
            };
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new AutomationException(
                AutomationErrorCode.CheckpointCorrupt,
                $"Unsupported checkpoint schema '{schemaVersion}'.");
        }

        return JsonSerializer.Deserialize<InventoryScanCheckpoint>(
            json,
            AutomationJson.CreateOptions(writeIndented: false)) ??
            throw new AutomationException(
                AutomationErrorCode.CheckpointCorrupt,
                $"Checkpoint '{sourceName}' contained no data.");
    }

    private static void Validate(InventoryScanCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != CurrentSchemaVersion)
        {
            throw new AutomationException(
                AutomationErrorCode.CheckpointCorrupt,
                $"Unsupported checkpoint schema '{checkpoint.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(checkpoint.RunId) ||
            string.IsNullOrWhiteSpace(checkpoint.DeviceSerial) ||
            string.IsNullOrWhiteSpace(checkpoint.AutomationProfileSha256) ||
            string.IsNullOrWhiteSpace(checkpoint.ScreenProfileSha256) ||
            checkpoint.ScreenWidth <= 0 ||
            checkpoint.ScreenHeight <= 0)
        {
            throw new AutomationException(
                AutomationErrorCode.CheckpointCorrupt,
                "Checkpoint is missing required run, device or geometry data.");
        }

        var expected = 1;
        foreach (var item in checkpoint.Items.OrderBy(x => x.SequenceNumber))
        {
            if (item.SequenceNumber != expected)
            {
                throw new AutomationException(
                    AutomationErrorCode.CheckpointCorrupt,
                    "Checkpoint item sequence is not contiguous.");
            }

            try
            {
                item.Observation.Validate();
            }
            catch (InvalidOperationException exception)
            {
                throw new AutomationException(
                    AutomationErrorCode.CheckpointCorrupt,
                    $"Checkpoint item {item.SequenceNumber} has an invalid observation: " +
                    exception.Message,
                    exception);
            }

            expected++;
        }
    }
}
