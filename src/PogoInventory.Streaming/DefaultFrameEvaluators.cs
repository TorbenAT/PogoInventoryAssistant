namespace PogoInventory.Streaming;

public sealed class DefaultFrameQualityEvaluator : IFrameQualityEvaluator
{
    public FrameQuality Evaluate(in RawFrame frame, ReadOnlySpan<byte> previousPixels)
    {
        var pixels = frame.Pixels.Span;
        var descriptor = frame.Descriptor;
        if (descriptor.PixelFormat != FramePixelFormat.Bgra32 && descriptor.PixelFormat != FramePixelFormat.Rgba32)
        {
            return FrameQuality.Unknown;
        }

        var sampleStep = Math.Max(4, descriptor.Width / 180) * 4;
        double luminanceSum = 0;
        double gradientSum = 0;
        double differenceSum = 0;
        var samples = 0;

        for (var y = 1; y < descriptor.Height - 1; y += Math.Max(1, descriptor.Height / 320))
        {
            var row = y * descriptor.Stride;
            for (var x = sampleStep; x < descriptor.Width * 4 - sampleStep; x += sampleStep)
            {
                var offset = row + x;
                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];
                var luminance = (0.0722 * b) + (0.7152 * g) + (0.2126 * r);
                luminanceSum += luminance;

                var left = pixels[offset - sampleStep + 1];
                var right = pixels[offset + sampleStep + 1];
                gradientSum += Math.Abs(right - left);

                if (previousPixels.Length >= pixels.Length)
                {
                    differenceSum += Math.Abs(g - previousPixels[offset + 1]);
                }

                samples++;
            }
        }

        if (samples == 0)
        {
            return FrameQuality.Unknown;
        }

        var meanLuminance = luminanceSum / samples;
        var exposure = 1 - Math.Min(1, Math.Abs(meanLuminance - 128) / 128);
        var sharpness = Math.Min(1, gradientSum / samples / 32);
        var motion = previousPixels.Length >= pixels.Length ? Math.Min(1, differenceSum / samples / 48) : 1;
        var compressionNoise = 0d;
        var composite = Math.Clamp((sharpness * 0.45) + (exposure * 0.35) + ((1 - motion) * 0.20), 0, 1);
        return new FrameQuality(sharpness, exposure, motion, compressionNoise, composite);
    }
}

public sealed class DefaultFrameStabilityEvaluator : IFrameStabilityEvaluator
{
    public double StableDifferenceThreshold { get; init; } = 0.045;
    public int RequiredConsecutiveFrames { get; init; } = 3;
    public TimeSpan RequiredDuration { get; init; } = TimeSpan.FromMilliseconds(120);

    public FrameStability Evaluate(in RawFrame frame, ReadOnlySpan<byte> previousPixels, FrameStability previousStability)
    {
        var pixels = frame.Pixels.Span;
        if (previousPixels.Length < pixels.Length)
        {
            return new FrameStability(1, 0, TimeSpan.Zero, false);
        }

        long difference = 0;
        var samples = 0;
        var step = Math.Max(16, pixels.Length / 12000);
        for (var offset = 0; offset < pixels.Length; offset += step)
        {
            difference += Math.Abs(pixels[offset] - previousPixels[offset]);
            samples++;
        }

        var score = samples == 0 ? 1 : difference / (samples * 255d);
        var stable = score <= StableDifferenceThreshold;
        var count = stable ? previousStability.ConsecutiveStableFrames + 1 : 0;
        var duration = stable
            ? previousStability.StableDuration + TimeSpan.FromMilliseconds(33)
            : TimeSpan.Zero;

        return new FrameStability(
            score,
            count,
            duration,
            stable && count >= RequiredConsecutiveFrames && duration >= RequiredDuration);
    }
}
