using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.SelfTest;

internal static class WrongScreenAuthorizationTests
{
    public static Task RunAsync()
    {
        var detector = new UnsafeConfirmationSurfaceDetector();
        var details = detector.Detect(PngEncoder.Encode(CreateFixture(powerUpModal: false)), "open-inventory");
        Assert(details.Kind == UnsafeConfirmationKind.None,
            "normal Details POWER UP and EVOLVE buttons must not be treated as a modal");

        var powerUp = detector.Detect(PngEncoder.Encode(CreateFixture(powerUpModal: true)), "open-inventory");
        Assert(powerUp.Kind == UnsafeConfirmationKind.PowerUp,
            $"Power Up confirmation evidence must be classified as unsafe ({string.Join(',', powerUp.Evidence)})");

        var valid = Enumerable.Range(0, 3).Select(_ => Frame()).ToArray();
        var precondition = MainMenuPreconditionValidator.TryCreate(valid);
        Assert(precondition is not null, "three stable MainMenu frames authorize inventory");
        var authorizedInputs = precondition is null ? 0 : 1;
        Assert(authorizedInputs == 1, "valid MainMenu inventory authorization is exactly one input");

        var detailsConflict = valid[..2].Append(Frame(
            strict: PokemonGoGameState.MainMenu,
            details: true,
            fallback: null)).ToArray();
        Assert(MainMenuPreconditionValidator.TryCreate(detailsConflict) is null,
            "MainMenu plus Details conflict denies input");

        var staleThenDetails = valid[..2].Append(Frame(
            strict: PokemonGoGameState.Unknown,
            details: true,
            fallback: PokemonGoGameState.PokemonDetails)).ToArray();
        Assert(MainMenuPreconditionValidator.TryCreate(staleThenDetails) is null,
            "stale MainMenu followed by Details denies input");

        var visualDetailsFallback = Enumerable.Range(0, 3).Select(_ => Frame(
            strict: PokemonGoGameState.Unknown,
            details: true,
            fallback: PokemonGoGameState.PokemonDetails)).ToArray();
        Assert(MainMenuPreconditionValidator.TryCreate(visualDetailsFallback) is null,
            "visual Details fallback cannot grant MainMenu authorization");

        var unsafeFrame = valid[..2].Append(Frame(unsafeConfirmation: true)).ToArray();
        Assert(MainMenuPreconditionValidator.TryCreate(unsafeFrame) is null,
            "unsafe confirmation denies all normal navigation");

        var source = File.ReadAllText(RepositoryPath(
            "src", "PogoInventory.Exploration", "Services",
            "AndroidVerifiedInventoryNamedOperations.cs"));
        Assert(source.Contains("AutoCancel = false", StringComparison.Ordinal),
            "unsafe confirmation must not auto-cancel");
        Assert(!source.Contains("CancelAsync", StringComparison.Ordinal),
            "unsafe confirmation must not send an automatic Cancel input");
        return Task.CompletedTask;
    }

    private static PixelImage CreateFixture(bool powerUpModal)
    {
        const int width = 1080;
        const int height = 1920;
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < rgba.Length; index += 4)
        {
            rgba[index] = 25;
            rgba[index + 1] = 35;
            rgba[index + 2] = 45;
            rgba[index + 3] = 255;
        }

        if (!powerUpModal)
            return new PixelImage(width, height, rgba);

        for (var y = (int)(height * 0.56); y < (int)(height * 0.97); y++)
        for (var x = (int)(width * 0.03); x < (int)(width * 0.97); x++)
        {
            var offset = (y * width + x) * 4;
            rgba[offset] = 225;
            rgba[offset + 1] = 225;
            rgba[offset + 2] = 225;
            rgba[offset + 3] = 255;
        }

        foreach (var centerX in new[] { (int)(width * 0.23), (int)(width * 0.77) })
        {
            const int centerY = 1420;
            const int radius = 55;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                var distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                if (distance is < 48 or > 70)
                    continue;
                var offset = (y * width + x) * 4;
                rgba[offset] = 75;
                rgba[offset + 1] = 180;
                rgba[offset + 2] = 180;
                rgba[offset + 3] = 255;
            }
        }

        return new PixelImage(width, height, rgba);
    }

    private static MainMenuFrameObservation Frame(
        PokemonGoGameState strict = PokemonGoGameState.MainMenu,
        bool details = false,
        PokemonGoGameState? fallback = null,
        bool unsafeConfirmation = false) => new()
    {
        StrictDetectedState = strict,
        VisualFallbackState = fallback,
        HasMainMenuTopology = true,
        HasInventoryLocator = true,
        HasPokemonDetailsTopology = details,
        HasPokemonMenu = false,
        HasAppraisal = false,
        HasUnsafeConfirmation = unsafeConfirmation,
        ConflictingStates = details
            ? new[] { PokemonGoGameState.PokemonDetails }
            : Array.Empty<PokemonGoGameState>(),
        InventoryTarget = new NormalizedPoint { X = 0.22, Y = 0.75 },
        ScreenshotSha256 = Guid.NewGuid().ToString("N")
    };

    private static string RepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root was not found.");
        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
