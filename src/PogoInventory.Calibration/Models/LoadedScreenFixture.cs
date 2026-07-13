using PogoInventory.Vision.Imaging;

namespace PogoInventory.Calibration.Models;

public sealed record LoadedScreenFixture
{
    public required ScreenFixtureDefinition Definition { get; init; }
    public required string FullPath { get; init; }
    public required PixelImage Image { get; init; }
}
