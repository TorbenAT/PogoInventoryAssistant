namespace PogoInventory.Device.Models;

public sealed record AndroidScreenInfo
{
    public int? PhysicalWidth { get; init; }
    public int? PhysicalHeight { get; init; }
    public int? OverrideWidth { get; init; }
    public int? OverrideHeight { get; init; }

    public int? EffectiveWidth => OverrideWidth ?? PhysicalWidth;
    public int? EffectiveHeight => OverrideHeight ?? PhysicalHeight;
}
