using System.Text.Json;
using PogoInventory.Automation.Errors;
using PogoInventory.Automation.Models;

namespace PogoInventory.Automation.Services;

public static class AutomationProfileLoader
{
    public static async Task<InventoryAutomationProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var profile = JsonSerializer.Deserialize<InventoryAutomationProfile>(
                json,
                AutomationJson.CreateOptions(writeIndented: false)) ??
                throw new AutomationException(
                    AutomationErrorCode.InvalidProfile,
                    $"Automation profile '{path}' contained no data.");
            profile.Validate();
            return profile;
        }
        catch (AutomationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            throw new AutomationException(
                AutomationErrorCode.InvalidProfile,
                $"Could not load automation profile '{path}'.",
                exception);
        }
    }
}
