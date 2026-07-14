namespace PogoInventory.Device;

public sealed record DeviceHarnessOptions
{
    public const string CurrentVersion = "0.6.2";

    public string AdbPath { get; init; } = "adb";
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public string HarnessVersion { get; init; } = CurrentVersion;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AdbPath))
        {
            throw new ArgumentException("ADB path cannot be empty.", nameof(AdbPath));
        }

        if (CommandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CommandTimeout),
                "Command timeout must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(HarnessVersion))
        {
            throw new ArgumentException("Harness version cannot be empty.", nameof(HarnessVersion));
        }
    }
}
