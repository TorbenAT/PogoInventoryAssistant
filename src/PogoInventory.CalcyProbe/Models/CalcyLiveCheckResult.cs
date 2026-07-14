using PogoInventory.Automation.Models;
using PogoInventory.Observations.Models;

namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyLiveCheckResult
{
    public required InventoryAutomationResult Navigation { get; init; }
    public required CalcyProbeResult Probe { get; init; }
    public CalcyObservation? ParsedObservation { get; init; }
    public string? ParsedObservationPath { get; init; }
}
