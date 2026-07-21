using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;

namespace PogoInventory.SelfTest;

internal static class KnownGameStateNormalizerTests
{
    public static Task RunAsync()
    {
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.GameplayMap, null) is null,
            "GameplayMap is already ready");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.MainMenu, null) ==
            "close-main-menu", "MainMenu has a named close transition");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.Inventory, null) ==
            "close-inventory", "Inventory closes through the named operation");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.InventorySearchOpen, null) ==
            "close-inventory", "open search closes through inventory recovery");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.InventoryFiltered, null) ==
            "close-inventory", "filtered inventory closes through inventory recovery");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.PokemonDetails, null) ==
            "return-to-inventory", "Details uses guarded ReturnToInventory");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.PokemonMenu, null) ==
            "return-to-inventory", "Menu uses guarded Back recovery");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.Appraisal,
                RecoveryFrameKind.AppraisalIntro) == "device-continue-appraisal-intro",
            "AppraisalIntro uses exactly one named continue operation");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.Appraisal,
                RecoveryFrameKind.AppraisalBars) == "exit-appraisal",
            "AppraisalBars uses guarded appraisal exit");
        Assert(KnownGameStateNormalizer.MaximumRecoveryInputs == 6,
            "normalizer has a six-input hard limit");
        Assert(KnownGameStateNormalizer.NextAction(PokemonGoGameState.Unknown, null) ==
            "STOP_UNKNOWN", "Unknown has no automatic input");
        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
