using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public sealed record VisibleTagRow
{
    public required NormalizedRegion Region { get; init; }
    public required NormalizedPoint Target { get; init; }
    public required bool IsSelected { get; init; }
}

public sealed record TagSelectionMatch
{
    public required string TagName { get; init; }
    public required VisibleTagRow Row { get; init; }
    public required double Confidence { get; init; }
    public required int VisibleRowCount { get; init; }
}

/// <summary>
/// Locates visible tag rows geometrically and identifies a requested tag by a
/// device-calibrated visual name template. Row order is never an input.
/// </summary>
public sealed class TagSelector
{
    private const double TextLeft = 0.14;
    private const double TextWidth = 0.44;
    private const double RowHeight = 0.050;

    public bool IsSelectorVisible(byte[] screenshotPng, TagSelectorProfile profile)
    {
        var image = PngDecoder.Decode(screenshotPng);
        EnsureGeometry(image, profile);
        return FindVisibleRows(image).Count >= 4;
    }

    public TagSelectionMatch? FindByName(
        byte[] screenshotPng,
        string tagName,
        TagSelectorProfile profile,
        string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var candidates = FindCandidatesByName(screenshotPng, tagName, profile, profilePath);
        var best = candidates.FirstOrDefault();
        var margin = best is null
            ? 0
            : best.Confidence - (candidates.Skip(1).FirstOrDefault()?.Confidence ?? 0);
        return best is not null && best.Confidence >= profile.MinimumMatchConfidence &&
            margin >= profile.MinimumMatchMargin
            ? best
            : null;
    }

    public TagSelectionMatch? FindBestByName(
        byte[] screenshotPng,
        string tagName,
        TagSelectorProfile profile,
        string profilePath)
    {
        return FindCandidatesByName(screenshotPng, tagName, profile, profilePath)
            .FirstOrDefault();
    }

    public IReadOnlyList<TagSelectionMatch> FindCandidatesByName(
        byte[] screenshotPng,
        string tagName,
        TagSelectorProfile profile,
        string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var configured = profile.Templates.SingleOrDefault(item =>
            string.Equals(item.Name, tagName, StringComparison.OrdinalIgnoreCase));
        if (configured is null)
        {
            return Array.Empty<TagSelectionMatch>();
        }

        var image = PngDecoder.Decode(screenshotPng);
        EnsureGeometry(image, profile);
        if (!IsSelectorVisible(screenshotPng, profile))
        {
            return Array.Empty<TagSelectionMatch>();
        }
        var sourcePath = TagSelectorProfile.ResolveSourcePath(profilePath, configured.SourceImage);
        var templateImage = PngDecoder.Decode(File.ReadAllBytes(sourcePath));
        var templateSignature = Signature(templateImage, configured.Region, IsDarkText);
        var rows = FindVisibleRows(image);
        return rows.Select(row =>
            {
                // Minor row-height differences occur across density/font rendering.
                // Compare normalized signatures at bounded scales without using the
                // row's ordinal or a fixed row coordinate.
                var confidence = new[] { 0.94, 1.00, 1.06 }
                    .Select(scale => Signature(
                        image, ScaleHeight(row.Region, scale), IsDarkText))
                    .Select(signature => Compare(templateSignature, signature))
                    .Max();
                return new TagSelectionMatch
                {
                    TagName = configured.Name,
                    Row = row,
                    Confidence = confidence,
                    VisibleRowCount = rows.Count
                };
            })
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();
    }

    public IReadOnlyList<VisibleTagRow> FindVisibleRows(byte[] screenshotPng) =>
        FindVisibleRows(PngDecoder.Decode(screenshotPng));

    public bool IsDoneVisible(byte[] screenshotPng, TagSelectorProfile profile)
    {
        var image = PngDecoder.Decode(screenshotPng);
        EnsureGeometry(image, profile);
        return GreenRatio(image, 0.27, 0.76, 0.73, 0.82) >= 0.10;
    }

    public bool HasDetailsTagPill(byte[] screenshotPng) =>
        CountDetailsTagPills(screenshotPng) > 0;

    public int CountDetailsTagPills(byte[] screenshotPng)
    {
        var image = PngDecoder.Decode(screenshotPng);
        var visited = new bool[image.Width * image.Height];
        var left = (int)(image.Width * 0.20);
        var right = (int)(image.Width * 0.80);
        var top = (int)(image.Height * 0.42);
        var bottom = (int)(image.Height * 0.62);
        var components = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var start = (y * image.Width) + x;
                if (visited[start] || !IsTagPillFill(image.GetPixel(x, y)))
                {
                    continue;
                }
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[start] = true;
                var count = 0;
                var minX = x;
                var maxX = x;
                var minY = y;
                var maxY = y;
                while (queue.Count > 0)
                {
                    var point = queue.Dequeue();
                    count++;
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                    foreach (var next in new[]
                    {
                        (point.X - 1, point.Y), (point.X + 1, point.Y),
                        (point.X, point.Y - 1), (point.X, point.Y + 1)
                    })
                    {
                        if (next.Item1 < left || next.Item1 >= right ||
                            next.Item2 < top || next.Item2 >= bottom)
                        {
                            continue;
                        }
                        var index = (next.Item2 * image.Width) + next.Item1;
                        if (!visited[index] && IsTagPillFill(image.GetPixel(next.Item1, next.Item2)))
                        {
                            visited[index] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
                if (count >= 900 && maxX - minX >= image.Width * 0.10 &&
                    maxY - minY >= image.Height * 0.012)
                {
                    components++;
                }
            }
        }
        return components;
    }

    private static IReadOnlyList<VisibleTagRow> FindVisibleRows(PixelImage image)
    {
        var activePixels = new List<int>();
        var left = (int)(image.Width * 0.07);
        var right = (int)(image.Width * 0.12);
        for (var y = (int)(image.Height * 0.27); y < (int)(image.Height * 0.69); y++)
        {
            var marker = 0;
            for (var x = left; x < right; x += 3)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.R < 245 || pixel.G < 245 || pixel.B < 245)
                {
                    marker++;
                }
            }
            if (marker >= 5)
            {
                activePixels.Add(y);
            }
        }

