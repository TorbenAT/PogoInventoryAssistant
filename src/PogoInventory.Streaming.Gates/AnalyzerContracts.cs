using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public readonly record struct PixelRegion(int X, int Y, int Width, int Height)
{
    public static PixelRegion From(FrameDescriptor descriptor, PogoInventory.Streaming.Scrcpy.NormalizedRegion normalized)
    {
        normalized.Validate();
        var x = (int)Math.Floor(normalized.X * descriptor.Width);
        var y = (int)Math.Floor(normalized.Y * descriptor.Height);
        var width = Math.Max(1, (int)Math.Floor(normalized.Width * descriptor.Width));
        var height = Math.Max(1, (int)Math.Floor(normalized.Height * descriptor.Height));
        if (x + width > descriptor.Width)
        {
            width = descriptor.Width - x;
        }

        if (y + height > descriptor.Height)
        {
            height = descriptor.Height - y;
        }

        return new PixelRegion(x, y, width, height);
    }
}

public interface IFrameDifferenceAnalyzer
{
    double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget);
}

public interface IMotionAnalyzer
{
    double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget);
}

public interface IFrameSimilarityAnalyzer
{
    double Analyze(IFrameLease current, IFrameLease? previous, PixelRegion region, int samplingTarget);
}

public interface ISharpnessAnalyzer
{
    double Analyze(IFrameLease current, PixelRegion region, int samplingTarget);
}

public interface IBrightnessContrastAnalyzer
{
    (double Brightness, double Contrast) Analyze(IFrameLease current, PixelRegion region, int samplingTarget);
}

public interface IVisualFingerprintAnalyzer
{
    ulong Analyze(IFrameLease current, PixelRegion region);
}

public interface IRegionalObservationAnalyzer
{
    RegionalFrameObservation Analyze(
        RegionDefinition definition,
        IFrameLease current,
        IFrameLease? previous,
        RegionalFrameObservation? previousObservation,
        TemporalObserverOptions options);
}
