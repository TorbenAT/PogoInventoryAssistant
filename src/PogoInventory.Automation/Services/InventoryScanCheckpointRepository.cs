using System.Text.Json;
using PogoInventory.Automation.Errors;
using PogoInventory.Automation.Models;

namespace PogoInventory.Automation.Services;

public static class InventoryScanCheckpointRepository
{
    public const string FileName = "inventory-scan-checkpoint.json";

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
            var checkpoint = JsonSerializer.Deserialize<InventoryScanCheckpoint>(
                json,
                AutomationJson.CreateOptions(writeIndented: false)) ??
                throw new AutomationException(
                    AutomationErrorCode.CheckpointCorrupt,
                    $"Checkpoint '{path}' contained no data.");

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

    private static void Validate(InventoryScanCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != "1.0")
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

            expected++;
        }
    }
}
