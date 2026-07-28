using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

internal static class PixelSampling
{
    public static void Validate(IFrameLease frame)
    {
        if (frame.Metadata.Descriptor.PixelFormat != FramePixelFormat.Bgra32)
        {
            throw new NotSupportedException("Phase 3 CPU analyzers require BGRA32.");
        }
    }

    public static int PixelStep(PixelRegion region, int target)
    {
        var pixels = checked(region.Width * region.Height);
        return Math.Max(1, (int)Math.Sqrt(Math.Max(1, pixels / Math.Max(1, target))));
    }

    public static byte Luma(ReadOnlySpan<byte> pixels, int offset)
    {
        var b = pixels[offset];
        var g = pixels[offset + 1];
        var r = pixels[offset + 2];
        return (byte)Math.Clamp((int)Math.Round((0.0722 * b) + (0.7152 * g) + (0.2126 * r)), 0, 255);
    }
}

public sealed class CpuMeanAbsoluteDifferenceAnalyzer : IFrameDifferenceAnalyzer
{
    public double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget)
    {
        PixelSampling.Validate(current);
        if (previous is null || previous.Metadata.Descriptor != current.Metadata.Descriptor)
        {
            return 1;
        }

        PixelSampling.Validate(previous);
        var descriptor = current.Metadata.Descriptor;
        var currentPixels = current.Pixels.Span;
        var previousPixels = previous.Pixels.Span;
        var step = PixelSampling.PixelStep(region, samplingTarget);
        long sum = 0;
        var count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y += step)
        {
            var row = y * descriptor.Stride;
            for (var x = region.X; x < region.X + region.Width; x += step)
            {
                var offset = row + (x * 4);
                sum += Math.Abs(PixelSampling.Luma(currentPixels, offset) - PixelSampling.Luma(previousPixels, offset));
                count++;
            }
        }

        return count == 0 ? 1 : Math.Clamp(sum / (count * 255d), 0, 1);
    }
}

public sealed class CpuChangedPixelMotionAnalyzer : IMotionAnalyzer
{
    public int LumaChangeThreshold { get; init; } = 12;

    public double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget)
    {
        PixelSampling.Validate(current);
        if (previous is null || previous.Metadata.Descriptor != current.Metadata.Descriptor)
        {
            return 1;
        }

        PixelSampling.Validate(previous);
        var descriptor = current.Metadata.Descriptor;
        var currentPixels = current.Pixels.Span;
        var previousPixels = previous.Pixels.Span;
        var step = PixelSampling.PixelStep(region, samplingTarget);
        var changed = 0;
        var count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y += step)
        {
            var row = y * descriptor.Stride;
            for (var x = region.X; x < region.X + region.Width; x += step)
            {
                var offset = row + (x * 4);
                var delta = Math.Abs(PixelSampling.Luma(currentPixels, offset) - PixelSampling.Luma(previousPixels, offset));
                if (delta >= LumaChangeThreshold)
                {
                    changed++;
                }

                count++;
            }
        }

        return count == 0 ? 1 : changed / (double)count;
    }
}

