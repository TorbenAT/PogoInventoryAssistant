namespace PogoInventory.Calibration.Errors;

public sealed class CalibrationException : Exception
{
    public CalibrationException(
        CalibrationErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public CalibrationErrorCode Code { get; }
}
