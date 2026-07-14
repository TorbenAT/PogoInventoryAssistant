namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyProbeResult
{
    public required CalcyProbeReport Report { get; init; }
    public required string OutputDirectory { get; init; }
    public required string JsonReportPath { get; init; }
    public required string MarkdownReportPath { get; init; }
    public required IReadOnlyDictionary<string, string> EvidenceFiles { get; init; }
}
