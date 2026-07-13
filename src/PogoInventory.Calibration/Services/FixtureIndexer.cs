using System.Text;
using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Workspace;
using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Services;

public static class FixtureIndexer
{
    public static async Task<CalibrationIndexResult> IndexAsync(
        CalibrationWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var existing = await FixtureManifestLoader.LoadAsync(
            workspace.ManifestPath,
            cancellationToken);
        var existingByPath = existing.Fixtures.ToDictionary(
            x => Portable(x.RelativePath),
            StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(
                workspace.FixturesPath,
                "*.png",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fixtures = new List<ScreenFixtureDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newCount = 0;
        var changedCount = 0;
        var preservedApprovalCount = 0;

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Portable(Path.GetRelativePath(workspace.FixturesPath, path));
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 ||
                !Enum.TryParse<ScreenState>(segments[0], ignoreCase: true, out var expectedState))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.InvalidManifest,
                    $"Fixture '{relative}' must be inside fixtures/<ScreenState>/.");
            }

            var hash = await CalibrationHash.Sha256Async(path, cancellationToken);
            ScreenFixtureDefinition definition;
            if (existingByPath.TryGetValue(relative, out var previous) &&
                previous.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                definition = previous with { ExpectedState = expectedState };
                if (definition.SafetyReview.IsComplete)
                {
                    preservedApprovalCount++;
                }
            }
            else
            {
                if (previous is null)
                {
                    newCount++;
                }
                else
                {
                    changedCount++;
                }

                var id = CreateUniqueId(expectedState, relative, ids);
                definition = new ScreenFixtureDefinition
                {
                    Id = previous?.Id ?? id,
                    RelativePath = relative,
                    ExpectedState = expectedState,
                    Sha256 = hash,
                    SafetyReview = new FixtureSafetyReview(),
                    Tags = previous?.Tags ?? Array.Empty<string>(),
                    Notes = previous is null
                        ? "New fixture. Complete the safety review before calibration."
                        : "File changed. Previous approval was reset. Review again before calibration."
                };
            }

            if (!ids.Add(definition.Id))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.InvalidManifest,
                    $"Duplicate fixture id after indexing: '{definition.Id}'.");
            }

            fixtures.Add(definition);
        }

        var updated = existing with { Fixtures = fixtures };
        FixtureManifestLoader.Validate(updated);
        await AtomicFile.WriteTextAsync(
            workspace.ManifestPath,
            JsonSerializer.Serialize(
                updated,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);

        return new CalibrationIndexResult
        {
            FixtureCount = fixtures.Count,
            NewFixtureCount = newCount,
            ChangedFixtureCount = changedCount,
            PreservedApprovalCount = preservedApprovalCount,
            ManifestPath = workspace.ManifestPath
        };
    }

    private static string Portable(string path) => path.Replace('\\', '/');

    private static string CreateUniqueId(
        ScreenState state,
        string relativePath,
        ISet<string> existingIds)
    {
        var source = Path.ChangeExtension(relativePath, null) ?? relativePath;
        var builder = new StringBuilder();
        foreach (var character in source)
        {
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-');
        }

        var compact = string.Join(
            '-',
            builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        var baseId = $"{state.ToString().ToLowerInvariant()}-{compact}";
        var candidate = baseId;
        var suffix = 2;
        while (existingIds.Contains(candidate))
        {
            candidate = $"{baseId}-{suffix++}";
        }

        return candidate;
    }
}
