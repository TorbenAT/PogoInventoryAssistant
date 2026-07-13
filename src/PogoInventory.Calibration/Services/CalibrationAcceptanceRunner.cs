using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;

namespace PogoInventory.Calibration.Services;

public static class CalibrationAcceptanceRunner
{
    public static async Task<CalibrationAcceptanceReport> RunAsync(
        ScreenFixtureManifest manifest,
        ScreenDetectionProfile profile,
        string fixturesRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturesRoot);

        FixtureManifestLoader.Validate(manifest);
        ScreenProfileLoader.Validate(profile);

        var fixtures = await FixtureRepository.LoadApprovedAsync(
            manifest,
            fixturesRoot,
            cancellationToken);
        var detector = new ScreenStateDetector();
        var evaluations = new List<FixtureEvaluationContext>();

        foreach (var fixture in fixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detection = detector.Detect(fixture.Image, profile);
            var margin = CalculateWinnerMargin(detection);
            var outcome = ClassifyOutcome(
                fixture.Definition.ExpectedState,
                detection.State);

            evaluations.Add(new FixtureEvaluationContext(
                fixture,
                detection,
                new FixtureAcceptanceResult
                {
                    FixtureId = fixture.Definition.Id,
                    RelativePath = fixture.Definition.RelativePath,
                    ExpectedState = fixture.Definition.ExpectedState,
                    ActualState = detection.State,
                    Outcome = outcome,
                    Confidence = detection.Confidence,
                    WinnerMargin = margin,
                    Reasons = detection.Reasons
                }));
        }

        var fixtureResults = evaluations.Select(x => x.Result).ToArray();
        var stateMetrics = BuildStateMetrics(manifest, fixtureResults);
        var missingCoverage = stateMetrics
            .Where(x => x.ExpectedCount < x.MinimumApprovedFixtures)
            .Select(x =>
                $"{x.State}: approved fixtures {x.ExpectedCount}, required {x.MinimumApprovedFixtures}.")
            .ToArray();
        var anchorMetrics = BuildAnchorMetrics(
            profile,
            evaluations,
            manifest.Acceptance.MinimumAnchorSeparation);

        var falseNegatives = fixtureResults.Count(x => x.Outcome == FixtureOutcome.FalseNegative);
        var falsePositives = fixtureResults.Count(x => x.Outcome == FixtureOutcome.FalsePositive);
        var misclassifications = fixtureResults.Count(x => x.Outcome == FixtureOutcome.Misclassified);
        var weakAnchors = anchorMetrics.Count(x => x.Weak);
        var failures = new List<string>();

        if (profile.MinimumWinnerMargin < manifest.Acceptance.MinimumWinnerMargin)
        {
            failures.Add(
                $"Profile winner margin {profile.MinimumWinnerMargin:F6} is below required " +
                $"{manifest.Acceptance.MinimumWinnerMargin:F6}.");
        }

        if (falsePositives > manifest.Acceptance.MaximumFalsePositives)
        {
            failures.Add(
                $"False positives {falsePositives} exceed allowed " +
                $"{manifest.Acceptance.MaximumFalsePositives}.");
        }

        if (misclassifications > manifest.Acceptance.MaximumMisclassifications)
        {
            failures.Add(
                $"Misclassifications {misclassifications} exceed allowed " +
                $"{manifest.Acceptance.MaximumMisclassifications}.");
        }

        if (weakAnchors > manifest.Acceptance.MaximumWeakAnchors)
        {
            failures.Add(
                $"Weak anchors {weakAnchors} exceed allowed " +
                $"{manifest.Acceptance.MaximumWeakAnchors}.");
        }

        failures.AddRange(missingCoverage.Select(x => "Missing coverage: " + x));
        failures.AddRange(
            stateMetrics
                .Where(x => x.ExpectedCount >= x.MinimumApprovedFixtures && !x.Accepted)
                .Select(x =>
                    $"State {x.State} recall {x.Recall:F4} is below required {x.MinimumRecall:F4}."));

