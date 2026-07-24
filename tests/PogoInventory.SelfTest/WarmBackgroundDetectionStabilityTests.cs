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
/// under local-data/ and found UNSAFE on its own: roughly 200 real frames
/// confidently classified as MainMenu, Appraisal, Inventory or GameplayMap by
/// the existing (trusted) detector pipeline also satisfy those three floors --
/// several with cpArea/detailsPanel magnitudes (0.95-1.00) indistinguishable
/// from the five genuine warm-background Details frames (0.958-0.998 /
/// 0.978). No threshold tuning within just these three regions separates
/// the two populations, and at least one call site
/// (<c>AndroidVerifiedInventoryNamedOperations.CaptureCleanupIdentityAsync</c>)
/// treated a non-null topology result as "this is PokemonDetails" with NO
/// additional state gating. Task I then measured a fourth, orthogonal
/// corroboration signal (<see cref="VisualControlLocator.LocateCanonicalCloseControl"/>)
/// against the full corpus and found it discriminates perfectly (0/168
/// colliders pass it; all 5 evidence frames score 0.975). Task J implements
/// the quadruple-corroborated branch (three area floors + the X signal) and
/// tightens the two call sites to require Unknown, not "any non-PokemonDetails
/// state", before trusting the topology fallback. See task-H-report.md,
/// task-I-report.md and task-J-report.md, and
/// <c>CanonicalCloseCorroborationTests</c> for the new branch's coverage.
///
/// This test file documents that the existing (measured-safe) *primary* gate
/// on <see cref="VisualControlLocator.LocateDetailsPageTopology"/> is
/// unchanged, alongside the change (2) MainMenu regression coverage. The
/// degenerate-model-area test below now also carries the canonical-close X
/// and strong cp/panel coverage so its null result is attributable to the
/// modelArea axis specifically (see Task J review nit fix-in-passing).
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
    /// High CP/name-band and details-panel coverage (each independently
    /// satisfying the relaxed branch's own >= 0.50 floors) with a degenerate
    /// model area (spread-based <c>IsDetailsModelArea</c> never matches on
    /// this frame -> ~0 coverage, below both the 0.15 primary floor and the
    /// 0.08 relaxed floor) AND the canonical-close X present -- every other
    /// signal the relaxed branch (Task J) needs is satisfied -- must still
    /// return null. This isolates that the null is attributable to the
    /// modelArea floor specifically, not to some other missing signal (the
    /// pre-Task-J version of this test was over-determined: its frame also
    /// failed the unrelated blue-header fallback for reasons that had
    /// nothing to do with model area).
    /// </summary>
    private static void DetailsTopologyDegenerateFrameStillNull()
    {
        var frame = CreateDegenerateModelAreaFrame(includeCanonicalClose: true);
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is null,
            "a frame with high cp/panel coverage, the canonical-close X, but a degenerate model area must remain unclassified");
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
    /// so modelArea measures 0.0. Optionally draws a synthetic
    /// canonical-close X (dark-teal circular shell with crossing light-green
    /// strokes) in the lower-centre safe zone so Task J's relaxed branch has
    /// every OTHER corroboration signal available, isolating the modelArea
    /// floor as the reason for a null result.
    /// </summary>
    private static byte[] CreateDegenerateModelAreaFrame(bool includeCanonicalClose = false)
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
        // cx=148, cy=494 land exactly on LocateCanonicalCloseControl's
        // candidate scan grid (x step 4 from width*0.32, y step 2 from
        // height*0.74) for this 300x600 canvas, while still sitting in its
        // lower-centre safe zone (x 0.45-0.55, y 0.76-0.91).
        if (includeCanonicalClose)
            DrawCanonicalClose(rgba, width, height, cx: 148, cy: 494, radius: 13);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    /// <summary>
    /// Draws a dark-teal circular shell with crossing light-green X strokes
    /// matching the shapes <c>IsCanonicalShell</c>/<c>IsCanonicalStroke</c>
    /// require, in the lower-centre safe zone (x 0.45-0.55, y 0.76-0.91)
    /// <see cref="VisualControlLocator.LocateCanonicalCloseControl"/> scores.
    /// </summary>
    private static void DrawCanonicalClose(byte[] rgba, int width, int height, int cx, int cy, int radius)
    {
        for (var y = cy - radius - 2; y <= cy + radius + 2; y++)
        for (var x = cx - radius - 2; x <= cx + radius + 2; x++)
        {
            var distance = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));
            if (distance is >= 11 and <= 15)
                SetPixel(rgba, width, height, x, y, r: 0, g: 165, b: 175);
        }
        for (var offset = -9; offset <= 9; offset++)
        {
            SetPixel(rgba, width, height, cx + offset, cy + offset, r: 190, g: 235, b: 205);
            SetPixel(rgba, width, height, cx + offset, cy + offset + 1, r: 190, g: 235, b: 205);
            SetPixel(rgba, width, height, cx + offset, cy - offset, r: 190, g: 235, b: 205);
            SetPixel(rgba, width, height, cx + offset, cy - offset + 1, r: 190, g: 235, b: 205);
        }
    }

    private static void SetPixel(byte[] rgba, int width, int height, int x, int y, byte r, byte g, byte b)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        var offset = (y * width + x) * 4;
        rgba[offset] = r;
        rgba[offset + 1] = g;
        rgba[offset + 2] = b;
        rgba[offset + 3] = 255;
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
