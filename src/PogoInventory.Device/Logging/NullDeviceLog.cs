namespace PogoInventory.Device.Logging;

public sealed class NullDeviceLog : IDeviceLog
{
    public static NullDeviceLog Instance { get; } = new();

    private NullDeviceLog()
    {
    }

    public void Write(
        DeviceLogLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? properties = null)
    {
    }
}
