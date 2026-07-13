namespace PogoInventory.Device.Adb;

public interface IAdbProcessRunner
{
    Task<AdbProcessResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
