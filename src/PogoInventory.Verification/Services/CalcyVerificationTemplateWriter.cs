using System.Text.Json;
using PogoInventory.Verification.Models;

namespace PogoInventory.Verification.Services;

public static class CalcyVerificationTemplateWriter
{
    public static async Task<string> InitializeAsync(
        string outputDirectory,
        int caseCount = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (caseCount < 20 || caseCount > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caseCount),
                "Verification initialization requires between 20 and 1000 cases.");
        }

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        var cases = Enumerable.Range(1, caseCount)
            .Select(number =>
            {
                var id = number.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Path.Combine(root, "cases", id));
                return new CalcyVerificationCase
                {
                    Id = id,
                    Sources = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["logcat"] = $"cases/{id}/raw.txt"
                    },
                    Expected = new ExpectedPokemonObservation()
                };
            })
            .ToArray();

        var manifest = new CalcyVerificationManifest
        {
            Name = "Real Calcy provider verification",
            Mechanism = CalcyProviderMechanism.Unknown,
            MinimumCases = caseCount,
            MinimumExactCompleteRate = 0.95,
            Cases = cases
        };
        var manifestPath = Path.Combine(root, "verification-manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                VerificationJson.CreateOptions(writeIndented: true)),
            cancellationToken);

        var instructions = """
            # Local Calcy verification workspace

            This directory is local evidence and should remain outside Git.

            1. Set the proven mechanism and installed Calcy version in verification-manifest.json.
            2. For every case, add the actual raw evidence file or point observationPath at a parsed observation.
            3. Fill species or Pokédex number, CP and the three IV values under expected.
            4. Run calcy-verification-run.
            5. A provider cannot be selected unless at least 20 cases were verified with zero wrong Complete observations.
            """;
        await File.WriteAllTextAsync(
            Path.Combine(root, "README.md"),
            instructions,
            cancellationToken);
        return manifestPath;
    }
}
