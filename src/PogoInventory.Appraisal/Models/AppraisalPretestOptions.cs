namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalPretestOptions
{
    public int MinimumDecodedImages { get; init; } = 20;
    public int MinimumCandidateImages { get; init; } = 5;
    public double MinimumDominantClusterShare { get; init; } = 0.70;

    public void Validate()
    {
        if (MinimumDecodedImages <= 0 ||
            MinimumCandidateImages <= 0 ||
            !double.IsFinite(MinimumDominantClusterShare) ||
            MinimumDominantClusterShare is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Appraisal pretest thresholds are invalid.");
        }
    }
}
