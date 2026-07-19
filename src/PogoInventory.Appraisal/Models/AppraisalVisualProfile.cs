namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalVisualProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ProfileId { get; init; }
    public string ReferencePlatform { get; init; } = "CrossPlatformNormalized";
    public double ReferenceAspectRatio { get; init; } = 0.4613;

    public IReadOnlyList<AppraisalBarDefinition> Bars { get; init; } =
        Array.Empty<AppraisalBarDefinition>();

    public IReadOnlyList<double> SearchXOffsets { get; init; } =
        Array.Empty<double>();
    public IReadOnlyList<double> SearchYOffsets { get; init; } =
        Array.Empty<double>();
    public IReadOnlyList<double> SearchScales { get; init; } =
        Array.Empty<double>();

    public required AppraisalColorProfile Colors { get; init; }
    public double CandidateScoreMinimum { get; init; } = 0.82;
    public int MinimumOrangeBars { get; init; } = 2;
    public int MinimumTrackBars { get; init; } = 2;
    public double TrackOnlyCandidateScoreFactor { get; init; } = 0.75;
    public double CompleteBarConfidenceMinimum { get; init; } = 0.72;

    public bool Verified { get; init; }
    public string? VerificationReportSha256 { get; init; }
    public DateTimeOffset? VerifiedAtUtc { get; init; }

    public string? SourceScreenshotSha256 { get; init; }
    public int? SourceScreenWidth { get; init; }
    public int? SourceScreenHeight { get; init; }

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported appraisal profile schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(ProfileId) ||
            string.IsNullOrWhiteSpace(ReferencePlatform) ||
            !double.IsFinite(ReferenceAspectRatio) ||
            ReferenceAspectRatio <= 0)
        {
            throw new InvalidOperationException(
                "Appraisal profile identity or reference geometry is invalid.");
        }

        if (Bars.Count != 3 ||
            Bars.Select(item => item.Kind).Distinct().Count() != 3 ||
            Enum.GetValues<AppraisalBarKind>().Any(kind =>
                Bars.All(item => item.Kind != kind)))
        {
            throw new InvalidOperationException(
                "Appraisal profile must contain exactly one definition for each IV bar.");
        }

        foreach (var bar in Bars)
        {
            bar.Validate();
        }

        ValidateSearchValues(SearchXOffsets, "X offset", allowZero: true);
        ValidateSearchValues(SearchYOffsets, "Y offset", allowZero: true);
        ValidateSearchValues(SearchScales, "scale", allowZero: false);

        if (!SearchXOffsets.Contains(0d) ||
            !SearchYOffsets.Contains(0d) ||
            !SearchScales.Contains(1d))
        {
            throw new InvalidOperationException(
                "Appraisal profile search values must include the identity transform.");
        }

        Colors.Validate();

        if (!double.IsFinite(CandidateScoreMinimum) ||
            CandidateScoreMinimum is < 0 or > 2 ||
            MinimumOrangeBars is < 0 or > 3 ||
            MinimumTrackBars is < 0 or > 3 ||
            !double.IsFinite(TrackOnlyCandidateScoreFactor) ||
            TrackOnlyCandidateScoreFactor is < 0 or > 1 ||
            !double.IsFinite(CompleteBarConfidenceMinimum) ||
            CompleteBarConfidenceMinimum is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Appraisal profile decision thresholds are invalid.");
        }

        if (Verified)
        {
            if (VerificationReportSha256 is null ||
                VerificationReportSha256.Length != 64 ||
                VerificationReportSha256.Any(character =>
                    !Uri.IsHexDigit(character)) ||
                VerifiedAtUtc is null)
            {
                throw new InvalidOperationException(
                    "A verified appraisal profile requires a verification hash and timestamp.");
            }
        }
        else if (VerificationReportSha256 is not null ||
                 VerifiedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "An unverified appraisal profile cannot contain verification metadata.");
        }

        if (SourceScreenshotSha256 is not null &&
            (SourceScreenshotSha256.Length != 64 ||
             SourceScreenshotSha256.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidOperationException(
                "Appraisal profile source screenshot hash is invalid.");
        }

        if ((SourceScreenWidth is null) != (SourceScreenHeight is null) ||
            (SourceScreenWidth is not null &&
             SourceScreenWidth.Value <= 0) ||
            (SourceScreenHeight is not null &&
             SourceScreenHeight.Value <= 0))
        {
            throw new InvalidOperationException(
                "Appraisal profile source screen dimensions must be positive and supplied together.");
        }
    }

    private static void ValidateSearchValues(
        IReadOnlyList<double> values,
        string name,
        bool allowZero)
    {
        if (values.Count == 0 ||
            values.Any(value =>
                !double.IsFinite(value) ||
                (!allowZero && value <= 0)))
        {
            throw new InvalidOperationException(
                $"Appraisal profile {name} search values are invalid.");
        }
    }
}
