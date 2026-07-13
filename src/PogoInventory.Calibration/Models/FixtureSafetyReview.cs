namespace PogoInventory.Calibration.Models;

public sealed record FixtureSafetyReview
{
    public bool AccountIdentitySafe { get; init; }
    public bool LocationSafe { get; init; }
    public bool NotificationsSafe { get; init; }
    public bool OtherPersonalDataSafe { get; init; }
    public bool ApprovedForCalibration { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAtUtc { get; init; }

    public bool IsComplete =>
        AccountIdentitySafe &&
        LocationSafe &&
        NotificationsSafe &&
        OtherPersonalDataSafe &&
        ApprovedForCalibration;
}
