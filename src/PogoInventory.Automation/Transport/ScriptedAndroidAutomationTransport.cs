using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Transport;

public sealed class ScriptedAndroidAutomationTransport : IAndroidAutomationTransport
{
    private readonly IReadOnlyDictionary<ScreenState, byte[]> _screens;
    private readonly IReadOnlyList<byte[]> _appraisalScreens;
    private readonly AndroidDeviceDescriptor _device;
    private readonly AndroidDeviceMetadata _metadata;
    private ScreenState _state = ScreenState.InventoryList;
    private int _appraisalIndex;
    private readonly List<string> _actions = new();

    public ScriptedAndroidAutomationTransport(
        IReadOnlyDictionary<ScreenState, byte[]> screens,
        IReadOnlyList<byte[]> appraisalScreens,
        string serial = "FAKE-AUTO-001")
    {
        _screens = screens ?? throw new ArgumentNullException(nameof(screens));
        _appraisalScreens = appraisalScreens ?? throw new ArgumentNullException(nameof(appraisalScreens));

        if (!_screens.ContainsKey(ScreenState.InventoryList) ||
            !_screens.ContainsKey(ScreenState.PokemonDetails) ||
            !_screens.ContainsKey(ScreenState.PokemonMenuOpen) ||
            _appraisalScreens.Count == 0)
        {
            throw new ArgumentException(
                "Scripted transport requires inventory, details, menu and at least one appraisal screen.");
        }

        var image = PngDecoder.Decode(_screens[ScreenState.InventoryList]);
        _device = new AndroidDeviceDescriptor
        {
            Serial = serial,
            State = AndroidDeviceState.Authorized,
            Product = "scripted",
            Model = "Scripted Android",
            Device = "scripted-device",
            TransportId = "1"
        };
        _metadata = new AndroidDeviceMetadata
        {
            Serial = serial,
            Manufacturer = "Pogo Inventory Assistant",
            Model = "Scripted Android",
            Product = "scripted",
            DeviceName = "scripted-device",
            AndroidVersion = "16",
            ApiLevel = 36,
            BuildFingerprint = "pogo/scripted/0.11.0",
            Screen = new AndroidScreenInfo
            {
                PhysicalWidth = image.Width,
                PhysicalHeight = image.Height
            },
            Battery = new AndroidBatteryInfo
            {
                LevelPercent = 90,
                TemperatureCelsius = 30m,
                StatusCode = 2,
                StatusName = "Charging",
                UsbPowered = true,
                Present = true,
                Technology = "Li-ion"
            },
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<string> Actions => _actions;

    public Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AndroidDeviceDescriptor>>(new[] { _device });
    }

    public Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        return Task.FromResult(_metadata with { CapturedAtUtc = DateTimeOffset.UtcNow });
    }

    public Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        var bytes = _state == ScreenState.AppraisalOpen
            ? _appraisalScreens[_appraisalIndex]
            : _screens[_state];
        return Task.FromResult(bytes.ToArray());
    }

    public Task TapAsync(
        string serial,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        _actions.Add($"tap:{x},{y}:{_state}");

        _state = _state switch
        {
            ScreenState.InventoryList => ScreenState.PokemonDetails,
            ScreenState.PokemonDetails => ScreenState.PokemonMenuOpen,
            ScreenState.PokemonMenuOpen => ScreenState.AppraisalOpen,
            _ => _state
        };

        return Task.CompletedTask;
    }

    public Task SwipeAsync(
        string serial,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSerial(serial);
        _actions.Add(
            $"swipe:{startX},{startY}->{endX},{endY}:{durationMilliseconds}:{_state}");

        if (_state == ScreenState.AppraisalOpen &&
            _appraisalIndex < _appraisalScreens.Count - 1)
        {
            _appraisalIndex++;
        }

        return Task.CompletedTask;
    }

    private void EnsureSerial(string serial)
    {
        if (!string.Equals(serial, _device.Serial, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unknown scripted device serial '{serial}'.", nameof(serial));
        }
    }
}
