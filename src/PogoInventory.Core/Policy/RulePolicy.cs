namespace PogoInventory.Core.Policy;

public sealed record RulePolicy
{
    public string Version { get; init; } = "0.1.0";
    public DateOnly OldPokemonCutoff { get; init; } = new(2018, 12, 31);
    public int MinimumOrdinaryCopiesPerSpeciesForm { get; init; } = 1;
    public bool DeleteRequiresExactIdentity { get; init; } = true;
    public bool DeleteRequiresStrictlyBetterDuplicate { get; init; } = true;
    public IReadOnlyCollection<string> TradeTagNames { get; init; } = new[] { "Trade" };
    public IReadOnlyCollection<string> TradeNicknameFragments { get; init; } = new[] { "Trade distan" };
    public PvpHeuristicPolicy PvpHeuristic { get; init; } = new();
}
