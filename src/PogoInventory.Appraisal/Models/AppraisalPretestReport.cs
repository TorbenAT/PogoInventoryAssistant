namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalPretestReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string InputDirectory { get; init; }
    public required string ProfilePath { get; init; }
    public string? RegionReportPath { get; init; }
    public int ImageCount { get; init; }
    public int DecodedCount { get; init; }
    public int CandidateCount { get; init; }
    public int CompleteCount { get; init; }
    public int DistinctCandidateClusterCount { get; init; }
    public string? DominantCandidateCluster { get; init; }
    public double DominantCandidateClusterShare { get; init; }
    public bool Accepted { get; init; }
    public required string GateDetail { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<AppraisalImageResult> Images { get; init; } =
        Array.Empty<AppraisalImageResult>();

    public void Validate()
    {
        if (SchemaVersion != "1.0" ||
            string.IsNullOrWhiteSpace(InputDirectory) ||
            string.IsNullOrWhiteSpace(ProfilePath) ||
            string.IsNullOrWhiteSpace(GateDetail) ||
            ImageCount != Images.Count ||
            DecodedCount != Images.Count(item => item.Decoded) ||
            CandidateCount != Images.Count(item => item.Analysis?.IsAppraisal == true) ||
            CompleteCount != Images.Count(item => item.Analysis?.IsComplete == true) ||
            !double.IsFinite(DominantCandidateClusterShare) ||
            DominantCandidateClusterShare is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Appraisal pretest report is inconsistent.");
        }
    }
}
