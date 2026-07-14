namespace PogoInventory.ImagePretest.Models;

public sealed record ImagePretestReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string InputDirectory { get; init; }
    public int MinimumImageCount { get; init; }
    public int ImageCount { get; init; }
    public int DecodedCount { get; init; }
    public int FailedCount { get; init; }
    public int PortraitCount { get; init; }
    public int LandscapeCount { get; init; }
    public int GeometryGroupCount { get; init; }
    public int DistinctFileHashCount { get; init; }
    public int ExactDuplicatePairCount { get; init; }
    public int NearDuplicatePairCount { get; init; }
    public int ClusterCount { get; init; }
    public bool Accepted { get; init; }
    public required string GateDetail { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ImagePretestItem> Images { get; init; } =
        Array.Empty<ImagePretestItem>();
    public IReadOnlyList<ImageSimilarityPair> SimilarityPairs { get; init; } =
        Array.Empty<ImageSimilarityPair>();
    public IReadOnlyList<ImagePretestCluster> Clusters { get; init; } =
        Array.Empty<ImagePretestCluster>();

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported image pretest schema '{SchemaVersion}'.");
        }

        if (ImageCount != Images.Count || DecodedCount + FailedCount != ImageCount)
        {
            throw new InvalidOperationException("Image pretest counts are inconsistent.");
        }

        if (PortraitCount + LandscapeCount > DecodedCount)
        {
            throw new InvalidOperationException("Image orientation counts are inconsistent.");
        }

        if (ClusterCount != Clusters.Count)
        {
            throw new InvalidOperationException("Image cluster counts are inconsistent.");
        }
    }
}
