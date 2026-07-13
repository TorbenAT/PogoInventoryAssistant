namespace PogoInventory.Device.Errors;

public sealed class DeviceHarnessException : Exception
{
    public DeviceHarnessException(
        DeviceErrorCode code,
        string message,
        string? command = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Command = command;
    }

    public DeviceErrorCode Code { get; }
    public string? Command { get; }
}
