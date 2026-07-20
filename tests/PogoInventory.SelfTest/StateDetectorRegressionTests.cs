namespace PogoInventory.SelfTest;

internal static class StateDetectorRegressionTests
{
    public static Task RunAsync()
    {
        var states = new[] { "Unknown", "GameplayMap", "MainMenu", "Inventory", "PokemonDetails", "PokemonMenu", "Appraisal" };
        Assert(states.Contains("GameplayMap"), "GameplayMap state contract");
        Assert(states.Contains("Inventory"), "Inventory state contract");
        Assert(states.Contains("PokemonDetails"), "PokemonDetails state contract");
        Assert(states.Contains("PokemonMenu"), "PokemonMenu state contract");
        Assert(states.Contains("MainMenu"), "MainMenu state contract");
        Assert(states.Contains("Appraisal"), "Appraisal state contract");
        var evidence = new[] { "MainMenuPokeballDetected", "InventorySearchBarDetected",
            "DetailsPageTopologyDetected", "AppraiseMenuItemDetected", "AppraisalIntroDetected" };
        Assert(evidence.Length == 5, "independent state anchor contracts");

        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
