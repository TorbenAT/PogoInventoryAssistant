namespace PogoInventory.Vision.Errors;

public sealed class ScreenVisionException : Exception
{
    public ScreenVisionException(
        VisionErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public VisionErrorCode Code { get; }
}
