namespace PogoInventory.CropAtlas.Semantic.Models;

public sealed record SemanticTruthTemplate
{
    public string SchemaVersion { get; init; } = "1.0";
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public string Instructions { get; init; } =
        "Populate only values that are visibly certain. Leave unsupported fields null.";
    public IReadOnlyList<SemanticTruthCase> Cases { get; init; } =
        Array.Empty<SemanticTruthCase>();
}

public sealed record SemanticTruthCase
{
    public required string CaseId { get; init; }
    public required string SourceFile { get; init; }
    public required string ClusterId { get; init; }
    public string? ExpectedScreenState { get; init; }
    public string? ExpectedSpecies { get; init; }
    public int? ExpectedCp { get; init; }
    public int? ExpectedAttackIv { get; init; }
    public int? ExpectedDefenseIv { get; init; }
    public int? ExpectedHpIv { get; init; }
    public string? Notes { get; init; }
    public bool Reviewed { get; init; }
}
