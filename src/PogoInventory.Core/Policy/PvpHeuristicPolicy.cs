namespace PogoInventory.Core.Policy;

public sealed record PvpHeuristicPolicy
{
    public bool Enabled { get; init; } = true;
    public int MaximumAttackIv { get; init; } = 5;
    public int MinimumDefenseIv { get; init; } = 10;
    public int MinimumHpIv { get; init; } = 10;
    public bool PreserveBestCandidatePerGroup { get; init; } = true;
}
