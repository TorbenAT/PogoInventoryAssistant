namespace PogoInventory.CropAtlas.Models;

public sealed record CropAtlasReadiness
{
    public bool HasStateDiscriminatorEvidence { get; init; }
    public bool HasDynamicContentEvidence { get; init; }
    public bool HasTextDenseEvidence { get; init; }
    public bool AllClustersRepresented { get; init; }
    public bool ReadyForSemanticExperiments { get; init; }
    public bool NeedsMoreImages { get; init; }
    public IReadOnlyList<string> UnderrepresentedClusters { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<string> Reasons { get; init; } =
        Array.Empty<string>();
}
