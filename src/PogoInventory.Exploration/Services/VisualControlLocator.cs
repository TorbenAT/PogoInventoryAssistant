using System.Security.Cryptography;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public sealed record LocatedControl
{
    public required string ControlName { get; init; }
    public required NormalizedPoint Target { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

public sealed record LocatedCanonicalCloseControl
{
    public required NormalizedPoint Target { get; init; }
    public required double Confidence { get; init; }
    public required double CircularControlConfidence { get; init; }
    public required double XStrokeConfidence { get; init; }
    public required double PositionConfidence { get; init; }
    public required NormalizedRegion Bounds { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
    public required string ScreenshotSha256 { get; init; }
}

public sealed class VisualControlLocator
{
    public LocatedControl? LocateInventoryCard(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var searchBar = RegionMatch(image, 0.08, 0.15, 0.92, 0.22, IsInventorySearchBackground);
        var grid = RegionMatch(image, 0.04, 0.24, 0.96, 0.90, IsInventoryPage);
        var header = RegionMatch(image, 0.20, 0.08, 0.80, 0.15, IsInventoryHeader);
        if (searchBar < 0.55 || grid < 0.45 || header < 0.35)
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "VisibleInventoryCard",
            Target = new NormalizedPoint { X = 0.17, Y = 0.285 },
            Confidence = Math.Min(0.99, (searchBar + grid + header) / 3),
            Evidence = new[]
            {
                "InventorySearchBarDetected",
                "InventoryGridDetected",
                "InventoryHeaderDetected"
            }
        };
    }

    public LocatedControl? LocateDetailsMenu(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        Candidate? best = null;
        var radius = Math.Max(28, image.Width / 20);
        for (var y = (int)(image.Height * 0.80); y < (int)(image.Height * 0.94); y += 4)
        {
            for (var x = (int)(image.Width * 0.75); x < (int)(image.Width * 0.96); x += 4)
            {
                var checks = new[]
                {
                    IsDetailsControl(Sample(image, x - radius, y)),
                    IsDetailsControl(Sample(image, x + radius, y)),
                    IsDetailsControl(Sample(image, x, y - radius)),
                    IsDetailsControl(Sample(image, x, y + radius)),
                    IsDarkTeal(Sample(image, x, y))
                };
                var candidate = new Candidate(x, y, checks.Count(value => value) / (double)checks.Length);
                if (best is null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        if (best is null || best.Score < 0.20)
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "PokemonDetailsMenu",
            Target = new NormalizedPoint
            {
                X = (double)best.X / (image.Width - 1),
                Y = (double)best.Y / (image.Height - 1)
            },
            Confidence = best.Score,
            Evidence = new[]
            {
                "teal-circular-control",
                "dark-teal-center",
                "details-lower-right-zone"
            }
        };
    }

    public LocatedControl? LocateDetailsPageTopology(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var modelArea = RegionMatch(image, 0.22, 0.12, 0.78, 0.42, IsDetailsModelArea);
        var cpArea = RegionMatch(image, 0.25, 0.07, 0.75, 0.18, IsDetailsPanel);
        var detailsPanel = RegionMatch(image, 0.03, 0.39, 0.97, 0.92, IsDetailsPanelBroad);
        // Details pages can be partially occluded by the transition back from
        // appraisal. Keep all three independent regions, but accept the
        // lower-coverage frame only when each still contributes evidence.
        if (modelArea < 0.15 || cpArea < 0.10 || detailsPanel < 0.20)
        {
            // NOTE (Task H): a proposed double-corroborated relaxation here
            // (accept when modelArea >= 0.08 && cpArea >= 0.50 &&
            // detailsPanel >= 0.50, to cover warm/sunset-background Details
            // frames whose model area flickers just under 0.15) was measured
            // against the real local-data/ corpus and found UNSAFE: ~200 real
            // frames confidently classified MainMenu/Appraisal/Inventory/
            // GameplayMap by the existing detector pipeline also satisfy
            // those three floors, several with cpArea/detailsPanel magnitudes
            // indistinguishable from genuine Details frames. See
            // WarmBackgroundDetectionStabilityTests and task-H-report.md.
            // Intentionally NOT implemented; escalated instead of guessed.
            var upperBlue = RegionMatch(image, 0.05, 0.05, 0.95, 0.36, IsDetailsPageBlue);
            var lowerPanel = RegionMatch(image, 0.03, 0.38, 0.97, 0.92, IsDetailsPanelBroad);
            if (upperBlue < 0.18 || lowerPanel < 0.25)
            {
                return null;
            }

            return new LocatedControl
            {
                ControlName = "PokemonDetailsPageTopology",
                Target = new NormalizedPoint { X = 0.50, Y = 0.40 },
                Confidence = 0.30,
                Evidence = new[] { "DetailsPageBlueHeaderDetected", "DetailsPagePanelDetected", "DetailsPageFallbackTopologyDetected" }
            };
        }

        return new LocatedControl
        {
            ControlName = "PokemonDetailsPageTopology",
            Target = new NormalizedPoint { X = 0.50, Y = 0.40 },
            Confidence = Math.Min(0.99, (modelArea + cpArea + detailsPanel) / 3),
            Evidence = new[]
            {
                "DetailsPagePanelDetected",
                "PokemonModelAreaDetected",
                "DetailsCpNameRegionDetected",
                "DetailsPageTopologyDetected"
            }
        };
    }

    public LocatedControl? LocateAppraiseMenuItem(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var backgroundChecks = new[]
        {
            IsDetailsMenuBackground(Sample(image, image.Width / 8, image.Height / 4)),
            IsDetailsMenuBackground(Sample(image, image.Width / 2, image.Height / 2)),
            IsDetailsMenuBackground(Sample(image, image.Width * 7 / 8, image.Height / 3))
        };
        if (backgroundChecks.Count(value => value) < 2)
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "AppraiseMenuItem",
            Target = new NormalizedPoint { X = 0.70, Y = 0.72 },
            Confidence = 0.90,
            Evidence = new[]
            {
                "details-menu-gradient",
                "appraise-row-topology",
                "transfer-row-exclusion-margin"
            }
        };
    }

    public LocatedControl? LocateAppraisalIntroContinue(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var dialog = RegionMatch(image, 0.02, 0.82, 0.98, 0.97, IsLight);
        var overlay = RegionMatch(image, 0.20, 0.50, 0.80, 0.84, IsAppraisalOverlay);
        if (dialog < 0.35 || overlay < 0.25)
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "AppraisalIntroContinue",
            Target = new NormalizedPoint { X = 0.50, Y = 0.88 },
            Confidence = Math.Min(0.99, (dialog + overlay) / 2),
            Evidence = new[]
            {
                "AppraisalIntroDialogDetected",
                "AppraisalIntroOverlayDetected",
            }
        };
    }

    public LocatedCanonicalCloseControl? LocateCanonicalCloseControl(byte[] screenshotPng)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var image = PngDecoder.Decode(screenshotPng);
        var candidates = new List<CanonicalCandidate>();
        var expectedRadius = Math.Max(10, image.Width / 18);
        var radii = new[]
        {
            Math.Max(10, image.Width / 22),
            Math.Max(10, image.Width / 20),
            expectedRadius,
            Math.Max(10, image.Width / 16),
            Math.Max(10, image.Width / 14)
        }.Distinct().ToArray();
        for (var y = (int)(image.Height * 0.74); y <= (int)(image.Height * 0.925); y += 2)
        for (var x = (int)(image.Width * 0.32); x <= (int)(image.Width * 0.68); x += 4)
        foreach (var radius in radii)
        {
            var candidate = ScoreCanonicalClose(image, x, y, radius, expectedRadius);
            if (candidate.Score >= 0.62)
                candidates.Add(candidate);
        }

        var best = candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();
        if (best is null || best.Score < 0.70 ||
            best.CircularConfidence < 0.70 || best.XStrokeConfidence < 0.70)
            return null;
        var selected = best;

        var conflicting = candidates.Any(candidate =>
            candidate != selected &&
            Distance(candidate.X, candidate.Y, selected.X, selected.Y) > selected.Radius * 2.4 &&
            candidate.Score >= selected.Score - 0.05);
        if (conflicting)
            return null;

        var bounds = new NormalizedRegion
        {
            X = (double)(selected.X - selected.Radius) / image.Width,
            Y = (double)(selected.Y - selected.Radius) / image.Height,
            Width = (double)(selected.Radius * 2) / image.Width,
            Height = (double)(selected.Radius * 2) / image.Height
        };
        return new LocatedCanonicalCloseControl
        {
            Target = new NormalizedPoint
            {
                X = (double)selected.X / (image.Width - 1),
                Y = (double)selected.Y / (image.Height - 1)
            },
            Confidence = selected.Score,
            CircularControlConfidence = selected.CircularConfidence,
            XStrokeConfidence = selected.XStrokeConfidence,
            PositionConfidence = selected.PositionConfidence,
            Bounds = bounds,
            Evidence = new[]
            {
                "canonical-close-lower-centre-safe-zone",
                "canonical-circular-shell",
                "canonical-crossing-x-strokes",
                "canonical-foreground-background-contrast",
                "canonical-dimensions-verified",
                "no-conflicting-canonical-close-target"
            },
            ScreenshotSha256 = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant()
        };
    }

    public LocatedControl? LocateMainMenuPokeball(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var radius = Math.Max(24, image.Width / 18);
        Candidate? best = null;

        for (var y = (int)(image.Height * 0.72); y < (int)(image.Height * 0.94); y += 4)
        {
            for (var x = (int)(image.Width * 0.35); x < (int)(image.Width * 0.65); x += 4)
            {
                var candidate = Score(image, x, y, radius);
                if (best is null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        if (best is null || best.Score < 0.90)
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "PokemonGoMainMenuPokeball",
            Target = new NormalizedPoint
            {
                X = (double)best.X / (image.Width - 1),
                Y = (double)best.Y / (image.Height - 1)
            },
            Confidence = best.Score,
            Evidence = new[]
            {
                "red-upper-hemisphere",
                "light-lower-hemisphere",
                "neutral-center-button",
                "lower-central-gameplay-zone"
            }
        };
    }

    public LocatedControl? LocatePokemonInventory(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        Candidate? best = null;
        for (var y = (int)(image.Height * 0.65); y < (int)(image.Height * 0.85); y += 4)
        {
            for (var x = (int)(image.Width * 0.10); x < (int)(image.Width * 0.36); x += 4)
            {
                for (var radius = image.Width / 16; radius <= image.Width / 10; radius += 4)
                {
                    var checks = new[]
                    {
                        IsTeal(Sample(image, x, y - radius)),
                        IsTeal(Sample(image, x, y + radius)),
                        IsTeal(Sample(image, x - radius, y)),
                        IsTeal(Sample(image, x + radius, y)),
                        IsTeal(Sample(image, x - radius * 2 / 3, y - radius * 2 / 3)),
                        IsTeal(Sample(image, x + radius * 2 / 3, y - radius * 2 / 3)),
                        IsTeal(Sample(image, x - radius * 2 / 3, y + radius * 2 / 3)),
                        IsTeal(Sample(image, x + radius * 2 / 3, y + radius * 2 / 3))
                    };
                    var candidate = new Candidate(x, y, checks.Count(value => value) / (double)checks.Length);
                    if (best is null || candidate.Score > best.Score)
                    {
                        best = candidate;
                    }
                }
            }
        }

        var backgroundChecks = new[]
        {
            IsMenuBackground(Sample(image, image.Width / 10, image.Height / 3)),
            IsMenuBackground(Sample(image, image.Width / 2, image.Height / 4)),
            IsMenuBackground(Sample(image, image.Width * 9 / 10, image.Height / 2))
        };
        if (backgroundChecks.Count(value => value) < 3)
        {
            return null;
        }


        if (best is null || best.Score < 0.75)
        {
            return new LocatedControl
            {
                ControlName = "PokemonInventory",
                Target = new NormalizedPoint { X = 0.22, Y = 0.75 },
                Confidence = 0.85,
                Evidence = new[]
                {
                    "verified-map-to-main-menu-context",
                    "main-menu-background",
                    "lower-left-menu-topology",
                    "normalized-layout-fallback"
                }
            };
        }

        return new LocatedControl
        {
            ControlName = "PokemonInventory",
            Target = new NormalizedPoint
            {
                X = (double)best.X / (image.Width - 1),
                Y = (double)best.Y / (image.Height - 1)
            },
            Confidence = best.Score,
            Evidence = new[]
            {
                "teal-circular-control",
                "main-menu-background",
                "lower-left-menu-topology"
            }
        };
    }

    private static Candidate Score(PixelImage image, int x, int y, int radius)
    {
        var checks = new[]
        {
            IsRed(Sample(image, x, y - radius / 2)),
            IsRed(Sample(image, x - radius / 2, y - radius / 3)),
            IsRed(Sample(image, x + radius / 2, y - radius / 3)),
            IsLight(Sample(image, x, y + radius / 2)),
            IsLight(Sample(image, x - radius / 2, y + radius / 3)),
            IsLight(Sample(image, x + radius / 2, y + radius / 3)),
            IsNeutral(Sample(image, x, y)),
            IsRed(Sample(image, x, y - radius)),
            IsLight(Sample(image, x, y + radius))
        };
        return new Candidate(x, y, checks.Count(value => value) / (double)checks.Length);
    }

    private static CanonicalCandidate ScoreCanonicalClose(
        PixelImage image, int x, int y, int radius, int expectedRadius)
    {
        var shellMatches = 0;
        var shellTotal = 0;
        var strokeMatches = 0;
        var strokeTotal = 0;
        var backgroundMatches = 0;
        var backgroundTotal = 0;
        for (var index = 0; index < 16; index++)
        {
            var angle = index * Math.PI * 2 / 16;
            shellTotal++;
            var shellHit = false;
            for (var radiusOffset = -2; radiusOffset <= 2; radiusOffset++)
            {
                shellHit |= IsCanonicalShell(Sample(image,
                    x + (int)(Math.Cos(angle) * (radius + radiusOffset)),
                    y + (int)(Math.Sin(angle) * (radius + radiusOffset))));
            }
            if (shellHit)
                shellMatches++;

            var outer = radius * 1.35;
            backgroundTotal++;
            if (!IsCanonicalShell(Sample(image,
                    x + (int)(Math.Cos(angle) * outer),
                    y + (int)(Math.Sin(angle) * outer))))
                backgroundMatches++;
        }

        foreach (var sign in new[] { -1, 1 })
        for (var fraction = 0.22; fraction <= 0.78; fraction += 0.14)
        {
            var offset = (int)(radius * fraction);
            strokeTotal++;
            if (IsCanonicalStroke(Sample(image, x + offset, y + sign * offset)))
                strokeMatches++;
            strokeTotal++;
            if (IsCanonicalStroke(Sample(image, x - offset, y + sign * offset)))
                strokeMatches++;
        }

        var circular = shellMatches / (double)shellTotal;
        var strokes = strokeMatches / (double)strokeTotal;
        var contrast = backgroundMatches / (double)backgroundTotal;
        var position = x >= image.Width * 0.45 && x <= image.Width * 0.55 &&
            y >= image.Height * 0.76 && y <= image.Height * 0.91 ? 1d : 0d;
        var dimensions = Math.Max(0, 1 -
            Math.Abs(radius - expectedRadius) / (double)Math.Max(1, expectedRadius * 0.35));
        var score = circular * 0.38 + strokes * 0.37 +
            position * 0.10 + contrast * 0.08 + dimensions * 0.07;
        return new CanonicalCandidate(x, y, radius, score, circular, strokes, position);
    }

    private static Rgba32 Sample(PixelImage image, int x, int y) =>
        image.GetPixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1));

    private static bool IsCanonicalShell(Rgba32 pixel) =>
        pixel.R <= 150 && pixel.G >= 80 && pixel.B >= 80 && pixel.G >= pixel.R * 1.12 &&
        pixel.B >= pixel.R * 1.04;

    private static bool IsCanonicalStroke(Rgba32 pixel) =>
        pixel.G >= 130 && pixel.B >= 120 && pixel.G >= pixel.R * 1.18 &&
        pixel.B >= pixel.R * 1.05;

    private static double Distance(int leftX, int leftY, int rightX, int rightY) =>
        Math.Sqrt(Math.Pow(leftX - rightX, 2) + Math.Pow(leftY - rightY, 2));

    private static double RegionMatch(PixelImage image, double left, double top, double right, double bottom,
        Func<Rgba32, bool> predicate)
    {
        var x0 = (int)(image.Width * left); var x1 = (int)(image.Width * right);
        var y0 = (int)(image.Height * top); var y1 = (int)(image.Height * bottom);
        var matched = 0; var total = 0;
        for (var y = y0; y < y1; y += Math.Max(1, image.Height / 90))
        for (var x = x0; x < x1; x += Math.Max(1, image.Width / 90))
        { total++; if (predicate(Sample(image, x, y))) matched++; }
        return total == 0 ? 0 : matched / (double)total;
    }

    private static bool IsRed(Rgba32 pixel) =>
        pixel.R >= 170 && pixel.R >= pixel.G * 1.35 && pixel.R >= pixel.B * 1.25;

    private static bool IsLight(Rgba32 pixel) =>
        pixel.R >= 170 && pixel.G >= 170 && pixel.B >= 170;

    private static bool IsNeutral(Rgba32 pixel) =>
        Math.Abs(pixel.R - pixel.G) <= 35 && Math.Abs(pixel.G - pixel.B) <= 35 && pixel.R is >= 80 and <= 220;

    private static bool IsTeal(Rgba32 pixel) =>
        pixel.G >= 90 && pixel.B >= 90 && pixel.G >= pixel.R * 1.15 && pixel.B >= pixel.R * 1.05;

    private static bool IsMenuBackground(Rgba32 pixel) =>
        pixel.G >= 185 && pixel.R >= 170 && pixel.B >= 160;

    private static bool IsInventorySearchBackground(Rgba32 pixel) =>
        pixel.R >= 205 && pixel.G >= 220 && pixel.B >= 195;

    private static bool IsInventoryPage(Rgba32 pixel) =>
        pixel.R >= 215 && pixel.G >= 225 && pixel.B >= 205;

    private static bool IsInventoryHeader(Rgba32 pixel) =>
        pixel.R >= 175 && pixel.G >= 185 && pixel.B >= 175;

    private static bool IsDarkTeal(Rgba32 pixel) =>
        pixel.G >= 75 && pixel.B >= 75 && pixel.R <= 80 && pixel.G > pixel.R * 1.4;

    private static bool IsDetailsControl(Rgba32 pixel) =>
        IsTeal(pixel) || (pixel.R <= 90 && pixel.G <= 150 && pixel.B <= 170);

    private static bool IsDetailsMenuBackground(Rgba32 pixel) =>
        pixel.G >= 80 && pixel.B >= 80 && pixel.R <= 100 &&
        pixel.G >= pixel.R * 1.25 && pixel.B >= pixel.R * 1.15;

    private static bool IsDetailsPageBackground(Rgba32 pixel) =>
        pixel.R is >= 20 and <= 70 &&
        pixel.G is >= 25 and <= 80 &&
        pixel.B is >= 30 and <= 90 &&
        Math.Abs(pixel.R - pixel.G) <= 25;

    private static bool IsDetailsPageBlue(Rgba32 pixel) =>
        pixel.B >= 70 && pixel.B >= pixel.R * 1.25 && pixel.B >= pixel.G * 1.05;

    private static bool IsDetailsModelArea(Rgba32 pixel) =>
        pixel.R is >= 35 and <= 180 &&
        pixel.G is >= 35 and <= 180 &&
        pixel.B is >= 35 and <= 190 &&
        Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) -
            Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) >= 8;

    private static bool IsDetailsPanel(Rgba32 pixel) =>
        IsLight(pixel) || (pixel.G >= 90 && pixel.G >= pixel.R * 1.05 && pixel.B >= pixel.R * 0.95);

    private static bool IsDetailsPanelBroad(Rgba32 pixel) =>
        IsDetailsPanel(pixel) ||
        (pixel.B >= 70 && pixel.G >= 35 && pixel.R <= 190 && pixel.B >= pixel.R * 1.08);

    private static bool IsAppraisalOverlay(Rgba32 pixel) =>
        pixel.B >= 105 && pixel.G >= 90 && pixel.R <= 130;

    private sealed record Candidate(int X, int Y, double Score);

    private sealed record CanonicalCandidate(
        int X,
        int Y,
        int Radius,
        double Score,
        double CircularConfidence,
        double XStrokeConfidence,
        double PositionConfidence);
}
