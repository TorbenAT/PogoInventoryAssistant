using PogoInventory.Vision.Errors;

namespace PogoInventory.Vision.Imaging;

public static class FingerprintComparer
{
    public static double Similarity(
        IReadOnlyList<byte> first,
        IReadOnlyList<byte> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count == 0 || first.Count != second.Count)
        {
            throw new ScreenVisionException(
                VisionErrorCode.InvalidFingerprint,
                "Fingerprints must have the same non-zero length.");
        }

        var difference = 0L;
        for (var index = 0; index < first.Count; index++)
        {
            difference += Math.Abs(first[index] - second[index]);
        }

        var maximum = 255d * first.Count;
        return 1d - difference / maximum;
    }
}
