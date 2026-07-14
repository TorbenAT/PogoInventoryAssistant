namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyProbeCapability
{
    public required string Name { get; init; }
    public CalcyProbeCapabilityStatus Status { get; init; }
    public required string Detail { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