public sealed class CpuStructuralSimilarityAnalyzer : IFrameSimilarityAnalyzer
{
    public double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget)
    {
        PixelSampling.Validate(current);
        if (previous is null || previous.Metadata.Descriptor != current.Metadata.Descriptor)
        {
            return 0;
        }

        PixelSampling.Validate(previous);
        var descriptor = current.Metadata.Descriptor;
        var currentPixels = current.Pixels.Span;
        var previousPixels = previous.Pixels.Span;
        var step = PixelSampling.PixelStep(region, samplingTarget);
        double currentSum = 0;
        double previousSum = 0;
        double currentSquareSum = 0;
        double previousSquareSum = 0;
        double productSum = 0;
        var count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y += step)
        {
            var row = y * descriptor.Stride;
            for (var x = region.X; x < region.X + region.Width; x += step)
            {
                var offset = row + (x * 4);
                var currentLuma = PixelSampling.Luma(currentPixels, offset);
                var previousLuma = PixelSampling.Luma(previousPixels, offset);
                currentSum += currentLuma;
                previousSum += previousLuma;
                currentSquareSum += currentLuma * currentLuma;
                previousSquareSum += previousLuma * previousLuma;
                productSum += currentLuma * previousLuma;
                count++;
            }
        }

        if (count < 2)
        {
            return 0;
        }

        var currentMean = currentSum / count;
        var previousMean = previousSum / count;
        var currentVariance = Math.Max(0, (currentSquareSum / count) - (currentMean * currentMean));
        var previousVariance = Math.Max(0, (previousSquareSum / count) - (previousMean * previousMean));
        var covariance = (productSum / count) - (currentMean * previousMean);
        var c1 = Math.Pow(0.01 * 255, 2);
        var c2 = Math.Pow(0.03 * 255, 2);
        var numerator = ((2 * currentMean * previousMean) + c1) * ((2 * covariance) + c2);
        var denominator = ((currentMean * currentMean) + (previousMean * previousMean) + c1) *
                          (currentVariance + previousVariance + c2);
        if (denominator <= 0)
        {
            return 0;
        }

        return Math.Clamp(numerator / denominator, 0, 1);
    }
}

public sealed class CpuGradientSharpnessAnalyzer : ISharpnessAnalyzer
{
    public double Analyze(IFrameLease current, PixelRegion region, int samplingTarget)
    {
        PixelSampling.Validate(current);
        var descriptor = current.Metadata.Descriptor;
        var pixels = current.Pixels.Span;
        var step = Math.Max(1, PixelSampling.PixelStep(region, samplingTarget));
        double gradient = 0;
        var count = 0;
        var maxX = region.X + region.Width - step;
        var maxY = region.Y + region.Height - step;

        for (var y = region.Y; y < maxY; y += step)
        {
            var row = y * descriptor.Stride;
            var nextRow = (y + step) * descriptor.Stride;
            for (var x = region.X; x < maxX; x += step)
            {
                var offset = row + (x * 4);
                var right = row + ((x + step) * 4);
                var down = nextRow + (x * 4);
                var centerLuma = PixelSampling.Luma(pixels, offset);
                gradient += Math.Abs(centerLuma - PixelSampling.Luma(pixels, right));
                gradient += Math.Abs(centerLuma - PixelSampling.Luma(pixels, down));
                count += 2;
            }
        }

        return count == 0 ? 0 : Math.Clamp((gradient / count) / 48d, 0, 1);
    }
}

public sealed class CpuBrightnessContrastAnalyzer : IBrightnessContrastAnalyzer
{
    public (double Brightness, double Contrast) Analyze(IFrameLease current, PixelRegion region, int samplingTarget)
    {
        PixelSampling.Validate(current);
        var descriptor = current.Metadata.Descriptor;
        var pixels = current.Pixels.Span;
        var step = PixelSampling.PixelStep(region, samplingTarget);
        double sum = 0;
        double squareSum = 0;
        var count = 0;

        for (var y = region.Y; y < region.Y + region.Height; y += step)
        {
            var row = y * descriptor.Stride;
            for (var x = region.X; x < region.X + region.Width; x += step)
            {
                var luma = PixelSampling.Luma(pixels, row + (x * 4));
                sum += luma;
                squareSum += luma * luma;
                count++;
            }
        }

        if (count == 0)
        {
            return (0, 0);
        }

        var mean = sum / count;
        var variance = Math.Max(0, (squareSum / count) - (mean * mean));
        return (mean / 255d, Math.Clamp(Math.Sqrt(variance) / 96d, 0, 1));
    }
}

