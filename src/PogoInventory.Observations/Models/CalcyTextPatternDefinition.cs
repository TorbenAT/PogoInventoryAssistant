namespace PogoInventory.Observations.Models;

public sealed record CalcyTextPatternDefinition
{
    public required CalcyTextField Field { get; init; }
    public required string Pattern { get; init; }
    public string GroupName { get; init; } = "value";
    public string? SourceName { get; init; }
    public bool IgnoreCase { get; init; } = true;
}
