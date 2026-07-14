using System.Text.Json;
using PogoInventory.Appraisal.Models;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalProfileLoader
{
    public static async Task<AppraisalVisualProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var json = await File.ReadAllTextAsync(
            fullPath,
            cancellationToken);
        var profile = JsonSerializer.Deserialize<AppraisalVisualProfile>(
            json,
            AppraisalJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                $"Could not deserialize appraisal profile '{fullPath}'.");
        profile.Validate();
        return profile;
    }

    public static async Task WriteAsync(
        AppraisalVisualProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        profile.Validate();

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "Appraisal profile path has no parent directory."));
        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(
                profile,
                AppraisalJson.CreateOptions(writeIndented: true)),
            cancellationToken);
    }
}
