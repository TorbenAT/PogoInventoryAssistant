using System.Text.Json;
using PogoInventory.Automation.Models;

namespace PogoInventory.Automation.Services;

public static class InventoryControlProfileLoader
{
    public static async Task<InventoryControlProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var profile = JsonSerializer.Deserialize<InventoryControlProfile>(
            json,
            AutomationJson.CreateOptions()) ??
            throw new InvalidOperationException($"Control profile '{path}' contained no data.");
        profile.Validate();
        return profile;
    }
}
