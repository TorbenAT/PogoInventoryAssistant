using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Vision.Profiles;

public static class ScreenProfileLoader
{
    public static JsonSerializerOptions CreateJsonOptions(bool writeIndented = false) =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };

    public static async Task<ScreenDetectionProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ScreenDetectionProfile? profile;
        try
        {
            await using var stream = File.OpenRead(path);
            profile = await JsonSerializer.DeserializeAsync<ScreenDetectionProfile>(
                stream,
                CreateJsonOptions(),
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidProfile,
                $"The screen profile '{path}' is not valid JSON: {exception.Message}",
                exception);
        }
        catch (IOException exception)
        {
            throw new ScreenVisionException(
                VisionErrorCode.FileSystemFailure,
                $"The screen profile '{path}' could not be read.",
                exception);
        }

        if (profile is null)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidProfile,
                $"The screen profile '{path}' contained no data.");
        }

        Validate(profile);
        return profile;
    }

    public static void Validate(ScreenDetectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            Invalid("Profile name is required.");
        }

        if (profile.MinimumWidth <= 0 || profile.MinimumHeight <= 0)
        {
            Invalid("Minimum image dimensions must be positive.");
        }

        if (!double.IsFinite(profile.MinimumAspectRatio) ||
            !double.IsFinite(profile.MaximumAspectRatio) ||
            profile.MinimumAspectRatio <= 0 ||
            profile.MaximumAspectRatio < profile.MinimumAspectRatio)
        {
            Invalid("The profile aspect-ratio range is invalid.");
        }

        ValidateUnitInterval(profile.MinimumStateScore, "minimum state score");
        ValidateUnitInterval(profile.MinimumWinnerMargin, "minimum winner margin");

        if (profile.States.Count == 0)
        {
            Invalid("At least one screen-state definition is required.");
        }

        var duplicateStates = profile.States
            .GroupBy(x => x.State)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();
        if (duplicateStates.Length > 0)
        {
            Invalid($"Duplicate state definitions: {string.Join(", ", duplicateStates)}.");
        }

        foreach (var state in profile.States)
        {
            if (state.State == ScreenState.Unknown)
            {
                Invalid("Unknown is an output state and cannot have an anchor definition.");
            }

            if (state.Anchors.Count == 0)
            {
                Invalid($"State {state.State} has no anchors.");
            }

            var duplicateAnchors = state.Anchors
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToArray();
            if (duplicateAnchors.Length > 0)
            {
                Invalid(
                    $"State {state.State} has duplicate anchor names: " +
                    string.Join(", ", duplicateAnchors));
            }

            if (!state.Anchors.Any(x => x.Expectation == AnchorExpectation.Required))
            {
                Invalid($"State {state.State} must contain at least one required anchor.");
            }

            foreach (var anchor in state.Anchors)
            {
                ValidateAnchor(state.State, anchor);
            }
        }
    }

    public static IReadOnlyList<byte[]> DecodeSamples(ScreenAnchorDefinition anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var expectedLength = FingerprintExtractor.ExpectedLength(
            anchor.Mode,
            anchor.FingerprintWidth,
            anchor.FingerprintHeight);
        var result = new List<byte[]>(anchor.SamplesBase64.Count);

        for (var index = 0; index < anchor.SamplesBase64.Count; index++)
        {
            byte[] sample;
            try
            {
                sample = Convert.FromBase64String(anchor.SamplesBase64[index]);
            }
            catch (FormatException exception)
            {
                throw new ScreenVisionException(
                    VisionErrorCode.InvalidProfile,
                    $"Anchor '{anchor.Name}' sample {index} is not valid Base64.",
                    exception);
            }

            if (sample.Length != expectedLength)
            {
                throw new ScreenVisionException(
                    VisionErrorCode.InvalidProfile,
                    $"Anchor '{anchor.Name}' sample {index} has {sample.Length} bytes; " +
                    $"expected {expectedLength}.");
            }

            result.Add(sample);
        }

        return result;
    }

    private static void ValidateAnchor(
        ScreenState state,
        ScreenAnchorDefinition anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor.Name))
        {
            Invalid($"State {state} contains an anchor without a name.");
        }

        if (anchor.Region is null)
        {
            Invalid($"Anchor '{anchor.Name}' has no region.");
        }

        try
        {
            anchor.Region.Validate(anchor.Name);
        }
        catch (ScreenVisionException exception)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidProfile,
                exception.Message,
                exception);
        }

        if (anchor.FingerprintWidth is < 2 or > 64 ||
            anchor.FingerprintHeight is < 2 or > 64)
        {
            Invalid($"Anchor '{anchor.Name}' fingerprint dimensions must be 2..64.");
        }

        ValidateUnitInterval(anchor.MatchThreshold, $"anchor '{anchor.Name}' match threshold");

        if (!double.IsFinite(anchor.Weight) || anchor.Weight <= 0)
        {
            Invalid($"Anchor '{anchor.Name}' weight must be positive.");
        }

        if (anchor.SamplesBase64.Count == 0)
        {
            Invalid($"Anchor '{anchor.Name}' must contain at least one sample.");
        }

        _ = DecodeSamples(anchor);
    }

    private static void ValidateUnitInterval(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            Invalid($"The {name} must be between 0 and 1.");
        }
    }

    [DoesNotReturn]
    private static void Invalid(string message) =>
        throw new ScreenVisionException(VisionErrorCode.InvalidProfile, message);
}
