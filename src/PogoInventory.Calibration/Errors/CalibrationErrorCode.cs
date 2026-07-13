namespace PogoInventory.Calibration.Errors;

public enum CalibrationErrorCode
{
    InvalidWorkspace,
    InvalidManifest,
    InvalidAnchorPlan,
    FixtureMissing,
    FixtureHashMismatch,
    UnsafePath,
    FileSystemFailure
}
