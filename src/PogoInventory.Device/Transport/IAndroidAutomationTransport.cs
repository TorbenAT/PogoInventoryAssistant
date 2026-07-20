namespace PogoInventory.Device.Transport;

public interface IAndroidAutomationTransport : IAndroidDeviceTransport
{
    Task PressBackAsync(
        string serial,
        CancellationToken cancellationToken = default);

    Task OpenPokemonInventoryAsync(
        string serial,
        CancellationToken cancellationToken = default);

    Task EnterInventorySearchQueryAsync(
        string serial,
        string query,
        CancellationToken cancellationToken = default);

    Task SubmitInventorySearchQueryAsync(
        string serial,
        CancellationToken cancellationToken = default);

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
