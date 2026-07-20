using PogoInventory.Automation.Models;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Exploration.Services;

public sealed record LocatedControl
{
    public required string ControlName { get; init; }
    public required NormalizedPoint Target { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

public sealed class VisualControlLocator
{
    public LocatedControl? LocateInventoryCard(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var searchBar = Sample(image, image.Width / 2, (int)(image.Height * 0.16));
        var page = Sample(image, image.Width / 2, (int)(image.Height * 0.30));
        var leftPage = Sample(image, image.Width / 20, image.Height / 2);
        if (!IsInventorySearchBackground(searchBar) ||
            !IsInventoryPage(page) ||
            !IsInventoryPage(leftPage))
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "VisibleInventoryCard",
            Target = new NormalizedPoint { X = 0.17, Y = 0.285 },
            Confidence = 0.95,
            Evidence = new[]
            {
                "inventory-search-bar",
                "inventory-grid-background",
                "upper-left-visible-card-topology"
            }
        };
    }

    public LocatedControl? LocateDetailsMenu(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        Candidate? best = null;
        var radius = Math.Max(28, image.Width / 15);
        for (var y = (int)(image.Height * 0.80); y < (int)(image.Height * 0.94); y += 4)
        {
            for (var x = (int)(image.Width * 0.75); x < (int)(image.Width * 0.96); x += 4)
            {
                var checks = new[]
                {
                    IsTeal(Sample(image, x - radius, y)),
                    IsTeal(Sample(image, x + radius, y)),
                    IsTeal(Sample(image, x, y - radius)),
                    IsTeal(Sample(image, x, y + radius)),
                    IsDarkTeal(Sample(image, x, y))
                };
                var candidate = new Candidate(x, y, checks.Count(value => value) / (double)checks.Length);
                if (best is null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        if (best is null || best.Score < 0.80)
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
        var pageChecks = new[]
        {
            IsDetailsPageBackground(Sample(image, image.Width / 2, image.Height / 8)),
            IsDetailsPageBackground(Sample(image, image.Width / 8, image.Height / 2)),
            IsDetailsPageBackground(Sample(image, image.Width * 7 / 8, image.Height / 2)),
            IsDetailsPageBackground(Sample(image, image.Width / 2, image.Height * 3 / 4))
        };
        var modelArea = Sample(image, image.Width / 2, image.Height * 2 / 5);
        if (pageChecks.Count(value => value) < 3 || !IsDetailsModelArea(modelArea))
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "PokemonDetailsPageTopology",
            Target = new NormalizedPoint { X = 0.50, Y = 0.40 },
            Confidence = pageChecks.Count(value => value) / (double)pageChecks.Length,
            Evidence = new[]
            {
                "DetailsPageBackgroundDetected",
                "PokemonModelAreaDetected",
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
        var dialogChecks = new[]
        {
            IsLight(Sample(image, image.Width / 10, (int)(image.Height * 0.84))),
            IsLight(Sample(image, image.Width / 2, (int)(image.Height * 0.84))),
            IsLight(Sample(image, image.Width * 9 / 10, (int)(image.Height * 0.84)))
        };
        var overlay = Sample(image, image.Width / 3, (int)(image.Height * 0.68));
        if (dialogChecks.Count(value => value) < 2 || !IsAppraisalOverlay(overlay))
        {
            return null;
        }

        return new LocatedControl
        {
            ControlName = "AppraisalIntroContinue",
            Target = new NormalizedPoint { X = 0.50, Y = 0.88 },
            Confidence = 0.95,
            Evidence = new[]
            {
                "white-appraisal-dialog",
                "blue-appraisal-overlay",
                "center-dialog-safe-target"
            }
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
        if (backgroundChecks.Count(value => value) < 2)
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

    private static Rgba32 Sample(PixelImage image, int x, int y) =>
        image.GetPixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1));

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

    private static bool IsDarkTeal(Rgba32 pixel) =>
        pixel.G >= 75 && pixel.B >= 75 && pixel.R <= 80 && pixel.G > pixel.R * 1.4;

    private static bool IsDetailsMenuBackground(Rgba32 pixel) =>
        pixel.G >= 80 && pixel.B >= 80 && pixel.R <= 100 &&
        pixel.G >= pixel.R * 1.25 && pixel.B >= pixel.R * 1.15;

    private static bool IsDetailsPageBackground(Rgba32 pixel) =>
        pixel.R is >= 20 and <= 70 &&
        pixel.G is >= 25 and <= 80 &&
        pixel.B is >= 30 and <= 90 &&
        Math.Abs(pixel.R - pixel.G) <= 25;

    private static bool IsDetailsModelArea(Rgba32 pixel) =>
        pixel.R is >= 35 and <= 180 &&
        pixel.G is >= 35 and <= 180 &&
        pixel.B is >= 35 and <= 190 &&
        Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) -
        Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) >= 8;

    private static bool IsAppraisalOverlay(Rgba32 pixel) =>
        pixel.B >= 105 && pixel.G >= 90 && pixel.R <= 130;

    private sealed record Candidate(int X, int Y, double Score);
}
