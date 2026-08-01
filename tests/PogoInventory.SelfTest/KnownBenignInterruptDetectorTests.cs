using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.SelfTest;

internal static class KnownBenignInterruptDetectorTests
{
    public static Task RunAsync()
    {
        var detector = new KnownBenignInterruptDetector();
        Assert(detector.Detect(PngEncoder.Encode(ExitFixture())).Kind == KnownBenignInterruptKind.KnownExitDialog,
            "only the dedicated exit topology may authorize CancelKnownExitDialog");
        var weekly = detector.Detect(PngEncoder.Encode(WeeklyFixture()));
        Assert(weekly.Kind == KnownBenignInterruptKind.WeeklyChallenge,
            "weekly continuation requires its sky, ground and CTA topology: " + string.Join(",", weekly.Evidence));
        Assert(weekly.Target is { X: >= .49 and <= .51, Y: >= .84 and <= .91 },
            "weekly continuation targets the visually measured CTA centroid, not its upper edge");
        Assert(detector.Detect(PngEncoder.Encode(MapLikeButNotWeeklyFixture())).Kind == KnownBenignInterruptKind.None,
            "a gameplay-map-like sky, ground and green control without the weekly cloud card and title remains unknown");
        Assert(!detector.IsUntrustedModalLikeOverlay(PngEncoder.Encode(MapLikeButNotWeeklyFixture())),
            "ordinary map-like pixels without the card never become a modal fence");
        Assert(detector.Detect(PngEncoder.Encode(EggFixture())).Kind == KnownBenignInterruptKind.EggHatch,
            "Oh? requires the mint field, title and centred egg silhouette");
        Assert(detector.Detect(PngEncoder.Encode(BlankFixture())).Kind == KnownBenignInterruptKind.None,
            "a generic dark screen remains unknown and sends zero recovery input");
        var host = File.ReadAllText(RepositoryPath("src", "PogoInventory.Exploration", "Services",
            "AndroidVerifiedInventoryNamedOperations.cs"));
        var recoveryStart = host.IndexOf("RecoverKnownBenignInterruptAsync", StringComparison.Ordinal);
        var recovery = host[recoveryStart..];
        Assert(recovery.Contains("stable.Kind == KnownBenignInterruptKind.KnownExitDialog", StringComparison.Ordinal) &&
               recovery.Contains("StateBoundAndroidBackFallback", StringComparison.Ordinal) &&
               recovery.Contains("await _transport.PressBackAsync", StringComparison.Ordinal),
            "Android Back is available only as the named, state-bound exit-dialog fallback");
        Assert(recovery.Contains("UnsafeConfirmationKind.PowerUp", StringComparison.Ordinal) &&
               recovery.Contains("DENIED_DESTRUCTIVE_CONFIRMATION", StringComparison.Ordinal),
            "the exit fallback retains explicit destructive-confirmation denial");
        Assert(host.Contains("if (state.State == PokemonGoGameState.Unknown)", StringComparison.Ordinal),
            "failed interrupt recovery is terminal and cannot consume a second input in one operation");
        Assert(host.Contains("IsUntrustedModalLikeOverlay", StringComparison.Ordinal) &&
               host.Contains("ZERO_INPUT_STOP", StringComparison.Ordinal),
            "an untrusted overlay stops before underlying Details state can authorize Back");
        Assert(recovery.Contains("postDeadline", StringComparison.Ordinal) &&
               recovery.Contains("KnownBenignInterruptPostcondition", StringComparison.Ordinal),
            "recovery waits only within the bounded state deadline for its known postcondition");
        var locatorSource = File.ReadAllText(RepositoryPath("src", "PogoInventory.Exploration", "Services",
            "VisualControlLocator.cs"));
        Assert(locatorSource.Contains("image.Height * 0.84", StringComparison.Ordinal),
            "main-menu locator excludes the observed schedule-raids banner band above the PokéBall safe zone");
        Assert(host.Contains("PngDecoder.Decode(screenshot)", StringComparison.Ordinal) &&
               host.Contains("point.ToPixels(targetImage.Width, targetImage.Height)", StringComparison.Ordinal),
            "fresh visual targets are converted with their own screenshot geometry, not mismatched metadata");
        return Task.CompletedTask;
    }

    private static PixelImage ExitFixture()
    {
        var rgba = BlankPixels();
        Fill(rgba, .05, .35, .95, .67, 240, 240, 232);
        Fill(rgba, .22, .48, .78, .56, 95, 205, 145);
        Fill(rgba, .34, .57, .66, .63, 240, 240, 232);
        // Sparse, teal glyph-like strokes in the separate CANCEL band.
        for (var x = .40; x < .60; x += .035)
            Fill(rgba, x, .585, x + .008, .615, 40, 180, 150);
        return new PixelImage(Width, Height, rgba);
    }

    private static PixelImage WeeklyFixture()
    {
        var rgba = BlankPixels();
        Fill(rgba, .08, .12, .92, .44, 60, 170, 215);
        Fill(rgba, .08, .58, .92, .90, 80, 165, 95);
        Fill(rgba, .10, .30, .90, .60, 240, 240, 232);
        Fill(rgba, .20, .14, .80, .25, 235, 80, 140);
        Fill(rgba, .24, .84, .76, .94, 95, 205, 145);
        return new PixelImage(Width, Height, rgba);
    }

    private static PixelImage EggFixture()
    {
        var rgba = BlankPixels();
        Fill(rgba, .02, .06, .98, .96, 210, 240, 225);
        Fill(rgba, .47, .26, .53, .30, 70, 90, 90);
        Fill(rgba, .40, .47, .60, .62, 215, 225, 240);
        Fill(rgba, .45, .50, .55, .60, 105, 135, 215);
        return new PixelImage(Width, Height, rgba);
    }

    private static PixelImage MapLikeButNotWeeklyFixture()
    {
        var rgba = BlankPixels();
        Fill(rgba, .08, .12, .92, .44, 60, 170, 215);
        Fill(rgba, .08, .58, .92, .90, 80, 165, 95);
        Fill(rgba, .24, .75, .76, .88, 95, 205, 145);
        return new PixelImage(Width, Height, rgba);
    }

    private static PixelImage BlankFixture()
    {
        return new PixelImage(Width, Height, BlankPixels());
    }

    private const int Width = 400;
    private const int Height = 800;

    private static byte[] BlankPixels()
    {
        var rgba = Enumerable.Repeat((byte)0, Width * Height * 4).ToArray();
        for (var i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
        return rgba;
    }

    private static void Fill(byte[] rgba, double left, double top, double right, double bottom,
        byte r, byte g, byte b)
    {
        for (var y = (int)(Height * top); y < (int)(Height * bottom); y++)
        for (var x = (int)(Width * left); x < (int)(Width * right); x++)
        {
            var offset = (y * Width + x) * 4;
            rgba[offset] = r; rgba[offset + 1] = g; rgba[offset + 2] = b; rgba[offset + 3] = 255;
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static string RepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root was not found.");
        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
