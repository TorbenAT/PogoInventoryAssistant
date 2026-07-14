namespace PogoInventory.Observations.Models;

public sealed record CalcyObservationRequest
{
    public required int SequenceNumber { get; init; }
    public required string DeviceSerial { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required byte[] ScreenshotPng { get; init; }
    public required string ScreenshotSha256 { get; init; }
}
