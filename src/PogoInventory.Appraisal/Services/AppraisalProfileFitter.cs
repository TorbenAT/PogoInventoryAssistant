using PogoInventory.Appraisal.Models;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalProfileFitter
{
    public static AppraisalVisualProfile CreateDeviceProfile(
        AppraisalVisualProfile source,
        AppraisalAnalysisResult analysis,
        string screenshotSha256,
        int screenWidth,
        int screenHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(analysis);
        source.Validate();
        analysis.Validate();

        if (!analysis.IsAppraisal)
        {
            throw new InvalidOperationException(
                "A device profile can be generated only from an appraisal candidate.");
        }

        if (screenshotSha256.Length != 64 ||
            screenshotSha256.Any(character => !Uri.IsHexDigit(character)) ||
            screenWidth <= 0 ||
            screenHeight <= 0)
        {
            throw new InvalidOperationException(
                "Device profile source evidence is invalid.");
        }

        var bars = source.Bars
            .OrderBy(item => item.Kind)
            .Select(item => new AppraisalBarDefinition
            {
                Kind = item.Kind,
                Region = AppraisalAnalyzer.TransformRegion(
                    item.Region,
                    analysis.Transform)
            })
            .ToArray();

        var profile = source with
        {
            ProfileId =
                $"{source.ProfileId}-device-{screenWidth}x{screenHeight}",
            ReferencePlatform = "AndroidDeviceAdjusted",
            ReferenceAspectRatio = screenWidth / (double)screenHeight,
            Bars = bars,
            SearchXOffsets = new[] { -0.02, -0.01, 0d, 0.01, 0.02 },
            SearchYOffsets = new[] { -0.03, -0.015, 0d, 0.015, 0.03 },
            SearchScales = new[] { 0.97, 1d, 1.03 },
            Verified = false,
            VerificationReportSha256 = null,
            VerifiedAtUtc = null,
            SourceScreenshotSha256 = screenshotSha256.ToLowerInvariant(),
            SourceScreenWidth = screenWidth,
            SourceScreenHeight = screenHeight
        };
        profile.Validate();
        return profile;
    }
}
