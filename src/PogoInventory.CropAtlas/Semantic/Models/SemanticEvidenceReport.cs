using PogoInventory.RegionDiscovery.Models;

namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticEvidenceReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string InputDirectory { get; init; }
    public required string RegionReportPath { get; init; }
    public required string CropAtlasReportPath { get; init; }
    public int CaseCount { get; init; }
    public int ClusterCount { get; init; }
    public int SelectedRegionCount { get; init; }
    public int CropCount { get; init; }
    public bool Accepted { get; init; }
    public required string GateDetail { get; init; }
    public required string ReviewPackFile { get; init; }
    public required string TruthTemplateFile { get; init; }
    public required SemanticEvidenceReadiness Readiness { get; init; }
    public IReadOnlyList<RegionCandidateKind> CandidateKinds { get; init; } =
        Array.Empty<RegionCandidateKind>();
    public IReadOnlyList<SemanticEvidenceCluster> Clusters { get; init; } =
        Array.Empty<SemanticEvidenceCluster>();
    public IReadOnlyList<SemanticEvidenceCase> Cases { get; init; } =
        Array.Empty<SemanticEvidenceCase>();

    public void Validate()
    {
        if (SchemaVersion != "1.0" ||
            string.IsNullOrWhiteSpace(InputDirectory) ||
            string.IsNullOrWhiteSpace(RegionReportPath) ||
            string.IsNullOrWhiteSpace(CropAtlasReportPath) ||
            string.IsNullOrWhiteSpace(GateDetail) ||
            string.IsNullOrWhiteSpace(ReviewPackFile) ||
            string.IsNullOrWhiteSpace(TruthTemplateFile) ||
            CaseCount != Cases.Count ||
            ClusterCount != Clusters.Count ||
            SelectedRegionCount <= 0 ||
            CropCount != CaseCount * SelectedRegionCount)
        {
            throw new InvalidOperationException(
                "Semantic evidence report counts or paths are inconsistent.");
        }

        foreach (var cluster in Clusters)
        {
            cluster.Validate();
        }

        foreach (var item in Cases)
        {
            item.Validate(SelectedRegionCount);
        }
    }
}
