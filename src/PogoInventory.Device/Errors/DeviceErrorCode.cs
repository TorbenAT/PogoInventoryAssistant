namespace PogoInventory.Device.Errors;

public enum DeviceErrorCode
{
    AdbNotFound,
    AdbStartFailed,
    CommandTimedOut,
    CommandFailed,
    NoAuthorizedDevice,
    MultipleAuthorizedDevices,
    RequestedDeviceNotFound,
    RequestedDeviceNotAuthorized,
    InvalidAdbOutput,
    InvalidScreenshot,
    IoFailure
}