        return new CalibrationAcceptanceReport
        {
            ManifestName = manifest.Name,
            ProfileName = profile.Name,
            Accepted = failures.Count == 0,
            ApprovedFixtureCount = fixtureResults.Length,
            CorrectCount = fixtureResults.Count(x => x.Outcome == FixtureOutcome.Correct),
            FalseNegativeCount = falseNegatives,
            FalsePositiveCount = falsePositives,
            MisclassificationCount = misclassifications,
            WeakAnchorCount = weakAnchors,
            AcceptanceFailures = failures,
            MissingCoverage = missingCoverage,
            Fixtures = fixtureResults,
            States = stateMetrics,
            ConfusionMatrix = BuildConfusionMatrix(fixtureResults),
            Anchors = anchorMetrics,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static FixtureOutcome ClassifyOutcome(
        ScreenState expected,
        ScreenState actual)
    {
        if (expected == actual)
        {
            return FixtureOutcome.Correct;
        }

        if (expected == ScreenState.Unknown)
        {
            return FixtureOutcome.FalsePositive;
        }

        if (actual == ScreenState.Unknown)
        {
            return FixtureOutcome.FalseNegative;
        }

        return FixtureOutcome.Misclassified;
    }

    private static double? CalculateWinnerMargin(ScreenDetectionResult result)
    {
        var eligible = result.States
            .Where(x => x.Eligible)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.State)
            .ToArray();

        return eligible.Length switch
        {
            0 => null,
            1 => eligible[0].Score,
            _ => eligible[0].Score - eligible[1].Score
        };
    }

    private static IReadOnlyList<StateAcceptanceMetric> BuildStateMetrics(
        ScreenFixtureManifest manifest,
        IReadOnlyList<FixtureAcceptanceResult> fixtures)
    {
        var requirements = manifest.Acceptance.States.ToDictionary(x => x.State);
        var states = requirements.Keys
            .Concat(fixtures.Select(x => x.ExpectedState))
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var metrics = new List<StateAcceptanceMetric>();

        foreach (var state in states)
        {
            var expected = fixtures.Where(x => x.ExpectedState == state).ToArray();
            var correct = expected.Count(x => x.ActualState == state);
            var unknown = expected.Count(x => x.ActualState == ScreenState.Unknown && state != ScreenState.Unknown);
            var wrongKnown = expected.Count(x =>
                x.ActualState != state && x.ActualState != ScreenState.Unknown);
            var recall = expected.Length == 0 ? 0d : (double)correct / expected.Length;
            var requirement = requirements.TryGetValue(state, out var value)
                ? value
                : new StateAcceptanceRequirement
                {
                    State = state,
                    MinimumApprovedFixtures = 0,
                    MinimumRecall = 0
                };

            metrics.Add(new StateAcceptanceMetric
            {
                State = state,
                ExpectedCount = expected.Length,
                CorrectCount = correct,
                UnknownCount = unknown,
                WrongKnownStateCount = wrongKnown,
                Recall = recall,
                MinimumApprovedFixtures = requirement.MinimumApprovedFixtures,
                MinimumRecall = requirement.MinimumRecall,
                Accepted = expected.Length >= requirement.MinimumApprovedFixtures &&
                           recall >= requirement.MinimumRecall
            });
        }

        return metrics;
    }

    private static IReadOnlyList<ConfusionMatrixCell> BuildConfusionMatrix(
        IReadOnlyList<FixtureAcceptanceResult> fixtures) =>
        fixtures
            .GroupBy(x => new { x.ExpectedState, x.ActualState })
            .OrderBy(x => x.Key.ExpectedState)
            .ThenBy(x => x.Key.ActualState)
            .Select(group => new ConfusionMatrixCell
            {
                ExpectedState = group.Key.ExpectedState,
                ActualState = group.Key.ActualState,
                Count = group.Count()
            })
            .ToArray();

