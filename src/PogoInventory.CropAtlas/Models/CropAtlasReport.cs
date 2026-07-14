namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string InputDirectory { get; init; }
    public required string RegionReportPath { get; init; }
    public int SourceImageCount { get; init; }
    public int ClusterCount { get; init; }
    public int SelectedRegionCount { get; init; }
    public int CropCount { get; init; }
    public required string OverviewFile { get; init; }
    public bool Accepted { get; init; }
    public required string GateDetail { get; init; }
    public required CropAtlasReadiness Readiness { get; init; }
    public IReadOnlyList<CropAtlasCluster> Clusters { get; init; } =
        Array.Empty<CropAtlasCluster>();
    public IReadOnlyList<CropAtlasSelectedRegion> SelectedRegions { get; init; } =
        Array.Empty<CropAtlasSelectedRegion>();
    public IReadOnlyList<CropAtlasCrop> Crops { get; init; } =
        Array.Empty<CropAtlasCrop>();

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported crop atlas schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(InputDirectory) ||
            string.IsNullOrWhiteSpace(RegionReportPath) ||
            string.IsNullOrWhiteSpace(OverviewFile) ||
            string.IsNullOrWhiteSpace(GateDetail) ||
            SourceImageCount <= 0 ||
            ClusterCount != Clusters.Count ||
            SelectedRegionCount != SelectedRegions.Count ||
            CropCount != Crops.Count)
        {
            throw new InvalidOperationException(
                "Crop atlas report counts or paths are inconsistent.");
        }

        foreach (var cluster in Clusters)
        {
            cluster.Validate();
        }

        foreach (var region in SelectedRegions)
        {
            region.Validate();
        }

        foreach (var crop in Crops)
        {
            crop.Validate();
        }
    }
}
