using System.Text;

namespace PogoInventory.Device.Adb;

public sealed record AdbProcessResult
{
    public required int ExitCode { get; init; }
    public required byte[] StandardOutput { get; init; }
    public required string StandardError { get; init; }

    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);
}
