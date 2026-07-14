namespace PogoInventory.ImagePretest.Models;

public sealed record ImagePretestOptions
{
    public int MinimumImageCount { get; init; } = 20;
    public double NearDuplicateThreshold { get; init; } = 0.995;
    public double ClusterThreshold { get; init; } = 0.925;
    public int FingerprintWidth { get; init; } = 16;
    public int FingerprintHeight { get; init; } = 32;

    public void Validate()
    {
        if (MinimumImageCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumImageCount),
                "Minimum image count must be positive.");
        }

        if (!double.IsFinite(NearDuplicateThreshold) ||
            NearDuplicateThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(NearDuplicateThreshold),
                "Near-duplicate threshold must be between 0 and 1.");
        }

        if (!double.IsFinite(ClusterThreshold) ||
            ClusterThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClusterThreshold),
                "Cluster threshold must be between 0 and 1.");
        }

        if (ClusterThreshold >= NearDuplicateThreshold)
        {
            throw new ArgumentException(
                "Cluster threshold must be lower than the near-duplicate threshold.");
        }

        if (FingerprintWidth is < 4 or > 64 || FingerprintHeight is < 4 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FingerprintWidth),
                "Fingerprint dimensions must be between 4 and 64.");
        }
    }
}
