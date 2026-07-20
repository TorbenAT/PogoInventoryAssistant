using System.Security.Cryptography;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Exploration.Services;

public sealed class PokemonGoGameStateDetector
{
    private readonly VisualControlLocator _locator = new();
    private readonly AppraisalAnalyzer _appraisalAnalyzer = new();

    public PokemonGoGameStateDetection Detect(
        byte[] screenshotPng,
        AppraisalVisualProfile? appraisalProfile = null)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var hash = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant();
        var image = PngDecoder.Decode(screenshotPng);

        if (appraisalProfile is not null)
        {
            var appraisal = _appraisalAnalyzer.Analyze(image, appraisalProfile);
            if (appraisal.IsAppraisal)
            {
                return Result(PokemonGoGameState.Appraisal, appraisal.Confidence,
                    new[] { "AppraisalBarsDetected" }, hash);
            }
        }

        var appraisalIntro = _locator.LocateAppraisalIntroContinue(screenshotPng);
        if (appraisalIntro is not null)
        {
            return Result(PokemonGoGameState.Appraisal, appraisalIntro.Confidence,
                new[] { "AppraisalIntroDetected" }.Concat(appraisalIntro.Evidence).ToArray(), hash);
        }

        var map = _locator.LocateMainMenuPokeball(screenshotPng);
        if (map is not null && map.Confidence >= 0.90)
        {
            return Result(PokemonGoGameState.GameplayMap, map.Confidence,
                new[] { "MainMenuPokeballDetected" }.Concat(map.Evidence).ToArray(), hash);
        }

        var menu = _locator.LocateAppraiseMenuItem(screenshotPng);
        if (menu is not null)
        {
            return Result(PokemonGoGameState.PokemonMenu, menu.Confidence,
                new[] { "AppraiseMenuItemDetected" }.Concat(menu.Evidence).ToArray(), hash);
        }

        var inventory = _locator.LocateInventoryCard(screenshotPng);
        if (inventory is not null)
        {
            return Result(PokemonGoGameState.Inventory, inventory.Confidence,
                new[] { "InventoryCardDetected" }.Concat(inventory.Evidence).ToArray(), hash);
        }

        var mainMenu = _locator.LocatePokemonInventory(screenshotPng);
        if (mainMenu is not null)
        {
            return Result(PokemonGoGameState.MainMenu, mainMenu.Confidence,
                new[] { "PokemonGoMainMenuDetected" }.Concat(mainMenu.Evidence).ToArray(), hash);
        }

        var details = _locator.LocateDetailsMenu(screenshotPng);
        var detailsPage = _locator.LocateDetailsPageTopology(screenshotPng);
        if (detailsPage is not null && (details is not null || detailsPage.Confidence >= 0.25))
        {
            return Result(PokemonGoGameState.PokemonDetails, details?.Confidence ?? detailsPage.Confidence,
                new[] { "DetailsMenuDetected" }.Concat(details?.Evidence ?? new[] { "DetailsMenuControlNotVisible" })
                    .Concat(detailsPage.Evidence).ToArray(), hash);
        }

        return Result(PokemonGoGameState.Unknown, 0, new[] { "NoKnownGameAnchorDetected" }, hash);
    }

    private static PokemonGoGameStateDetection Result(
        PokemonGoGameState state, double confidence, IReadOnlyList<string> evidence, string hash) =>
        new() { State = state, Confidence = confidence, Evidence = evidence,
            ScreenshotSha256 = hash };
}
