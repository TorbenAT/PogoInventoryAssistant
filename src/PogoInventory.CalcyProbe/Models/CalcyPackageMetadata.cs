namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyPackageMetadata
{
    public required string PackageName { get; init; }
    public bool IsInstalled { get; init; }
    public string? VersionName { get; init; }
    public long? VersionCode { get; init; }
    public int? TargetSdk { get; init; }
    public int? MinSdk { get; init; }
    public int? UserId { get; init; }
    public bool? Enabled { get; init; }
    public DateTimeOffset? FirstInstallTime { get; init; }
    public DateTimeOffset? LastUpdateTime { get; init; }
    public IReadOnlyList<string> Activities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Receivers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequestedPermissions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GrantedPermissions { get; init; } = Array.Empty<string>();
}
