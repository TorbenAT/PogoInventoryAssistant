using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;

internal static class GuardedInventoryCloseTests
{
    public static Task RunAsync()
    {
        var inventory = Detection(PokemonGoGameState.Inventory);
        var map = Detection(PokemonGoGameState.GameplayMap);
        var unknown = Detection(PokemonGoGameState.Unknown);
        var details = Detection(PokemonGoGameState.PokemonDetails);

        AssertTrue(GuardedInventoryClose.MaxActions == 1, "close action has one-action limit");
        AssertTrue(GuardedInventoryClose.CanAct(inventory), "inventory permits one Back");
        AssertTrue(!GuardedInventoryClose.CanAct(map), "map permits no close action");
        AssertTrue(!GuardedInventoryClose.CanAct(unknown), "unknown permits no close action");
        AssertTrue(GuardedInventoryClose.IsSuccessful(map), "map is the only successful post-state");
        AssertTrue(!GuardedInventoryClose.IsSuccessful(details), "details is not a successful post-state");
        AssertTrue(!GuardedInventoryClose.IsSuccessful(unknown), "unknown is not a successful post-state");
        AssertTrue(inventory.ScreenshotSha256 != map.ScreenshotSha256, "before and after hashes are recorded and differ");
        return Task.CompletedTask;
    }

    private static PokemonGoGameStateDetection Detection(PokemonGoGameState state) => new()
    {
        State = state,
        Confidence = state == PokemonGoGameState.Unknown ? 0 : 1,
        Evidence = new[] { state.ToString() },
        ScreenshotSha256 = state + "-hash"
    };

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
