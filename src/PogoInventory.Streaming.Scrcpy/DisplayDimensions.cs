using System.Globalization;
using System.Text.RegularExpressions;

namespace PogoInventory.Streaming.Scrcpy;

public sealed record DisplayDimensions(int Width, int Height, string Orientation)
{
    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width));
        }
    }
}

public sealed record ResolvedStreamDimensions(int Width, int Height, string Source, string Orientation)
{
    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width));
        }
    }
}

public static class AdbDisplayDimensionParser
{
    private static readonly Regex SizePattern = new(
        @"(?<width>\d+)\s*x\s*(?<height>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static DisplayDimensions ParseWmSize(string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        var matches = SizePattern.Matches(output);
        if (matches.Count == 0)
        {
            throw new FormatException("ADB wm size output did not contain a usable WxH value.");
        }

        var match = matches[^1];
        var width = int.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture);
        var height = int.Parse(match.Groups["height"].Value, CultureInfo.InvariantCulture);
        var dimensions = new DisplayDimensions(width, height, width >= height ? "Landscape" : "Portrait");
        dimensions.Validate();
        return dimensions;
    }
}

public static class StreamDimensionResolver
{
    public static ResolvedStreamDimensions Resolve(
        DisplayDimensions display,
        int maxSize,
        int? requestedWidth = null,
        int? requestedHeight = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        display.Validate();
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize));
        }
        if ((requestedWidth is null) != (requestedHeight is null))
        {
            throw new StreamTransportException(new(
                StreamFailureCode.StreamDimensionMismatch,
                "Width and height overrides must be supplied together."));
        }

        if (requestedWidth is { } width && requestedHeight is { } height)
        {
            var explicitDimensions = new ResolvedStreamDimensions(width, height, "ExplicitOverride", display.Orientation);
            explicitDimensions.Validate();
            return explicitDimensions;
        }

        var scale = Math.Min(1d, maxSize / (double)Math.Max(display.Width, display.Height));
        var resolvedWidth = Math.Max(1, (int)Math.Round(display.Width * scale));
        var resolvedHeight = Math.Max(1, (int)Math.Round(display.Height * scale));
        var automatic = new ResolvedStreamDimensions(resolvedWidth, resolvedHeight, "AdbWmSize", display.Orientation);
        automatic.Validate();
        return automatic;
    }
}
