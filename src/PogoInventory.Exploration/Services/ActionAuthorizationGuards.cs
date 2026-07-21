using System.Security.Cryptography;
using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public enum UnsafeConfirmationKind
{
    None,
    PowerUp,
    Evolve,
    Transfer,
    Purify,
    PurchaseOrItem,
    UnknownConfirmation
}

public sealed record UnsafeConfirmationDetection
{
    public required UnsafeConfirmationKind Kind { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
    public bool IsUnsafe => Kind != UnsafeConfirmationKind.None;
}

public sealed class UnsafeConfirmationSurfaceDetector
{
    public UnsafeConfirmationDetection Detect(byte[] screenshotPng, string? intendedAction = null)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var hash = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant();
        var image = PngDecoder.Decode(screenshotPng);
        var pairedAdjusters = PairedAdjusterScore(image);
        var lowerLightPanel = RegionMatch(image, 0.03, 0.56, 0.97, 0.97, IsLightPanel);
        var confirmationFooter = RegionMatch(image, 0.25, 0.84, 0.75, 0.96, IsLightPanel);

        // Normal PokemonDetails has one lower action row and one menu control.
        // The Power Up confirmation has two independent +/- controls on a
        // large light modal surface. Keep this detector structural and
        // conservative; it intentionally blocks uncertain modal surfaces.
        var hasConfirmationTopology = pairedAdjusters >= 0.56 &&
            lowerLightPanel >= 0.62 && confirmationFooter >= 0.70;
        var kind = hasConfirmationTopology
            ? ClassifyByIntendedAction(intendedAction, UnsafeConfirmationKind.PowerUp)
            : HasBroadModalSurface(image)
                ? ClassifyByIntendedAction(intendedAction, UnsafeConfirmationKind.UnknownConfirmation)
                : UnsafeConfirmationKind.None;

        var evidence = new List<string>();
        evidence.Add($"paired-adjuster-score:{pairedAdjusters:F3}");
        evidence.Add($"light-panel-score:{lowerLightPanel:F3}");
        evidence.Add($"footer-score:{confirmationFooter:F3}");
        if (pairedAdjusters >= 0.56) evidence.Add("paired-adjuster-controls");
        if (lowerLightPanel >= 0.62) evidence.Add("large-light-confirmation-panel");
        if (confirmationFooter >= 0.70) evidence.Add("confirmation-footer-surface");
        if (kind == UnsafeConfirmationKind.None)
            evidence.Add("no-confirmation-surface");

        return new UnsafeConfirmationDetection
        {
            Kind = kind,
            ScreenshotSha256 = hash,
            Evidence = evidence
        };
    }

    private static UnsafeConfirmationKind ClassifyByIntendedAction(
        string? intendedAction, UnsafeConfirmationKind structuralDefault)
    {
        var action = intendedAction ?? string.Empty;
        if (action.Contains("evolve", StringComparison.OrdinalIgnoreCase))
            return UnsafeConfirmationKind.Evolve;
        if (action.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("delete", StringComparison.OrdinalIgnoreCase))
            return UnsafeConfirmationKind.Transfer;
        if (action.Contains("purify", StringComparison.OrdinalIgnoreCase))
            return UnsafeConfirmationKind.Purify;
        if (action.Contains("purchase", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("item", StringComparison.OrdinalIgnoreCase))
            return UnsafeConfirmationKind.PurchaseOrItem;
        return structuralDefault;
    }

    private static bool HasBroadModalSurface(PixelImage image)
    {
        var panel = RegionMatch(image, 0.03, 0.48, 0.97, 0.97, IsLightPanel);
        var footer = RegionMatch(image, 0.28, 0.84, 0.72, 0.96, IsLightPanel);
        return panel >= 0.80 && footer >= 0.82 &&
            RegionMatch(image, 0.08, 0.56, 0.92, 0.70, IsDarkConfirmationText) >= 0.008;
    }

    private static double PairedAdjusterScore(PixelImage image)
    {
        var yStart = (int)(image.Height * 0.60);
        var yEnd = (int)(image.Height * 0.84);
        var best = 0d;
        for (var y = yStart; y <= yEnd; y += Math.Max(4, image.Height / 180))
        {
            var left = CircleScore(image, (int)(image.Width * 0.23), y);
            var right = CircleScore(image, (int)(image.Width * 0.77), y);
            best = Math.Max(best, (left + right) / 2d);
        }
        return best;
    }

    private static double CircleScore(PixelImage image, int x, int y)
    {
        var best = 0d;
        for (var radius = Math.Max(30, image.Width / 18);
             radius <= Math.Max(42, image.Width / 9);
             radius += Math.Max(2, image.Width / 180))
        {
            var checks = new List<bool>(9);
            for (var point = 0; point < 8; point++)
            {
                var angle = point * Math.PI / 4d;
                checks.Add(IsAdjusterRing(Sample(image,
                    x + (int)(Math.Cos(angle) * radius),
                    y + (int)(Math.Sin(angle) * radius))));
            }
            checks.Add(IsLightPanel(Sample(image, x, y)));
            best = Math.Max(best, checks.Count(value => value) / (double)checks.Count);
        }
        return best;
    }

    private static double RegionMatch(PixelImage image, double left, double top,
        double right, double bottom, Func<Rgba32, bool> predicate)
    {
        var x0 = (int)(image.Width * left);
        var x1 = (int)(image.Width * right);
        var y0 = (int)(image.Height * top);
        var y1 = (int)(image.Height * bottom);
        var matched = 0;
        var total = 0;
        for (var y = y0; y < y1; y += Math.Max(1, image.Height / 90))
        for (var x = x0; x < x1; x += Math.Max(1, image.Width / 90))
        {
            total++;
            if (predicate(Sample(image, x, y))) matched++;
        }
        return total == 0 ? 0 : matched / (double)total;
    }

    private static Rgba32 Sample(PixelImage image, int x, int y) =>
        image.GetPixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1));

    private static bool IsAdjusterRing(Rgba32 pixel) =>
        pixel.G >= 110 && pixel.B >= 110 && pixel.G >= pixel.R * 1.05 &&
        pixel.B >= pixel.R * 1.02;

    private static bool IsLightPanel(Rgba32 pixel) =>
        pixel.R >= 185 && pixel.G >= 205 && pixel.B >= 185;

    private static bool IsDarkConfirmationText(Rgba32 pixel) =>
        pixel.R <= 120 && pixel.G >= 70 && pixel.B >= 70 && pixel.G >= pixel.R * 1.10;
}

