namespace PogoInventory.Core.Models;

public sealed record PokemonDecision
{
    public required string ExternalKey { get; init; }
    public required int SequenceNumber { get; init; }
    public required string Species { get; init; }
    public required string GroupKey { get; init; }
    public required DecisionCategory Category { get; init; }
    public required IReadOnlyList<DecisionReason> Reasons { get; init; }
    public required string PolicyVersion { get; init; }
    public string? BetterDuplicateExternalKey { get; init; }
}
