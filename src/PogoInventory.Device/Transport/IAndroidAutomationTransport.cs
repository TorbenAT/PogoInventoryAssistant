namespace PogoInventory.Device.Transport;

public interface IAndroidAutomationTransport : IAndroidDeviceTransport
{
    Task TapAsync(
        string serial,
        int x,
        int y,
        CancellationToken cancellationToken = default);

    Task SwipeAsync(
        string serial,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken = default);
}
