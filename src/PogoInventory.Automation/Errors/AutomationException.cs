namespace PogoInventory.Automation.Errors;

public sealed class AutomationException : Exception
{
    public AutomationException(
        AutomationErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public AutomationErrorCode Code { get; }
}
