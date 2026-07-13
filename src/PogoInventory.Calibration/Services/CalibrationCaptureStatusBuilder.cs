using PogoInventory.Calibration.Models;

namespace PogoInventory.Calibration.Services;

public static class CalibrationCaptureStatusBuilder
{
    public static CalibrationCaptureStatus Build(
        CalibrationCapturePlan plan,
        CalibrationCaptureSession session)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(session);
        CalibrationCapturePlanLoader.Validate(plan);
        CalibrationCaptureSessionRepository.Validate(session, plan);

        var progress = plan.Requirements.Select(requirement =>
        {
            var stateCaptures = session.Captures
                .Where(x => x.ExpectedState == requirement.State)
                .ToArray();
            var unique = stateCaptures.Count(x => !x.IsDuplicate);
            return new CalibrationCaptureStateProgress
            {
                State = requirement.State,
                RequiredUniqueCaptures = requirement.RequiredUniqueCaptures,
                UniqueCaptureCount = unique,
                DuplicateCaptureCount = stateCaptures.Count(x => x.IsDuplicate),
                PromotedCaptureCount = stateCaptures.Count(x => x.IsPromoted),
                OptionalWhenUnavailable = requirement.OptionalWhenUnavailable,
                Instruction = requirement.Instruction,
                VariationHints = requirement.VariationHints
            };
        }).ToArray();

        var requiredComplete = progress
            .Where(x => !x.OptionalWhenUnavailable)
            .All(x => x.Complete);
        var next = progress.FirstOrDefault(x => !x.Complete && !x.OptionalWhenUnavailable)?.State
            ?? progress.FirstOrDefault(x => !x.Complete)?.State;
        var warnings = new List<string>();

        if (session.Captures.Count > 0 &&
            (string.IsNullOrWhiteSpace(session.LockedDeviceSerial) ||
             session.LockedImageWidth is null ||
             session.LockedImageHeight is null))
        {
            warnings.Add("The capture session contains images but has no complete device and geometry lock.");
        }

        if (session.Captures.Any(x => x.IsDuplicate))
        {
            warnings.Add("Duplicate pixel-identical captures do not count toward required variation coverage.");
        }

        if (progress.Any(x => x.UniqueCaptureCount > 0 && x.PromotedCaptureCount == 0))
        {
            warnings.Add("Captured images remain private incoming files until explicitly reviewed and promoted.");
        }

        return new CalibrationCaptureStatus
        {
            PlanName = plan.Name,
            SessionId = session.Id,
            TotalCaptureCount = session.Captures.Count,
            UniqueCaptureCount = session.Captures.Count(x => !x.IsDuplicate),
            DuplicateCaptureCount = session.Captures.Count(x => x.IsDuplicate),
            PromotedCaptureCount = session.Captures.Count(x => x.IsPromoted),
            RequiredCoverageComplete = requiredComplete,
            NextRecommendedState = next,
            States = progress,
            Warnings = warnings
        };
    }
}
