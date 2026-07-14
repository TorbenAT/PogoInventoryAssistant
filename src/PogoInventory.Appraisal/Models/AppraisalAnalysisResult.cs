namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalAnalysisResult
{
    public AppraisalAnalysisStatus Status { get; init; }
    public double Confidence { get; init; }
    public double CandidateScore { get; init; }
    public required AppraisalLayoutTransform Transform { get; init; }
    public IReadOnlyList<AppraisalBarMeasurement> Bars { get; init; } =
        Array.Empty<AppraisalBarMeasurement>();
    public required string Detail { get; init; }

    public bool IsAppraisal =>
        Status is AppraisalAnalysisStatus.Candidate or
        AppraisalAnalysisStatus.Complete;

    public bool IsComplete => Status == AppraisalAnalysisStatus.Complete;

    public int? AttackIv => Value(AppraisalBarKind.Attack);
    public int? DefenseIv => Value(AppraisalBarKind.Defense);
    public int? HpIv => Value(AppraisalBarKind.Hp);

    public void Validate()
    {
        Transform.Validate();
        if (!double.IsFinite(Confidence) ||
            Confidence is < 0 or > 1 ||
            !double.IsFinite(CandidateScore) ||
            CandidateScore is < 0 or > 2 ||
            string.IsNullOrWhiteSpace(Detail))
        {
            throw new InvalidOperationException(
                "Appraisal analysis result is invalid.");
        }

        if (Bars.Count != 3 ||
            Bars.Select(item => item.Kind).Distinct().Count() != 3)
        {
            throw new InvalidOperationException(
                "Appraisal analysis result must contain exactly three bars.");
        }

        foreach (var bar in Bars)
        {
            bar.Validate();
        }

        if (Status == AppraisalAnalysisStatus.Complete &&
            Bars.Any(item => item.EstimatedIv is null))
        {
            throw new InvalidOperationException(
                "Complete appraisal analysis requires all three IV values.");
        }
    }

    private int? Value(AppraisalBarKind kind) =>
        Bars.Single(item => item.Kind == kind).EstimatedIv;
}
