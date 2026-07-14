using PogoInventory.Vision.Models;

namespace PogoInventory.ImagePretest.Models;

public sealed record ImagePretestItem
{
    public required int SequenceNumber { get; init; }
    public required string FileName { get; init; }
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public long LengthBytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double AspectRatio { get; init; }
    public ScreenOrientation Orientation { get; init; }
    public required string GeometryKey { get; init; }
    public required string VisualFingerprintSha256 { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }

    public bool Decoded => ErrorCode is null;
}
