namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasOptions
{
    public int MaximumCandidates { get; init; } = 8;
    public int RepresentativesPerCluster { get; init; } = 2;
    public int MaximumCropWidth { get; init; } = 640;
    public int MaximumCropHeight { get; init; } = 480;
    public int OverviewThumbnailWidth { get; init; } = 220;
    public int OverviewThumbnailHeight { get; init; } = 480;
    public double MaximumSameKindOverlap { get; init; } = 0.35;

    public void Validate()
    {
        if (MaximumCandidates < 3)
        {
            throw new InvalidOperationException(
                "The crop atlas requires at least three candidate regions.");
        }

        if (RepresentativesPerCluster <= 0 ||
            MaximumCropWidth <= 0 ||
            MaximumCropHeight <= 0 ||
            OverviewThumbnailWidth <= 0 ||
            OverviewThumbnailHeight <= 0)
        {
            throw new InvalidOperationException(
                "Crop atlas dimensions and representative counts must be positive.");
        }

        if (!double.IsFinite(MaximumSameKindOverlap) ||
            MaximumSameKindOverlap is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Crop atlas overlap must be between zero and one.");
        }
    }
}
