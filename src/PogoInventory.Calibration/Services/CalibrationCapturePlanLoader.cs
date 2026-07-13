using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Services;

public static class CalibrationCapturePlanLoader
{
    public static async Task<CalibrationCapturePlan> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = File.OpenRead(path);
            var plan = await JsonSerializer.DeserializeAsync<CalibrationCapturePlan>(
                stream,
                CalibrationJson.CreateOptions(),
                cancellationToken);
            if (plan is null)
            {
                Invalid($"Capture plan '{path}' contained no data.");
            }

            Validate(plan!);
            return plan!;
        }
        catch (CalibrationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidCapturePlan,
                $"Capture plan '{path}' is not valid JSON: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CalibrationException(
                CalibrationErrorCode.FileSystemFailure,
                $"Capture plan '{path}' could not be read.",
                exception);
        }
    }

    public static string ComputeFingerprint(CalibrationCapturePlan plan)
    {
        Validate(plan);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            plan,
            CalibrationJson.CreateOptions());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static void Validate(CalibrationCapturePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            Invalid("Capture plan name is required.");
        }

        if (plan.RequiredOrientation == ScreenOrientation.Any)
        {
            Invalid("Capture plan must require a concrete screen orientation.");
        }

        if (plan.MinimumWidth <= 0 || plan.MinimumHeight <= 0)
        {
            Invalid("Capture plan minimum width and height must be positive.");
        }

        if (plan.Requirements.Count == 0)
        {
            Invalid("Capture plan requires at least one screen-state requirement.");
        }

        var states = new HashSet<ScreenState>();
        foreach (var requirement in plan.Requirements)
        {
            if (!states.Add(requirement.State))
            {
                Invalid($"Duplicate capture requirement for state '{requirement.State}'.");
            }

            if (requirement.RequiredUniqueCaptures <= 0)
            {
                Invalid($"State '{requirement.State}' must require at least one unique capture.");
            }

            if (string.IsNullOrWhiteSpace(requirement.Instruction))
            {
                Invalid($"State '{requirement.State}' requires a manual navigation instruction.");
            }
        }

        if (!states.Contains(ScreenState.Unknown))
        {
            Invalid("Capture plan must include Unknown negative examples.");
        }
    }

    private static void Invalid(string message) =>
        throw new CalibrationException(CalibrationErrorCode.InvalidCapturePlan, message);
}
