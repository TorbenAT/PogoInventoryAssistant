namespace PogoInventory.Calibration.Models;

public sealed record CalibrationIndexResult
{
    public required int FixtureCount { get; init; }
    public required int NewFixtureCount { get; init; }
    public required int ChangedFixtureCount { get; init; }
    public required int PreservedApprovalCount { get; init; }
    public required string ManifestPath { get; init; }
}
