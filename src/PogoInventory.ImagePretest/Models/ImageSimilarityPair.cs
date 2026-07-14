namespace PogoInventory.ImagePretest.Models;

public sealed record ImageSimilarityPair
{
    public required string FirstFileName { get; init; }
    public required string SecondFileName { get; init; }
    public double Similarity { get; init; }
    public bool ExactDuplicate { get; init; }
    public bool NearDuplicate { get; init; }
    public bool Consecutive { get; init; }
}
