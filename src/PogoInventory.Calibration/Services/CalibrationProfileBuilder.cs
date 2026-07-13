using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;

namespace PogoInventory.Calibration.Services;

public static class CalibrationProfileBuilder
{
    public static async Task<ScreenDetectionProfile> BuildAsync(
        ScreenFixtureManifest manifest,
        CalibrationAnchorPlan plan,
        string fixturesRoot,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        FixtureManifestLoader.Validate(manifest);
        AnchorPlanLoader.Validate(plan);

        if (plan.MinimumWinnerMargin < manifest.Acceptance.MinimumWinnerMargin)
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidAnchorPlan,
                $"Anchor plan winner margin {plan.MinimumWinnerMargin:F6} is lower than the " +
                $"acceptance requirement {manifest.Acceptance.MinimumWinnerMargin:F6}.");
        }

        if (plan.States.Count == 0 || plan.States.Any(x => x.Anchors.Count == 0))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidAnchorPlan,
                "Every classified state requires at least one anchor before a profile can be built.");
        }

        var loaded = await FixtureRepository.LoadApprovedAsync(
            manifest,
            fixturesRoot,
            cancellationToken);
        var fixtures = loaded.ToDictionary(
            x => x.Definition.Id,
            StringComparer.OrdinalIgnoreCase);

        var states = new List<ScreenStateDefinition>();
        foreach (var statePlan in plan.States)
        {
            var anchors = new List<ScreenAnchorDefinition>();
            foreach (var anchorPlan in statePlan.Anchors)
            {
                var samples = new List<string>();
                var sampleHashes = new HashSet<string>(StringComparer.Ordinal);

                foreach (var fixtureId in anchorPlan.SampleFixtureIds)
                {
                    if (!fixtures.TryGetValue(fixtureId, out var fixture))
                    {
                        throw new CalibrationException(
                            CalibrationErrorCode.InvalidAnchorPlan,
                            $"Anchor '{statePlan.State}/{anchorPlan.Name}' references fixture " +
                            $"'{fixtureId}', but that fixture is missing or not fully approved.");
                    }

                    if (anchorPlan.Expectation != AnchorExpectation.Forbidden &&
                        fixture.Definition.ExpectedState != statePlan.State)
                    {
                        throw new CalibrationException(
                            CalibrationErrorCode.InvalidAnchorPlan,
                            $"Anchor '{statePlan.State}/{anchorPlan.Name}' uses fixture '{fixtureId}' " +
                            $"from state '{fixture.Definition.ExpectedState}'. Required and optional " +
                            "anchors must use fixtures from their own state.");
                    }

                    var fingerprint = FingerprintExtractor.Extract(
                        fixture.Image,
                        anchorPlan.Region,
                        anchorPlan.Mode,
                        anchorPlan.FingerprintWidth,
                        anchorPlan.FingerprintHeight);
                    var base64 = Convert.ToBase64String(fingerprint);
                    if (sampleHashes.Add(base64))
                    {
                        samples.Add(base64);
                    }
                }

                if (samples.Count == 0)
                {
                    throw new CalibrationException(
                        CalibrationErrorCode.InvalidAnchorPlan,
                        $"Anchor '{statePlan.State}/{anchorPlan.Name}' produced no unique samples.");
                }

                anchors.Add(new ScreenAnchorDefinition
                {
                    Name = anchorPlan.Name,
                    Region = anchorPlan.Region,
                    Mode = anchorPlan.Mode,
                    Expectation = anchorPlan.Expectation,
                    FingerprintWidth = anchorPlan.FingerprintWidth,
                    FingerprintHeight = anchorPlan.FingerprintHeight,
                    MatchThreshold = anchorPlan.MatchThreshold,
                    Weight = anchorPlan.Weight,
                    SamplesBase64 = samples
                });
            }

            states.Add(new ScreenStateDefinition
            {
                State = statePlan.State,
                Anchors = anchors
            });
        }

        var profile = new ScreenDetectionProfile
        {
            Name = manifest.ProfileName,
            RequiredOrientation = plan.RequiredOrientation,
            MinimumWidth = plan.MinimumWidth,
            MinimumHeight = plan.MinimumHeight,
            MinimumAspectRatio = plan.MinimumAspectRatio,
            MaximumAspectRatio = plan.MaximumAspectRatio,
            MinimumStateScore = plan.MinimumStateScore,
            MinimumWinnerMargin = plan.MinimumWinnerMargin,
            States = states
        };

        ScreenProfileLoader.Validate(profile);
        await AtomicFile.WriteTextAsync(
            outputPath,
            JsonSerializer.Serialize(
                profile,
                ScreenProfileLoader.CreateJsonOptions(writeIndented: true)),
            cancellationToken);

        return profile;
    }
}
