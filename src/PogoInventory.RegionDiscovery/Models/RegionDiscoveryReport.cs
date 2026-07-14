namespace PogoInventory.RegionDiscovery.Models;

public sealed record RegionDiscoveryReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string InputDirectory { get; init; }
    public int ImageCount { get; init; }
    public int DecodedCount { get; init; }
    public int FailedCount { get; init; }
    public double DecodeRate { get; init; }
    public int GeometryGroupCount { get; init; }
    public int ClusterCount { get; init; }
    public int GridColumns { get; init; }
    public int GridRows { get; init; }
    public int CellCount { get; init; }
    public bool Accepted { get; init; }
    public required string GateDetail { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RegionDiscoveryImage> Images { get; init; } =
        Array.Empty<RegionDiscoveryImage>();
    public IReadOnlyList<RegionDiscoveryCell> Cells { get; init; } =
        Array.Empty<RegionDiscoveryCell>();
    public IReadOnlyList<RegionDiscoveryCandidate> Candidates { get; init; } =
        Array.Empty<RegionDiscoveryCandidate>();

    public int CandidateCount(RegionCandidateKind kind) =>
        Candidates.Count(candidate => candidate.Kind == kind);

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported region discovery schema '{SchemaVersion}'.");
        }

        if (ImageCount < 0 || DecodedCount < 0 || FailedCount < 0 ||
            DecodedCount + FailedCount != ImageCount ||
            !double.IsFinite(DecodeRate) || DecodeRate is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Region discovery image counts are inconsistent.");
        }

        if (GridColumns <= 0 || GridRows <= 0 ||
            CellCount != GridColumns * GridRows ||
            CellCount != Cells.Count)
        {
            throw new InvalidOperationException(
                "Region discovery grid counts are inconsistent.");
        }

        if (Images.Count != DecodedCount)
        {
            throw new InvalidOperationException(
                "Region discovery decoded image count is inconsistent.");
        }

        foreach (var cell in Cells)
        {
            cell.Validate();
        }

        foreach (var candidate in Candidates)
        {
            candidate.Validate();
        }
    }
}
