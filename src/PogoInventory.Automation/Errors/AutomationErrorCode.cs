namespace PogoInventory.Automation.Errors;

public enum AutomationErrorCode
{
    InvalidProfile,
    DeviceMismatch,
    GeometryMismatch,
    InvalidStartingState,
    StateTimeout,
    UnsafeScreenState,
    ResumeMismatch,
    CheckpointCorrupt,
    FileSystemFailure,
    TransportFailure
}
