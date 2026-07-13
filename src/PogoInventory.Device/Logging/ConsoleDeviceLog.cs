namespace PogoInventory.Device.Logging;

public sealed class ConsoleDeviceLog : IDeviceLog
{
    public void Write(
        DeviceLogLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var propertyText = properties is null || properties.Count == 0
            ? string.Empty
            : " " + string.Join(
                " ",
                properties.Select(x => $"{x.Key}={Escape(x.Value)}"));

        var line = $"{DateTimeOffset.UtcNow:O} [{level}] {eventName}: {message}{propertyText}";

        if (level is DeviceLogLevel.Warning or DeviceLogLevel.Error)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }
    }

    private static string Escape(string value) =>
        value.Any(char.IsWhiteSpace)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
}
