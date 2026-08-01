using System.Security.Cryptography;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

/// <summary>
/// Narrow, visually-grounded interruptions which are allowed to be recovered
/// without changing the player's inventory, currency, or game location.
/// This is deliberately separate from unsafe-confirmation detection: a broad
/// modal is never benign merely because it has a green control.
/// </summary>
public enum KnownBenignInterruptKind
{
    None,
    EggHatch,
    WeeklyChallenge,
    KnownExitDialog
}

public sealed record KnownBenignInterruptDetection
{
    public required KnownBenignInterruptKind Kind { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
    public NormalizedPoint? Target { get; init; }
    public double Confidence { get; init; }
}

public sealed class KnownBenignInterruptDetector
{
    /// <summary>Recognises an overlay-shaped Weekly card which is not precise
    /// enough to authorize Continue. Callers must stop with zero input.</summary>
    public bool IsUntrustedModalLikeOverlay(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var sky = RegionMatch(image, .08, .12, .92, .44, IsWeeklySky);
        var ground = RegionMatch(image, .08, .58, .92, .90, IsWeeklyGround);
        var cloud = RegionMatch(image, .08, .18, .92, .75, IsNearWhite);
        var title = RegionMatch(image, .20, .14, .80, .25, IsWeeklyTitlePink);
        var cta = RegionMatch(image, .24, .72, .76, .95, IsGreenCta);
        return sky >= .20 && ground >= .15 && cloud >= .08 &&
            (title >= .010 || cta >= .12);
    }

    public KnownBenignInterruptDetection Detect(byte[] screenshotPng)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var image = PngDecoder.Decode(screenshotPng);
        var hash = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant();

        // Exit is intentionally much narrower than a generic confirmation:
        // the OnePlus capture has a white central dialog, a filled green OK
        // pill, and the separate green CANCEL glyph band below it.  Purchase
        // and power-up layouts do not satisfy this three-part topology.
        var exitPanel = RegionMatch(image, .05, .35, .95, .67, IsNearWhite);
        var exitOk = RegionMatch(image, .22, .48, .78, .56, IsGreenCta);
        var cancelGlyphs = RegionMatch(image, .34, .57, .66, .63, IsTealGlyph);
        var cancelBackground = RegionMatch(image, .30, .56, .70, .64, IsNearWhite);
        if (exitPanel >= .70 && exitOk >= .22 && cancelGlyphs >= .010 && cancelBackground >= .72)
            return Detected(KnownBenignInterruptKind.KnownExitDialog,
                GlyphCentroid(image, .34, .57, .66, .63, IsTealGlyph) ??
                    new NormalizedPoint { X = .50, Y = .602 }, hash,
                Math.Min(.99, (exitPanel + exitOk + cancelBackground) / 3),
                "white-exit-dialog-panel", "green-ok-pill", "separate-cancel-glyph-band",
                "cancel-target-visually-grounded");

        // The weekly card is an unusually large blue/green illustrated panel
        // over a dimmed map with exactly one large green CTA at the bottom.
        var weeklySky = RegionMatch(image, .08, .12, .92, .44, IsWeeklySky);
        var weeklyGround = RegionMatch(image, .08, .58, .92, .90, IsWeeklyGround);
        var weeklyCta = RegionMatch(image, .24, .72, .76, .95, IsGreenCta);
        var weeklyCloudCard = RegionMatch(image, .08, .18, .92, .75, IsNearWhite);
        var weeklyTitle = RegionMatch(image, .20, .14, .80, .25, IsWeeklyTitlePink);
        if (weeklySky >= .32 && weeklyGround >= .22 && weeklyCta >= .12 &&
            weeklyCloudCard >= .12 && weeklyTitle >= .025)
            return Detected(KnownBenignInterruptKind.WeeklyChallenge,
                // Detection may use the broad lower-card band, but target
                // extraction is restricted to the CTA's lower safe zone so
                // green progress/illustration pixels cannot pull it upward.
                GlyphCentroid(image, .24, .82, .76, .95, IsGreenCta) ??
                    new NormalizedPoint { X = .50, Y = .86 }, hash,
                Math.Min(.99, (weeklySky + weeklyGround + weeklyCta) / 3),
                "weekly-sky-panel", "weekly-ground-panel", "weekly-green-continue-cta",
                "continue-target-visually-grounded");

