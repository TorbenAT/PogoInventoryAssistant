namespace PogoInventory.Device.Models;

public sealed record AndroidDeviceMetadata
{
    public required string Serial { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? Product { get; init; }
    public string? DeviceName { get; init; }
    public string? AndroidVersion { get; init; }
    public int? ApiLevel { get; init; }
    public string? BuildFingerprint { get; init; }
    public required AndroidScreenInfo Screen { get; init; }
    public required AndroidBatteryInfo Battery { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
}
