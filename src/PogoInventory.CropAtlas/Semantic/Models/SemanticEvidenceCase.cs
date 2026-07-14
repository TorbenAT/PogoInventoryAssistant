namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceCase
{
    public required string CaseId { get; init; }
    public int SequenceNumber { get; init; }
    public required string SourceFile { get; init; }
    public required string SourceSha256 { get; init; }
    public required string ClusterId { get; init; }
    public IReadOnlyList<SemanticEvidenceCrop> Crops { get; init; } =
        Array.Empty<SemanticEvidenceCrop>();

    public void Validate(int expectedCropCount)
    {
        if (string.IsNullOrWhiteSpace(CaseId) ||
            SequenceNumber < 0 ||
            string.IsNullOrWhiteSpace(SourceFile) ||
            SourceSha256.Length != 64 ||
            string.IsNullOrWhiteSpace(ClusterId) ||
            Crops.Count != expectedCropCount)
        {
            throw new InvalidOperationException(
                "Semantic evidence case is invalid.");
        }

        foreach (var crop in Crops)
        {
            crop.Validate();
        }
    }
}