        // Oh? is the first, inert egg-hatch frame: mint field, centred dark
        // title band, and one large blue/white egg silhouette. The egg itself
        // is the only action target; a subsequent unrecognised frame stops.
        var mintField = RegionMatch(image, .02, .06, .98, .96, IsEggMint);
        var title = RegionMatch(image, .34, .22, .66, .34, IsDarkGlyph);
        var egg = RegionMatch(image, .35, .43, .65, .64, IsEggBlueOrWhite);
        if (mintField >= .62 && title >= .008 && egg >= .20)
            return Detected(KnownBenignInterruptKind.EggHatch,
                new NormalizedPoint { X = .50, Y = .535 }, hash,
                Math.Min(.99, (mintField + egg) / 2),
                "mint-oh-screen", "dark-oh-title-band", "centred-egg-silhouette",
                "egg-target-visually-grounded");

        return Detected(KnownBenignInterruptKind.None, null, hash, 0,
            "no-known-benign-interrupt",
            $"exit:{exitPanel:F3}/{exitOk:F3}/{cancelGlyphs:F3}/{cancelBackground:F3}",
            $"weekly:{weeklySky:F3}/{weeklyGround:F3}/{weeklyCta:F3}/{weeklyCloudCard:F3}/{weeklyTitle:F3}",
            $"egg:{mintField:F3}/{title:F3}/{egg:F3}");
    }

    private static KnownBenignInterruptDetection Detected(KnownBenignInterruptKind kind,
        NormalizedPoint? target, string hash, double confidence, params string[] evidence) => new()
    {
        Kind = kind, Target = target, ScreenshotSha256 = hash, Confidence = confidence, Evidence = evidence
    };

    private static double RegionMatch(PixelImage image, double left, double top, double right, double bottom,
        Func<Rgba32, bool> predicate)
    {
        var matched = 0;
        var total = 0;
        for (var y = (int)(image.Height * top); y < (int)(image.Height * bottom); y += Math.Max(1, image.Height / 100))
        for (var x = (int)(image.Width * left); x < (int)(image.Width * right); x += Math.Max(1, image.Width / 100))
        {
            total++;
            if (predicate(image.GetPixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1)))) matched++;
        }
        return total == 0 ? 0 : matched / (double)total;
    }

    private static NormalizedPoint? GlyphCentroid(PixelImage image, double left, double top, double right,
        double bottom, Func<Rgba32, bool> predicate)
    {
        var x0 = (int)(image.Width * left);
        var x1 = (int)(image.Width * right);
        var y0 = (int)(image.Height * top);
        var y1 = (int)(image.Height * bottom);
        long count = 0;
        long sumX = 0;
        long sumY = 0;
        for (var y = y0; y < y1; y++)
        for (var x = x0; x < x1; x++)
        {
            if (!predicate(image.GetPixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1))))
                continue;
            count++;
            sumX += x;
            sumY += y;
        }
        return count == 0 ? null : new NormalizedPoint
        {
            X = sumX / (double)count / Math.Max(1, image.Width - 1),
            Y = sumY / (double)count / Math.Max(1, image.Height - 1)
        };
    }

    private static bool IsNearWhite(Rgba32 p) => p.R >= 215 && p.G >= 215 && p.B >= 205;
    private static bool IsGreenCta(Rgba32 p) => p.G >= 150 && p.B >= 105 && p.R >= 75 && p.G >= p.R * 1.25;
    private static bool IsTealGlyph(Rgba32 p) => p.G >= 120 && p.B >= 105 && p.R <= 115 && p.G >= p.R * 1.20;
    private static bool IsWeeklySky(Rgba32 p) => p.B >= 145 && p.G >= 130 && p.R <= 150 && p.B >= p.R * 1.20;
    private static bool IsWeeklyGround(Rgba32 p) => p.G >= 100 && p.G >= p.R * 1.15 && p.G >= p.B * .85;
    private static bool IsWeeklyTitlePink(Rgba32 p) => p.R >= 190 && p.G is >= 45 and <= 180 &&
        p.B is >= 80 and <= 210 && p.R >= p.G * 1.30;
    private static bool IsEggMint(Rgba32 p) => p.G >= 205 && p.B >= 195 && p.R >= 175 && p.G >= p.R * 1.04;
    private static bool IsDarkGlyph(Rgba32 p) => p.R <= 115 && p.G <= 135 && p.B <= 135;
    private static bool IsEggBlueOrWhite(Rgba32 p) => p.B >= 145 && p.B >= p.R * 1.12 && p.B >= p.G * 1.02 ||
        (p.R >= 205 && p.G >= 210 && p.B >= 215);
}
