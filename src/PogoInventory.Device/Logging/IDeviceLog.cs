namespace PogoInventory.Device.Logging;

public interface IDeviceLog
{
    void Write(
        DeviceLogLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? properties = null);
}
