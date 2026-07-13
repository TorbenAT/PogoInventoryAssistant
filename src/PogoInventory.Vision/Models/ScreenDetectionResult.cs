namespace PogoInventory.Vision.Models;

public sealed record ScreenDetectionResult
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ProfileName { get; init; }
    public required ScreenState State { get; init; }
    public required double Confidence { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required ScreenOrientation Orientation { get; init; }
    public required double AspectRatio { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<StateEvidence> States { get; init; } =
        Array.Empty<StateEvidence>();
    public DateTimeOffset AnalysedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
