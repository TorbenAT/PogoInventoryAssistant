using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task H: the field diagnosis found two things on a warm/bright (sunset)
/// background: (1) <see cref="VisualControlLocator.LocateDetailsPageTopology"/>'s
/// model-area heuristic flickers just under its 0.15 floor while the CP/name
/// band and the lower details panel remain fully intact, and (2)
/// <see cref="VisualControlLocator.LocatePokemonInventory"/>'s
/// <c>IsMenuBackground</c> fallback false-fired on only 2-of-3 sample points
/// for a sunset-colored frame that was really a Details screen.
///
/// Change (2), tightening the MainMenu background fallback from 2-of-3 to
/// 3-of-3 sample points, is implemented and covered below: it strictly
/// tightens an input-authorizing state, so it carries no additional risk.
///
/// Change (1), a proposed double-corroborated relaxation of the Details
/// topology gate (accept when modelArea &gt;= 0.08 &amp;&amp; cpArea &gt;= 0.50 &amp;&amp;
/// detailsPanel &gt;= 0.50), was verified against the real evidence corpus
/// under local-data/ and found UNSAFE: roughly 200 real frames confidently
/// classified as MainMenu, Appraisal, Inventory or GameplayMap by the
/// existing (trusted) detector pipeline also satisfy those three floors --
/// several with cpArea/detailsPanel magnitudes (0.95-1.00) indistinguishable
/// from the five genuine warm-background Details frames (0.958-0.998 /
/// 0.978). No threshold tuning within just these three regions separates
/// the two populations, and at least one call site
/// (<c>AndroidVerifiedInventoryNamedOperations.CaptureCleanupIdentityAsync</c>)
/// treats a non-null topology result as "this is PokemonDetails" with NO
/// additional state gating. Shipping the proposed constants would therefore
/// make PokemonDetails easier to falsely claim on MainMenu/Appraisal/
/// Inventory/GameplayMap screens -- the opposite of the fail-closed
/// requirement. Change (1) is intentionally NOT implemented; see
/// task-H-report.md for the full corroboration data and the escalation.
///
/// This test file documents that the existing (measured-safe) gate on
/// <see cref="VisualControlLocator.LocateDetailsPageTopology"/> is
/// unchanged, alongside the change (2) MainMenu regression coverage.
/// </summary>
internal static class WarmBackgroundDetectionStabilityTests
{
    public static Task RunAsync()
    {
        MainMenuBackgroundTwoOfThreeIsNoLongerSufficient();
        MainMenuBackgroundThreeOfThreeStillClassifies();
        DetailsTopologyDegenerateFrameStillNull();
        DetailsTopologyStrongPrimaryFrameStillClassifies();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exactly 2 of the 3 <c>IsMenuBackground</c> sample points are satisfied
    /// (the third is painted with a dark patch). Before the fix this passed
    /// (2-of-3); after the fix it must fail (3-of-3 required), so
    /// <see cref="VisualControlLocator.LocatePokemonInventory"/> returns
    /// null instead of falsely authorizing a MainMenu classification.
    /// </summary>
    private static void MainMenuBackgroundTwoOfThreeIsNoLongerSufficient()
    {
        var frame = CreateMenuBackgroundFrame(satisfyThirdPoint: false);
        var located = new VisualControlLocator().LocatePokemonInventory(frame);
        AssertTrue(located is null,
            "2-of-3 MainMenu background sample points must no longer authorize a MainMenu classification");
    }

    /// <summary>
    /// All 3 <c>IsMenuBackground</c> sample points are satisfied. This must
    /// still classify, proving the tightening did not disturb a genuine
    /// MainMenu frame.
    /// </summary>
    private static void MainMenuBackgroundThreeOfThreeStillClassifies()
    {
        var frame = CreateMenuBackgroundFrame(satisfyThirdPoint: true);
        var located = new VisualControlLocator().LocatePokemonInventory(frame);
        AssertTrue(located is not null,
            "3-of-3 MainMenu background sample points must still authorize a MainMenu classification");
    }

    /// <summary>
    /// High CP/name-band and details-panel coverage with a low model area
    /// (below both the 0.15 primary floor and a hypothetical 0.08 relaxed
    /// floor) must still return null: the un-relaxed gate has no double
    /// corroboration branch, and this floor is exactly what would need to
    /// hold if one were ever added safely.
    /// </summary>
    private static void DetailsTopologyDegenerateFrameStillNull()
    {
        var frame = CreateDegenerateModelAreaFrame();
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is null,
            "a frame with high cp/panel coverage but a degenerate model area must remain unclassified");
    }

