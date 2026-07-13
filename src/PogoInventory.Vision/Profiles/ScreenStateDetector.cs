using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Vision.Profiles;

public sealed class ScreenStateDetector
{
    public ScreenDetectionResult Detect(
        PixelImage image,
        ScreenDetectionProfile profile,
        DateTimeOffset? analysedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(profile);
        ScreenProfileLoader.Validate(profile);

        var orientation = GetOrientation(image.Width, image.Height);
        var aspectRatio = (double)image.Width / image.Height;
        var geometryReasons = ValidateGeometry(
            image,
            profile,
            orientation,
            aspectRatio);

        if (geometryReasons.Count > 0)
        {
            return new ScreenDetectionResult
            {
                ProfileName = profile.Name,
                State = ScreenState.Unknown,
                Confidence = 0,
                ImageWidth = image.Width,
                ImageHeight = image.Height,
                Orientation = orientation,
                AspectRatio = aspectRatio,
                Reasons = geometryReasons,
                States = Array.Empty<StateEvidence>(),
                AnalysedAtUtc = analysedAtUtc ?? DateTimeOffset.UtcNow
            };
        }

        var stateEvidence = profile.States
            .Select(state => EvaluateState(image, state))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.State)
            .ToArray();

        var candidates = stateEvidence
            .Where(x => x.Eligible && x.Score >= profile.MinimumStateScore)
            .ToArray();

        var reasons = new List<string>();
        var selectedState = ScreenState.Unknown;
        var confidence = candidates.Length > 0 ? candidates[0].Score : 0;

        if (candidates.Length == 0)
        {
            reasons.Add("NoStatePassedRequiredAnchorsAndMinimumScore");
        }
        else if (candidates.Length > 1 &&
                 candidates[0].Score - candidates[1].Score < profile.MinimumWinnerMargin)
        {
            reasons.Add("ConflictingStateEvidence");
            reasons.Add(
                $"WinnerMargin={candidates[0].Score - candidates[1].Score:F6};" +
                $"Required={profile.MinimumWinnerMargin:F6}");
        }
        else
        {
            selectedState = candidates[0].State;
            reasons.Add("StateClassified");
        }

        return new ScreenDetectionResult
        {
            ProfileName = profile.Name,
            State = selectedState,
            Confidence = confidence,
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            Orientation = orientation,
            AspectRatio = aspectRatio,
            Reasons = reasons,
            States = stateEvidence,
            AnalysedAtUtc = analysedAtUtc ?? DateTimeOffset.UtcNow
        };
    }

    public static ScreenOrientation GetOrientation(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Image dimensions must be positive.");
        }

        if (width == height)
        {
            return ScreenOrientation.Square;
        }

        return width < height
            ? ScreenOrientation.Portrait
            : ScreenOrientation.Landscape;
    }

    private static StateEvidence EvaluateState(
        PixelImage image,
        ScreenStateDefinition state)
    {
        var anchors = new List<AnchorEvidence>(state.Anchors.Count);
        var rejectionReasons = new List<string>();
        var weightedScore = 0d;
        var totalWeight = 0d;

        foreach (var anchor in state.Anchors)
        {
            var actual = FingerprintExtractor.Extract(
                image,
                anchor.Region,
                anchor.Mode,
                anchor.FingerprintWidth,
                anchor.FingerprintHeight);
            var samples = ScreenProfileLoader.DecodeSamples(anchor);

            var bestSimilarity = double.NegativeInfinity;
            int? bestSampleIndex = null;
            for (var index = 0; index < samples.Count; index++)
            {
                var similarity = FingerprintComparer.Similarity(actual, samples[index]);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestSampleIndex = index;
                }
            }

            var matched = bestSimilarity >= anchor.MatchThreshold;
            var conditionSatisfied = anchor.Expectation switch
            {
                AnchorExpectation.Required => matched,
                AnchorExpectation.Optional => true,
                AnchorExpectation.Forbidden => !matched,
                _ => false
            };
            if (anchor.Expectation != AnchorExpectation.Forbidden)
            {
                weightedScore += bestSimilarity * anchor.Weight;
                totalWeight += anchor.Weight;
            }

            if (!conditionSatisfied)
            {
                rejectionReasons.Add(
                    anchor.Expectation == AnchorExpectation.Forbidden
                        ? $"ForbiddenAnchorMatched:{anchor.Name}"
                        : $"RequiredAnchorMissing:{anchor.Name}");
            }

            anchors.Add(new AnchorEvidence
            {
                Name = anchor.Name,
                Expectation = anchor.Expectation,
                Mode = anchor.Mode,
                Region = anchor.Region,
                Similarity = bestSimilarity,
                MatchThreshold = anchor.MatchThreshold,
                Matched = matched,
                ConditionSatisfied = conditionSatisfied,
                Weight = anchor.Weight,
                BestSampleIndex = bestSampleIndex
            });
        }

        return new StateEvidence
        {
            State = state.State,
            Score = totalWeight == 0 ? 0 : weightedScore / totalWeight,
            Eligible = rejectionReasons.Count == 0,
            RejectionReasons = rejectionReasons,
            Anchors = anchors
        };
    }

    private static IReadOnlyList<string> ValidateGeometry(
        PixelImage image,
        ScreenDetectionProfile profile,
        ScreenOrientation orientation,
        double aspectRatio)
    {
        var reasons = new List<string>();

        if (profile.RequiredOrientation != ScreenOrientation.Any &&
            orientation != profile.RequiredOrientation)
        {
            reasons.Add(
                $"UnsupportedOrientation:Expected={profile.RequiredOrientation};Actual={orientation}");
        }

        if (image.Width < profile.MinimumWidth || image.Height < profile.MinimumHeight)
        {
            reasons.Add(
                $"UnsupportedResolution:Minimum={profile.MinimumWidth}x{profile.MinimumHeight};" +
                $"Actual={image.Width}x{image.Height}");
        }

        if (aspectRatio < profile.MinimumAspectRatio ||
            aspectRatio > profile.MaximumAspectRatio)
        {
            reasons.Add(
                $"UnsupportedAspectRatio:Allowed={profile.MinimumAspectRatio:F6}-" +
                $"{profile.MaximumAspectRatio:F6};Actual={aspectRatio:F6}");
        }

        return reasons;
    }
}
