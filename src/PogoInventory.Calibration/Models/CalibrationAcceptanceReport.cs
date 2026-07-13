namespace PogoInventory.Calibration.Models;

public sealed record CalibrationAcceptanceReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ManifestName { get; init; }
    public required string ProfileName { get; init; }
    public required bool Accepted { get; init; }
    public required int ApprovedFixtureCount { get; init; }
    public required int CorrectCount { get; init; }
    public required int FalseNegativeCount { get; init; }
    public required int FalsePositiveCount { get; init; }
    public required int MisclassificationCount { get; init; }
    public required int WeakAnchorCount { get; init; }
    public IReadOnlyList<string> AcceptanceFailures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingCoverage { get; init; } = Array.Empty<string>();
    public IReadOnlyList<FixtureAcceptanceResult> Fixtures { get; init; } =
        Array.Empty<FixtureAcceptanceResult>();
    public IReadOnlyList<StateAcceptanceMetric> States { get; init; } =
        Array.Empty<StateAcceptanceMetric>();
    public IReadOnlyList<ConfusionMatrixCell> ConfusionMatrix { get; init; } =
        Array.Empty<ConfusionMatrixCell>();
    public IReadOnlyList<AnchorQualityMetric> Anchors { get; init; } =
        Array.Empty<AnchorQualityMetric>();
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