public sealed record MainMenuFrameObservation
{
    public required PokemonGoGameState StrictDetectedState { get; init; }
    public PokemonGoGameState? VisualFallbackState { get; init; }
    public required bool HasMainMenuTopology { get; init; }
    public required bool HasInventoryLocator { get; init; }
    public required bool HasPokemonDetailsTopology { get; init; }
    public required bool HasPokemonMenu { get; init; }
    public required bool HasAppraisal { get; init; }
    public required bool HasUnsafeConfirmation { get; init; }
    public required IReadOnlyList<PokemonGoGameState> ConflictingStates { get; init; }
    public required NormalizedPoint? InventoryTarget { get; init; }
    public required string ScreenshotSha256 { get; init; }
}

public sealed record VerifiedMainMenuPrecondition
{
    public required PokemonGoGameState RequiredState { get; init; }
    public required PokemonGoGameState StrictDetectedState { get; init; }
    public PokemonGoGameState? VisualFallbackState { get; init; }
    public required IReadOnlyList<PokemonGoGameState> ConflictingStates { get; init; }
    public required NormalizedPoint Target { get; init; }
    public required string PreconditionScreenshotSha256 { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

public static class MainMenuPreconditionValidator
{
    public const int RequiredStableFrames = 3;

    public static VerifiedMainMenuPrecondition? TryCreate(
        IReadOnlyList<MainMenuFrameObservation> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count < RequiredStableFrames)
            return null;
        var stable = frames.TakeLast(RequiredStableFrames).ToArray();
        if (stable.Any(frame => !IsSafeMainMenuFrame(frame) || frame.InventoryTarget is null))
            return null;
        var target = stable[0].InventoryTarget!;
        if (stable.Any(frame => Distance(frame.InventoryTarget!, target) > 0.025))
            return null;

        return new VerifiedMainMenuPrecondition
        {
            RequiredState = PokemonGoGameState.MainMenu,
            StrictDetectedState = stable[^1].StrictDetectedState,
            VisualFallbackState = stable[^1].VisualFallbackState,
            ConflictingStates = stable.SelectMany(frame => frame.ConflictingStates)
                .Distinct().ToArray(),
            Target = target,
            PreconditionScreenshotSha256 = stable[^1].ScreenshotSha256,
            Evidence = new[]
            {
                "three-stable-main-menu-frames",
                "strict-main-menu-state",
                "main-menu-topology",
                "pokemon-inventory-locator",
                "no-details-menu-appraisal-or-modal-conflict"
            }
        };
    }

    public static bool IsSafeMainMenuFrame(MainMenuFrameObservation frame) =>
        frame.StrictDetectedState == PokemonGoGameState.MainMenu &&
        frame.HasMainMenuTopology && frame.HasInventoryLocator &&
        !frame.HasPokemonDetailsTopology && !frame.HasPokemonMenu &&
        !frame.HasAppraisal && !frame.HasUnsafeConfirmation &&
        frame.ConflictingStates.Count == 0;

    private static double Distance(NormalizedPoint left, NormalizedPoint right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));
}
