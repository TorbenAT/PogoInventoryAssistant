namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalColorProfile
{
    public byte OrangeRedMinimum { get; init; } = 180;
    public byte OrangeGreenMinimum { get; init; } = 55;
    public byte OrangeGreenMaximum { get; init; } = 220;
    public byte OrangeBlueMaximum { get; init; } = 175;
    public byte OrangeRedGreenDeltaMinimum { get; init; } = 20;
    public byte OrangeGreenBlueDeltaMinimum { get; init; } = 15;

    // Full 15-IV segments use a pink/red fill on current Android appraisal screens.
    public byte FullBarRedMinimum { get; init; } = 180;
    public byte FullBarGreenMinimum { get; init; } = 65;
    public byte FullBarGreenMaximum { get; init; } = 190;
    public byte FullBarBlueMinimum { get; init; } = 65;
    public byte FullBarBlueMaximum { get; init; } = 190;
    public byte FullBarRedGreenDeltaMinimum { get; init; } = 35;
    public byte FullBarGreenBlueDeltaMaximum { get; init; } = 40;

    public byte TrackChannelMinimum { get; init; } = 125;
    public byte TrackChannelMaximum { get; init; } = 245;
    public byte TrackMaximumChannelSpread { get; init; } = 34;

    public double MinimumColumnCoverage { get; init; } = 0.12;
    public double MinimumTrackWidthFraction { get; init; } = 0.42;
    public double MaximumTrackGapFraction { get; init; } = 0.08;

    public void Validate()
    {
        if (OrangeGreenMinimum > OrangeGreenMaximum ||
            FullBarGreenMinimum > FullBarGreenMaximum ||
            FullBarBlueMinimum > FullBarBlueMaximum ||
            TrackChannelMinimum > TrackChannelMaximum)
        {
            throw new InvalidOperationException(
                "Appraisal colour ranges are inverted.");
        }

        foreach (var value in new[]
        {
            MinimumColumnCoverage,
            MinimumTrackWidthFraction,
            MaximumTrackGapFraction
        })
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    "Appraisal colour fractions must be between zero and one.");
            }
        }
    }
}
