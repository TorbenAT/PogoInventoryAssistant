using System.Text.Json;
using PogoInventory.Verification.Models;

namespace PogoInventory.Verification.Services;

public static class CalcyVerificationManifestLoader
{
    public static async Task<CalcyVerificationManifest> LoadForRunAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var manifest = JsonSerializer.Deserialize<CalcyVerificationManifest>(
            await File.ReadAllTextAsync(path, cancellationToken),
            VerificationJson.CreateOptions()) ?? throw new InvalidOperationException(
                $"Verification manifest '{path}' contained no data.");
        manifest.ValidateForRun();
        return manifest;
    }
}
