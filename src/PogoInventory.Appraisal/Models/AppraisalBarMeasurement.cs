using PogoInventory.Vision.Models;

namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalBarMeasurement
{
    public AppraisalBarKind Kind { get; init; }
    public required NormalizedRegion Region { get; init; }
    public bool TrackDetected { get; init; }
    public bool OrangeDetected { get; init; }
    public double FillFraction { get; init; }
    public int? EstimatedIv { get; init; }
    public double Confidence { get; init; }
    public double TrackWidthFraction { get; init; }
    public int TrackStartColumn { get; init; }
    public int TrackEndColumn { get; init; }
    public int FillEndColumn { get; init; }
    public int RegionPixelWidth { get; init; }
    public int RegionPixelHeight { get; init; }

    public void Validate()
    {
        Region.Validate(Kind.ToString());
        foreach (var value in new[]
        {
            FillFraction,
            Confidence,
            TrackWidthFraction
        })
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    "Appraisal measurement fractions must be between zero and one.");
            }
        }

        if (EstimatedIv is < 0 or > 15 ||
            RegionPixelWidth <= 0 ||
            RegionPixelHeight <= 0)
        {
            throw new InvalidOperationException(
                "Appraisal measurement values are invalid.");
        }
    }
}
