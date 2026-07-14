namespace PogoInventory.RegionDiscovery.Models;

public sealed record RegionDiscoveryOptions
{
    public int MinimumDecodedImages { get; init; } = 20;
    public double MinimumDecodeRate { get; init; } = 0.90;
    public double NearDuplicateThreshold { get; init; } = 0.995;
    public double ClusterThreshold { get; init; } = 0.925;
    public int GridColumns { get; init; } = 12;
    public int GridRows { get; init; } = 24;
    public int CellFingerprintWidth { get; init; } = 6;
    public int CellFingerprintHeight { get; init; } = 6;
    public int MaximumCandidatesPerKind { get; init; } = 6;
    public double MinimumCandidateScore { get; init; } = 0.08;
    public double CandidatePercentile { get; init; } = 0.85;
    public double CandidateExpansionRatio { get; init; } = 0.72;

    public void Validate()
    {
        if (MinimumDecodedImages < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumDecodedImages),
                "At least two decoded images are required.");
        }

        if (!double.IsFinite(MinimumDecodeRate) ||
            MinimumDecodeRate is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumDecodeRate),
                "Minimum decode rate must be greater than zero and at most one.");
        }


        ValidateUnitInterval(
            NearDuplicateThreshold,
            nameof(NearDuplicateThreshold),
            allowZero: true);
        ValidateUnitInterval(
            ClusterThreshold,
            nameof(ClusterThreshold),
            allowZero: true);
        if (ClusterThreshold >= NearDuplicateThreshold)
        {
            throw new ArgumentException(
                "Cluster threshold must be lower than the near-duplicate threshold.");
        }

        if (GridColumns is < 2 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GridColumns),
                "Grid columns must be between 2 and 64.");
        }

        if (GridRows is < 2 or > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GridRows),
                "Grid rows must be between 2 and 128.");
        }

        if (CellFingerprintWidth is < 2 or > 32 ||
            CellFingerprintHeight is < 2 or > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CellFingerprintWidth),
                "Cell fingerprint dimensions must be between 2 and 32.");
        }

        if (MaximumCandidatesPerKind is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumCandidatesPerKind),
                "Maximum candidates per kind must be between 1 and 50.");
        }

        ValidateUnitInterval(
            MinimumCandidateScore,
            nameof(MinimumCandidateScore),
            allowZero: false);
        ValidateUnitInterval(
            CandidatePercentile,
            nameof(CandidatePercentile),
            allowZero: false);
        ValidateUnitInterval(
            CandidateExpansionRatio,
            nameof(CandidateExpansionRatio),
            allowZero: false);
    }

    private static void ValidateUnitInterval(
        double value,
        string parameterName,
        bool allowZero)
    {
        var invalid = !double.IsFinite(value) ||
            (allowZero ? value < 0 : value <= 0) ||
            value > 1;
        if (invalid)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be inside the unit interval.");
        }
    }
}
