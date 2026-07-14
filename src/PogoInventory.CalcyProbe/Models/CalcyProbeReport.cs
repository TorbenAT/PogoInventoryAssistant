using PogoInventory.Device.Models;

namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyProbeReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string ProbeVersion { get; init; }
    public required string PackageName { get; init; }
    public required AndroidDeviceMetadata Device { get; init; }
    public required CalcyPackageMetadata Package { get; init; }
    public string? ProcessId { get; init; }
    public CalcyProbeDecision Decision { get; init; }
    public IReadOnlyList<CalcyProbeCapability> Capabilities { get; init; } = Array.Empty<CalcyProbeCapability>();
    public int FilteredLogLineCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public required IReadOnlyDictionary<string, string> EvidenceSha256 { get; init; }
}
