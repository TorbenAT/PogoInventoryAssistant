namespace PogoInventory.Device.Models;

public sealed record AndroidBatteryInfo
{
    public int? LevelPercent { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public int? StatusCode { get; init; }
    public string? StatusName { get; init; }
    public int? HealthCode { get; init; }
    public bool? AcPowered { get; init; }
    public bool? UsbPowered { get; init; }
    public bool? WirelessPowered { get; init; }
    public bool? Present { get; init; }
    public string? Technology { get; init; }
}
