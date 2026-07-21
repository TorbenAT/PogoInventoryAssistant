using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;

namespace PogoInventory.SelfTest;

internal static class WrongScreenAuthorizationTests
{
    public static Task RunAsync()
    {
        var detailsPath = RepositoryPath(
            "local-data", "validation", "android-sequence-host", "failures",
            "ten-item-age0-1825-final-start", "screen.png");
        var powerUpPath = RepositoryPath(
            "local-data", "validation", "android-sequence-host", "failures",
            "ten-item-age0-1825-final3-current", "screen.png");
        var detector = new UnsafeConfirmationSurfaceDetector();
        var details = detector.Detect(File.ReadAllBytes(detailsPath), "open-inventory");
        Assert(details.Kind == UnsafeConfirmationKind.None,
            "normal Details POWER UP and EVOLVE buttons must not be treated as a modal");

        var powerUp = detector.Detect(File.ReadAllBytes(powerUpPath), "open-inventory");
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