public sealed class CpuDifferenceHashAnalyzer : IVisualFingerprintAnalyzer
{
    public ulong Analyze(IFrameLease current, PixelRegion region)
    {
        PixelSampling.Validate(current);
        var descriptor = current.Metadata.Descriptor;
        var pixels = current.Pixels.Span;
        Span<byte> values = stackalloc byte[72];
        var index = 0;

        for (var rowIndex = 0; rowIndex < 8; rowIndex++)
        {
            var y = region.Y + Math.Min(region.Height - 1, (int)Math.Round(rowIndex * (region.Height - 1) / 7d));
            var row = y * descriptor.Stride;
            for (var columnIndex = 0; columnIndex < 9; columnIndex++)
            {
                var x = region.X + Math.Min(region.Width - 1, (int)Math.Round(columnIndex * (region.Width - 1) / 8d));
                values[index++] = PixelSampling.Luma(pixels, row + (x * 4));
            }
        }

        ulong hash = 0;
        var bit = 0;
        for (var rowIndex = 0; rowIndex < 8; rowIndex++)
        {
            var rowStart = rowIndex * 9;
            for (var columnIndex = 0; columnIndex < 8; columnIndex++)
            {
                if (values[rowStart + columnIndex] > values[rowStart + columnIndex + 1])
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return hash;
    }
}

public sealed class CpuRegionalObservationAnalyzer : IRegionalObservationAnalyzer
{
    private readonly IFrameDifferenceAnalyzer _difference;
    private readonly IMotionAnalyzer _motion;
    private readonly IFrameSimilarityAnalyzer _similarity;
    private readonly ISharpnessAnalyzer _sharpness;
    private readonly IBrightnessContrastAnalyzer _brightnessContrast;
    private readonly IVisualFingerprintAnalyzer _fingerprint;

    public CpuRegionalObservationAnalyzer(
        IFrameDifferenceAnalyzer? difference = null,
        IMotionAnalyzer? motion = null,
        IFrameSimilarityAnalyzer? similarity = null,
        ISharpnessAnalyzer? sharpness = null,
        IBrightnessContrastAnalyzer? brightnessContrast = null,
        IVisualFingerprintAnalyzer? fingerprint = null)
    {
        _difference = difference ?? new CpuMeanAbsoluteDifferenceAnalyzer();
        _motion = motion ?? new CpuChangedPixelMotionAnalyzer();
        _similarity = similarity ?? new CpuStructuralSimilarityAnalyzer();
        _sharpness = sharpness ?? new CpuGradientSharpnessAnalyzer();
        _brightnessContrast = brightnessContrast ?? new CpuBrightnessContrastAnalyzer();
        _fingerprint = fingerprint ?? new CpuDifferenceHashAnalyzer();
    }

    public RegionalFrameObservation Analyze(
        RegionDefinition definition,
        IFrameLease current,
        IFrameLease? previous,
        RegionalFrameObservation? previousObservation,
        TemporalObserverOptions options)
    {
        var pixelRegion = PixelRegion.From(current.Metadata.Descriptor, definition.Region);
        var difference = _difference.Analyze(current, previous, pixelRegion, options.SamplingTarget);
        var motion = _motion.Analyze(current, previous, pixelRegion, options.SamplingTarget);
        var similarity = _similarity.Analyze(current, previous, pixelRegion, options.SamplingTarget);
        var sharpness = _sharpness.Analyze(current, pixelRegion, options.SamplingTarget);
        var brightnessContrast = _brightnessContrast.Analyze(current, pixelRegion, options.SamplingTarget);
        var fingerprint = _fingerprint.Analyze(current, pixelRegion);
        var velocity = previousObservation is null
            ? difference
            : Math.Abs(difference - previousObservation.DifferenceScore);
        var stable = motion <= options.StableMotionThreshold &&
                     difference <= options.StableDifferenceThreshold &&
                     similarity >= options.StableSimilarityThreshold &&
                     sharpness >= options.MinimumSharpness;
        var transitioning = definition.ObserveTransition &&
                            (motion >= options.TransitionMotionThreshold ||
                             difference >= options.TransitionDifferenceThreshold);

        return new RegionalFrameObservation
        {
            RegionName = definition.Name,
            StabilityRole = definition.StabilityRole,
            ObserveTransition = definition.ObserveTransition,
            DifferenceScore = difference,
            MotionScore = motion,
            SimilarityScore = similarity,
            SharpnessScore = sharpness,
            BrightnessScore = brightnessContrast.Brightness,
            ContrastScore = brightnessContrast.Contrast,
            ChangeVelocity = velocity,
            VisualFingerprint = fingerprint,
            IsLikelyStable = stable,
            IsLikelyTransitioning = transitioning
        };
    }
}
