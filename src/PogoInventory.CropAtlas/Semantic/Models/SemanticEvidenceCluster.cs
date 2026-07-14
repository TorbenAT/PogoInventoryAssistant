namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceCluster
{
    public required string ClusterId { get; init; }
    public int CaseCount { get; init; }
    public bool Underrepresented { get; init; }
    public IReadOnlyList<string> CaseIds { get; init; } =
        Array.Empty<string>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClusterId) ||
            CaseCount <= 0 ||
            CaseIds.Count != CaseCount ||
            CaseIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Semantic evidence cluster is invalid.");
        }
    }
}