    /// <summary>
    /// Regression: a frame that already satisfies the existing >=0.15/>=0.10/
    /// >=0.20 primary gate must still classify as Details topology exactly as
    /// before -- change (2) only touches MainMenu, and change (1) was not
    /// implemented, so this path is untouched.
    /// </summary>
    private static void DetailsTopologyStrongPrimaryFrameStillClassifies()
    {
        var frame = CreateStrongDetailsFrame();
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is not null,
            "a strong Details frame satisfying the existing primary gate must still classify");
    }

    /// <summary>
    /// A frame whose three <c>IsMenuBackground</c> sample points
    /// ((w/10, h/3), (w/2, h/4), (w*9/10, h/2)) are either all light
    /// (R&gt;=170,G&gt;=185,B&gt;=160) or, when <paramref name="satisfyThirdPoint"/>
    /// is false, the third point is painted dark so only 2 of 3 pass. No
    /// teal circular control is present, so the teal-candidate search never
    /// scores above the 0.75 threshold and the background check alone
    /// decides the outcome.
    /// </summary>
    private static byte[] CreateMenuBackgroundFrame(bool satisfyThirdPoint)
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 200, g: 200, b: 200);
        if (!satisfyThirdPoint)
        {
            // Third sample point is (width * 9 / 10, height / 2) = (270, 300).
            PaintPoint(rgba, width, height, x: width * 9 / 10, y: height / 2, r: 40, g: 40, b: 40, radius: 6);
        }
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    /// <summary>
    /// High CP/name band (light) and a broad light details panel, but the
    /// model-area band is filled with a uniform bright color that fails
    /// <c>IsDetailsModelArea</c> (needs channel spread &gt;= 8) everywhere,
    /// so modelArea measures 0.0.
    /// </summary>
    private static byte[] CreateDegenerateModelAreaFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 235, g: 235, b: 235);
        // modelArea region (0.22,0.12)-(0.78,0.42): uniform bright, no spread -> IsDetailsModelArea false everywhere.
        Paint(rgba, width, height, 0.22, 0.12, 0.78, 0.42, r: 235, g: 235, b: 235);
        // cpArea region (0.25,0.07)-(0.75,0.18): light -> IsDetailsPanel true.
        Paint(rgba, width, height, 0.25, 0.07, 0.75, 0.18, r: 240, g: 240, b: 240);
        // detailsPanel region (0.03,0.39)-(0.97,0.92): light -> IsDetailsPanelBroad true.
        Paint(rgba, width, height, 0.03, 0.39, 0.97, 0.92, r: 235, g: 240, b: 235);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    /// <summary>
    /// Satisfies the existing (unchanged) primary gate directly: a
    /// non-uniform, mid-tone model-area band (spread &gt;= 8, channels in
    /// 35-190) plus a light CP band and a light details panel.
    /// </summary>
    private static byte[] CreateStrongDetailsFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 230, g: 230, b: 230);
        Paint(rgba, width, height, 0.22, 0.12, 0.78, 0.42, r: 100, g: 90, b: 70);
        Paint(rgba, width, height, 0.25, 0.07, 0.75, 0.18, r: 240, g: 240, b: 240);
        Paint(rgba, width, height, 0.03, 0.39, 0.97, 0.92, r: 235, g: 240, b: 235);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    private static byte[] Fill(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < rgba.Length; index += 4)
        {
            rgba[index] = r;
            rgba[index + 1] = g;
            rgba[index + 2] = b;
            rgba[index + 3] = 255;
        }
        return rgba;
    }

    private static void Paint(
        byte[] rgba, int width, int height,
        double left, double top, double right, double bottom, byte r, byte g, byte b)
    {
        var x0 = (int)(width * left);
        var x1 = (int)(width * right);
        var y0 = (int)(height * top);
        var y1 = (int)(height * bottom);
        for (var y = y0; y < y1; y++)
        for (var x = x0; x < x1; x++)
        {
            var offset = (y * width + x) * 4;
            rgba[offset] = r;
            rgba[offset + 1] = g;
            rgba[offset + 2] = b;
            rgba[offset + 3] = 255;
        }
    }

    private static void PaintPoint(
        byte[] rgba, int width, int height, int x, int y, byte r, byte g, byte b, int radius)
    {
        var x0 = Math.Max(0, x - radius);
        var x1 = Math.Min(width, x + radius);
        var y0 = Math.Max(0, y - radius);
        var y1 = Math.Min(height, y + radius);
        for (var py = y0; py < y1; py++)
        for (var px = x0; px < x1; px++)
        {
            var offset = (py * width + px) * 4;
            rgba[offset] = r;
            rgba[offset + 1] = g;
            rgba[offset + 2] = b;
            rgba[offset + 3] = 255;
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
