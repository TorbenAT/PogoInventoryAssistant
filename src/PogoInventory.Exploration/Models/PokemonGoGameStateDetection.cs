namespace PogoInventory.Exploration.Models;

public sealed record PokemonGoGameStateDetection
{
    public required PokemonGoGameState State { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
    public required string ScreenshotSha256 { get; init; }
}
