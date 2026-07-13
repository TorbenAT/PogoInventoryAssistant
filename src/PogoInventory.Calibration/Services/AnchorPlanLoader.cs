using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Services;

public static class AnchorPlanLoader
{
    public static async Task<CalibrationAnchorPlan> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = File.OpenRead(path);
            var plan = await JsonSerializer.DeserializeAsync<CalibrationAnchorPlan>(
                stream,
                CalibrationJson.CreateOptions(),
                cancellationToken);
            if (plan is null)
            {
                throw new CalibrationException(
                    CalibrationErrorCode.InvalidAnchorPlan,
                    $"The anchor plan '{path}' contained no data.");
            }

            Validate(plan);
            return plan;
        }
        catch (CalibrationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidAnchorPlan,
                $"The anchor plan '{path}' is not valid JSON: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CalibrationException(
                CalibrationErrorCode.FileSystemFailure,
                $"The anchor plan '{path}' could not be read.",
                exception);
        }
    }

    public static void Validate(CalibrationAnchorPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            Invalid("Anchor plan name is required.");
        }

        if (plan.MinimumWidth <= 0 || plan.MinimumHeight <= 0)
        {
            Invalid("Anchor plan minimum dimensions must be positive.");
        }

        if (!PositiveFinite(plan.MinimumAspectRatio) ||
            !PositiveFinite(plan.MaximumAspectRatio) ||
            plan.MinimumAspectRatio > plan.MaximumAspectRatio)
        {
            Invalid("Anchor plan aspect-ratio range is invalid.");
        }

        if (!UnitInterval(plan.MinimumStateScore) ||
            !UnitInterval(plan.MinimumWinnerMargin))
        {
            Invalid("Anchor plan score and winner margin must be between 0 and 1.");
        }

        var states = new HashSet<ScreenState>();
        foreach (var state in plan.States)
        {
            if (state.State == ScreenState.Unknown)
            {
                Invalid("Unknown cannot be a classified state in an anchor plan.");
            }

            if (!states.Add(state.State))
            {
                Invalid($"Duplicate anchor state '{state.State}'.");
            }

            var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var anchor in state.Anchors)
            {
                if (string.IsNullOrWhiteSpace(anchor.Name))
                {
                    Invalid($"State '{state.State}' contains an anchor without a name.");
                }

                if (!anchors.Add(anchor.Name))
                {
                    Invalid($"State '{state.State}' contains duplicate anchor '{anchor.Name}'.");
                }

                anchor.Region.Validate(anchor.Name);

                if (anchor.FingerprintWidth <= 0 || anchor.FingerprintHeight <= 0)
                {
                    Invalid($"Anchor '{anchor.Name}' has invalid fingerprint dimensions.");
                }

                if (!UnitInterval(anchor.MatchThreshold) ||
                    !PositiveFinite(anchor.Weight))
                {
                    Invalid($"Anchor '{anchor.Name}' has an invalid threshold or weight.");
                }

                if (anchor.SampleFixtureIds.Count == 0)
                {
                    Invalid($"Anchor '{anchor.Name}' requires at least one sample fixture id.");
                }

                var sampleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var sampleId in anchor.SampleFixtureIds)
                {
                    if (string.IsNullOrWhiteSpace(sampleId) || !sampleIds.Add(sampleId))
                    {
                        Invalid($"Anchor '{anchor.Name}' contains a blank or duplicate sample id.");
                    }
                }
            }
        }
    }

    private static bool UnitInterval(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1;

    private static bool PositiveFinite(double value) =>
        double.IsFinite(value) && value > 0;

    private static void Invalid(string message) =>
        throw new CalibrationException(CalibrationErrorCode.InvalidAnchorPlan, message);
}