    private static IReadOnlyList<AnchorQualityMetric> BuildAnchorMetrics(
        ScreenDetectionProfile profile,
        IReadOnlyList<FixtureEvaluationContext> evaluations,
        double minimumSeparation)
    {
        var metrics = new List<AnchorQualityMetric>();
        foreach (var state in profile.States)
        {
            foreach (var anchor in state.Anchors)
            {
                var positive = FindAnchorEvidence(evaluations, state.State, anchor.Name, expectedSame: true);
                var negative = FindAnchorEvidence(evaluations, state.State, anchor.Name, expectedSame: false);
                var reasons = new List<string>();
                var positiveFailures = positive.Count(x => !x.ConditionSatisfied);
                var negativeMatched = negative.Count(x => x.Matched);
                double? positiveMin = positive.Count == 0 ? null : positive.Min(x => x.Similarity);
                double? positiveAverage = positive.Count == 0 ? null : positive.Average(x => x.Similarity);
                double? positiveMax = positive.Count == 0 ? null : positive.Max(x => x.Similarity);
                double? negativeMax = negative.Count == 0 ? null : negative.Max(x => x.Similarity);
                double? separation = null;

                if (positive.Count == 0)
                {
                    reasons.Add("NoPositiveCoverage");
                }

                if (positiveFailures > 0)
                {
                    reasons.Add("ExpectedFixtureFailedCondition");
                }

                if (anchor.Expectation == AnchorExpectation.Forbidden)
                {
                    if (positiveMax is not null && positiveMax >= anchor.MatchThreshold)
                    {
                        reasons.Add("ForbiddenAnchorMatchedExpectedState");
                    }

                    if (negativeMatched == 0)
                    {
                        reasons.Add("NoForbiddenTriggerCoverage");
                    }
                }
                else
                {
                    if (positiveMin is not null && positiveMin < anchor.MatchThreshold)
                    {
                        reasons.Add("PositiveSimilarityBelowThreshold");
                    }

                    if (positiveMin is not null && negativeMax is not null)
                    {
                        separation = positiveMin.Value - negativeMax.Value;
                        if (separation < minimumSeparation)
                        {
                            reasons.Add("LowPositiveNegativeSeparation");
                        }
                    }
                    else if (negative.Count == 0)
                    {
                        reasons.Add("NoNegativeCoverage");
                    }
                }

                metrics.Add(new AnchorQualityMetric
                {
                    State = state.State,
                    AnchorName = anchor.Name,
                    Expectation = anchor.Expectation,
                    MatchThreshold = anchor.MatchThreshold,
                    PositiveFixtureCount = positive.Count,
                    NegativeFixtureCount = negative.Count,
                    PositiveMinimumSimilarity = positiveMin,
                    PositiveAverageSimilarity = positiveAverage,
                    PositiveMaximumSimilarity = positiveMax,
                    NegativeMaximumSimilarity = negativeMax,
                    Separation = separation,
                    PositiveConditionFailureCount = positiveFailures,
                    NegativeMatchedCount = negativeMatched,
                    Weak = reasons.Count > 0,
                    WeakReasons = reasons
                });
            }
        }

        return metrics;
    }

    private static IReadOnlyList<AnchorEvidence> FindAnchorEvidence(
        IReadOnlyList<FixtureEvaluationContext> evaluations,
        ScreenState state,
        string anchorName,
        bool expectedSame)
    {
        var result = new List<AnchorEvidence>();
        foreach (var evaluation in evaluations)
        {
            var isSame = evaluation.Fixture.Definition.ExpectedState == state;
            if (isSame != expectedSame)
            {
                continue;
            }

            if (!expectedSame && IsCompositeNegativeFixture(evaluation.Fixture.Definition))
            {
                continue;
            }

            var stateEvidence = evaluation.Detection.States.FirstOrDefault(x => x.State == state);
            var anchor = stateEvidence?.Anchors.FirstOrDefault(x =>
                x.Name.Equals(anchorName, StringComparison.OrdinalIgnoreCase));
            if (anchor is not null)
            {
                result.Add(anchor);
            }
        }

        return result;
    }

    private static bool IsCompositeNegativeFixture(ScreenFixtureDefinition fixture) =>
        fixture.ExpectedState == ScreenState.Unknown &&
        fixture.Tags.Any(tag =>
            tag.Equals("mixed-state", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("partial-state", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("unsupported-layout", StringComparison.OrdinalIgnoreCase));

    private sealed record FixtureEvaluationContext(
        LoadedScreenFixture Fixture,
        ScreenDetectionResult Detection,
        FixtureAcceptanceResult Result);
}
