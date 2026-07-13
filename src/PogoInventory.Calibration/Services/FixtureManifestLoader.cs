using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;

namespace PogoInventory.Calibration.Services;

public static class FixtureManifestLoader
{
    public static async Task<ScreenFixtureManifest> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<ScreenFixtureManifest>(
                stream,
                CalibrationJson.CreateOptions(),
                cancellationToken);
            if (manifest is null)
            {
                throw new CalibrationException(
                    CalibrationErrorCode.InvalidManifest,
                    $"The fixture manifest '{path}' contained no data.");
            }

            Validate(manifest);
            return manifest;
        }
        catch (CalibrationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidManifest,
                $"The fixture manifest '{path}' is not valid JSON: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CalibrationException(
                CalibrationErrorCode.FileSystemFailure,
                $"The fixture manifest '{path}' could not be read.",
                exception);
        }
    }

    public static void Validate(ScreenFixtureManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            Invalid("Manifest name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ProfileName))
        {
            Invalid("Profile name is required.");
        }

        ValidateAcceptance(manifest.Acceptance);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fixture in manifest.Fixtures)
        {
            if (string.IsNullOrWhiteSpace(fixture.Id))
            {
                Invalid("Every fixture requires an id.");
            }

            if (!ids.Add(fixture.Id))
            {
                Invalid($"Duplicate fixture id: '{fixture.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(fixture.RelativePath))
            {
                Invalid($"Fixture '{fixture.Id}' requires a relative path.");
            }

            _ = CalibrationPathSafety.ResolveInsideRoot(
                Path.GetTempPath(),
                fixture.RelativePath,
                $"fixture '{fixture.Id}'");

            var portablePath = fixture.RelativePath.Replace('\\', '/');
            if (!paths.Add(portablePath))
            {
                Invalid($"Duplicate fixture path: '{fixture.RelativePath}'.");
            }

            if (!CalibrationPathSafety.IsSha256(fixture.Sha256))
            {
                Invalid($"Fixture '{fixture.Id}' has an invalid SHA-256 value.");
            }

            if (fixture.SafetyReview is null)
            {
                Invalid($"Fixture '{fixture.Id}' requires a safety review object.");
            }
        }
    }

    private static void ValidateAcceptance(CalibrationAcceptancePolicy policy)
    {
        if (policy.MaximumFalsePositives < 0 ||
            policy.MaximumMisclassifications < 0 ||
            policy.MaximumWeakAnchors < 0)
        {
            Invalid("Acceptance count limits cannot be negative.");
        }

        if (!IsUnitInterval(policy.MinimumWinnerMargin) ||
            !IsUnitInterval(policy.MinimumAnchorSeparation))
        {
            Invalid("Acceptance margins must be between 0 and 1.");
        }

        var states = new HashSet<PogoInventory.Vision.Models.ScreenState>();
        foreach (var requirement in policy.States)
        {
            if (!states.Add(requirement.State))
            {
                Invalid($"Duplicate acceptance requirement for state '{requirement.State}'.");
            }

            if (requirement.MinimumApprovedFixtures < 0)
            {
                Invalid($"Minimum fixture count for '{requirement.State}' cannot be negative.");
            }

            if (!IsUnitInterval(requirement.MinimumRecall))
            {
                Invalid($"Minimum recall for '{requirement.State}' must be between 0 and 1.");
            }
        }
    }

    private static bool IsUnitInterval(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1;

    private static void Invalid(string message) =>
        throw new CalibrationException(CalibrationErrorCode.InvalidManifest, message);
}
