namespace PogoInventory.Automation.Models;

public enum AutomationStopReason
{
    None,
    MaximumItemsReached,
    MaximumRuntimeReached,
    EndOfInventoryDetected,
    Cancelled,
    UnknownScreen,
    PopupDetected,
    NetworkErrorDetected,
    UnexpectedScreen,
    StateTimeout,
    ResumeMismatch,
    DeviceMismatch,
    GeometryMismatch,
    BatterySafetyStop,
    Failure
}
