namespace PogoInventory.HeaderText;

/// <summary>
/// Pure BGRA8 pixel-buffer binarization for the CP crop, kept free of the
/// WinRT-only PogoInventory.HeaderOcr project so it is directly testable.
/// WinRT OCR drops digits in thin white CP text over busy game backgrounds;
/// converting the crop to black-text-on-white first removes that background
/// noise before recognition.
/// </summary>
public static class HeaderOcrBinarization
{
    /// <summary>Rec. 601 luma from a BGRA8 pixel (byte order B, G, R, A).</summary>
    public static byte Luminance(byte b, byte g, byte r) =>
        (byte)Math.Clamp((int)Math.Round(0.114 * b + 0.587 * g + 0.299 * r), 0, 255);

    /// <summary>
    /// Otsu's method: the luminance split point that maximizes between-class
    /// variance over the pixel histogram. Adapts to each crop instead of a
    /// single fixed brightness cutoff, since game backgrounds vary a lot.
    /// </summary>
    public static byte ComputeOtsuThreshold(IReadOnlyList<byte> luminances)
    {
        ArgumentNullException.ThrowIfNull(luminances);
        if (luminances.Count == 0) return 128;

        var histogram = new int[256];
        foreach (var value in luminances) histogram[value]++;

        var total = luminances.Count;
        double sumAll = 0;
        for (var i = 0; i < 256; i++) sumAll += i * (double)histogram[i];

        double sumBackground = 0;
        var weightBackground = 0;
        double bestVariance = -1;
        // Track the whole run of thresholds tied for the best variance (not
        // just the first): a flat gap between two clean clusters ties across
        // the whole gap, and the midpoint of that run is the split point that
        // actually sits between the clusters, not at the low cluster's edge.
        var bestRunStart = 0;
        var bestRunEnd = 0;

        for (var t = 0; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0) continue;
            var weightForeground = total - weightBackground;
            if (weightForeground == 0) break;

            sumBackground += t * (double)histogram[t];
            var meanBackground = sumBackground / weightBackground;
            var meanForeground = (sumAll - sumBackground) / weightForeground;

            var betweenVariance = (double)weightBackground * weightForeground *
                Math.Pow(meanBackground - meanForeground, 2);

            if (betweenVariance > bestVariance)
            {
                bestVariance = betweenVariance;
                bestRunStart = bestRunEnd = t;
            }
            else if (betweenVariance == bestVariance)
            {
                bestRunEnd = t;
            }
        }

        return (byte)((bestRunStart + bestRunEnd) / 2);
    }

    /// <summary>
    /// Converts a BGRA8 pixel buffer to black-text-on-white: pixels at or
    /// above <paramref name="threshold"/> luminance (bright header text)
    /// become black, everything else becomes white. Alpha is forced opaque.
    /// </summary>
    public static byte[] Binarize(byte[] bgra, int width, int height, int stride, byte threshold)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (stride < width * 4) throw new ArgumentOutOfRangeException(nameof(stride));
        if (bgra.Length < height * stride) throw new ArgumentException("Buffer too small for width/height/stride.", nameof(bgra));

        var output = new byte[bgra.Length];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = rowStart + x * 4;
                var luminance = Luminance(bgra[offset], bgra[offset + 1], bgra[offset + 2]);
                var value = luminance >= threshold ? (byte)0 : (byte)255;
                output[offset] = value;
                output[offset + 1] = value;
                output[offset + 2] = value;
                output[offset + 3] = 255;
            }
        }
        return output;
    }

    /// <summary>Computes the Otsu threshold directly from a BGRA8 buffer.</summary>
    public static byte ComputeThreshold(byte[] bgra, int width, int height, int stride)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        var luminances = new byte[width * height];
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = rowStart + x * 4;
                luminances[index++] = Luminance(bgra[offset], bgra[offset + 1], bgra[offset + 2]);
            }
        }
        return ComputeOtsuThreshold(luminances);
    }
}
