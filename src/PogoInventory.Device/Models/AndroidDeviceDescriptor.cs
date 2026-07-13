namespace PogoInventory.Device.Models;

public sealed record AndroidDeviceDescriptor
{
    public required string Serial { get; init; }
    public required AndroidDeviceState State { get; init; }
    public string? Product { get; init; }
    public string? Model { get; init; }
    public string? Device { get; init; }
    public string? TransportId { get; init; }
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsAuthorized => State == AndroidDeviceState.Authorized;
}
