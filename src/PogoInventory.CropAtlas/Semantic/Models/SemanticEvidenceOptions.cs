namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceOptions
{
    public int MinimumCaseCount { get; init; } = 20;
    public int MinimumCasesPerCluster { get; init; } = 2;
    public int MaximumCropWidth { get; init; } = 640;
    public int MaximumCropHeight { get; init; } = 480;

    public void Validate()
    {
        if (MinimumCaseCount <= 0 ||
            MinimumCasesPerCluster <= 0 ||
            MaximumCropWidth <= 0 ||
            MaximumCropHeight <= 0)
        {
            throw new InvalidOperationException(
                "Semantic evidence limits must be positive.");
        }
    }
}
