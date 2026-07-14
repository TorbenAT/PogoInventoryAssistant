namespace PogoInventory.CalcyProbe.Models;

public sealed record CalcyProbeOptions
{
    public const string DefaultPackageName = "tesmath.calcy";

    public string PackageName { get; init; } = DefaultPackageName;
    public int MaximumLogcatLines { get; init; } = 4000;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PackageName))
        {
            throw new ArgumentException("Package name is required.", nameof(PackageName));
        }

        if (MaximumLogcatLines is < 100 or > 20000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumLogcatLines),
                "Maximum logcat lines must be between 100 and 20000.");
        }
    }
}
