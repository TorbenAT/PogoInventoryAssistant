using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

public static class GuardedInventoryClose
{
    public const int MaxActions = 1;

    public static bool CanAct(PokemonGoGameStateDetection start) =>
        start.State == PokemonGoGameState.Inventory;

    public static bool IsSuccessful(PokemonGoGameStateDetection post) =>
        post.State == PokemonGoGameState.GameplayMap;
}