        var bands = new List<(int Start, int End)>();
        foreach (var y in activePixels)
        {
            if (bands.Count == 0 || y - bands[^1].End > 3)
            {
                bands.Add((y, y));
            }
            else
            {
                bands[^1] = (bands[^1].Start, y);
            }
        }
        return bands
            .Where(band => band.End - band.Start >= 8)
            .Select(band => (band.Start + band.End) / 2.0 / image.Height)
            .Where(center => center is >= 0.28 and <= 0.68)
            .Select(center =>
            {
                var region = new NormalizedRegion
                {
                    X = TextLeft,
                    Y = Math.Max(0, center - (RowHeight / 2)),
                    Width = TextWidth,
                    Height = RowHeight
                };
                return new VisibleTagRow
                {
                    Region = region,
                    Target = new NormalizedPoint { X = 0.50, Y = center },
                    IsSelected = GreenRatio(
                        image, 0.82, center - 0.028, 0.94, center + 0.028) >= 0.008
                };
            })
            .ToArray();
    }

    private static double[] Signature(
        PixelImage image,
        NormalizedRegion region,
        Func<Rgba32, bool> predicate)
    {
        const int width = 88;
        const int height = 20;
        var result = new double[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var x = (int)(image.Width *
                    (region.X + ((column + 0.5) * region.Width / width)));
                var y = (int)(image.Height *
                    (region.Y + ((row + 0.5) * region.Height / height)));
                result[(row * width) + column] = predicate(image.GetPixel(
                    Math.Clamp(x, 0, image.Width - 1),
                    Math.Clamp(y, 0, image.Height - 1))) ? 1 : 0;
            }
        }
        return result;
    }

    private static double Compare(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
    {
        var intersection = 0.0;
        var expectedInk = 0.0;
        var actualInk = 0.0;
        for (var index = 0; index < expected.Count; index++)
        {
            intersection += Math.Min(expected[index], actual[index]);
            expectedInk += expected[index];
            actualInk += actual[index];
        }
        return expectedInk + actualInk == 0
            ? 0
            : (2 * intersection) / (expectedInk + actualInk);
    }

    private static NormalizedRegion ScaleHeight(NormalizedRegion region, double scale)
    {
        var height = Math.Min(1, region.Height * scale);
        var center = region.Y + (region.Height / 2);
        return new NormalizedRegion
        {
            X = region.X,
            Y = Math.Clamp(center - (height / 2), 0, 1 - height),
            Width = region.Width,
            Height = height
        };
    }

    private static double GreenRatio(
        PixelImage image, double x1, double y1, double x2, double y2)
    {
        var green = 0;
        var total = 0;
        for (var y = (int)(image.Height * y1); y < (int)(image.Height * y2); y += 3)
        {
            for (var x = (int)(image.Width * x1); x < (int)(image.Width * x2); x += 3)
            {
                total++;
                var pixel = image.GetPixel(x, y);
                if (pixel.G >= 140 && pixel.G >= pixel.R + 5 && pixel.G >= pixel.B - 25)
                {
                    green++;
                }
            }
        }
        return total == 0 ? 0 : green / (double)total;
    }

    private static bool IsDarkText(Rgba32 pixel) =>
        pixel.R <= 100 && pixel.G is >= 75 and <= 150 && pixel.B is >= 80 and <= 165;

    private static bool IsTagGreen(Rgba32 pixel) =>
        pixel.G >= 150 && pixel.G >= pixel.R + 15 && pixel.G >= pixel.B + 5;

    private static bool IsTagPillFill(Rgba32 pixel)
    {
        var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        var gray = maximum - minimum <= 24 && maximum is >= 145 and <= 225;
        var colored = maximum is >= 125 and <= 235 && maximum - minimum >= 18;
        return gray || colored;
    }

    private static void EnsureGeometry(PixelImage image, TagSelectorProfile profile)
    {
        if (image.Width != profile.ScreenWidth || image.Height != profile.ScreenHeight)
        {
            throw new InvalidOperationException(
                $"Tag selector profile geometry {profile.ScreenWidth}x{profile.ScreenHeight} " +
                $"does not match screenshot {image.Width}x{image.Height}.");
        }
    }
}
