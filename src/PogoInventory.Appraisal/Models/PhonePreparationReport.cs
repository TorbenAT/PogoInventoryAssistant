using PogoInventory.Device.Models;

namespace PogoInventory.Appraisal.Models;

public sealed record PhonePreparationReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required AndroidDeviceMetadata Device { get; init; }
    public required string ScreenshotFile { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public int ScreenshotWidth { get; init; }
    public int ScreenshotHeight { get; init; }
    public bool Portrait { get; init; }
    public required string BaseProfileId { get; init; }
    public double ScreenshotAspectRatio { get; init; }
    public double ReferenceAspectRatio { get; init; }
    public double AspectRatioDifference { get; init; }
    public required AppraisalAnalysisResult Appraisal { get; init; }
    public string? GeneratedProfileFile { get; init; }
    public bool AdbReady { get; init; }
    public bool PassiveCaptureReady { get; init; }
    public bool AppraisalCalibrationReady { get; init; }
    public bool VerifiedIvExtractionReady { get; init; }
    public bool AutomaticNavigationReady { get; init; }
    public IReadOnlyList<string> NextActions { get; init; } =
        Array.Empty<string>();

    public void Validate()
    {
        if (SchemaVersion != "1.0" ||
            string.IsNullOrWhiteSpace(ScreenshotFile) ||
            ScreenshotSha256.Length != 64 ||
            ScreenshotWidth <= 0 ||
            ScreenshotHeight <= 0 ||
            string.IsNullOrWhiteSpace(BaseProfileId) ||
            !double.IsFinite(ScreenshotAspectRatio) ||
            ScreenshotAspectRatio <= 0 ||
            !double.IsFinite(ReferenceAspectRatio) ||
            ReferenceAspectRatio <= 0 ||
            !double.IsFinite(AspectRatioDifference) ||
            AspectRatioDifference < 0 ||
            NextActions.Count == 0)
        {
            throw new InvalidOperationException(
                "Phone preparation report is invalid.");
        }

        Appraisal.Validate();

        if (AutomaticNavigationReady)
        {
            throw new InvalidOperationException(
                "Version 0.14.3 cannot mark automatic navigation ready.");
        }
    }
}
