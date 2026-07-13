using PogoInventory.Core.Models;

namespace PogoInventory.Core.Analysis;

public sealed record InventoryAnalysisResult
{
    public required IReadOnlyList<PokemonDecision> Decisions { get; init; }

    public int KeepCount => Decisions.Count(x => x.Category == DecisionCategory.Keep);
    public int ReviewCount => Decisions.Count(x => x.Category == DecisionCategory.Review);
    public int DeleteCount => Decisions.Count(x => x.Category == DecisionCategory.Delete);
}
