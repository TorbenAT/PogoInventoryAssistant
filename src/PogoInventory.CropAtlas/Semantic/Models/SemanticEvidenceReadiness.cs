namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceReadiness
{
    public bool ReadyForExternalVisualReview { get; init; }
    public bool ReadyForAutomatedExtraction { get; init; }
    public bool NeedsMoreImages { get; init; }
    public IReadOnlyList<string> UnderrepresentedClusters { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<string> Reasons { get; init; } =
        Array.Empty<string>();
    public required string RecommendedNextAction { get; init; }
}
