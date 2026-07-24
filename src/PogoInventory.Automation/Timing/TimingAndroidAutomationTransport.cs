using System.Diagnostics;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;

namespace PogoInventory.Automation.Timing;

/// <summary>
/// Decorates an <see cref="IAndroidAutomationTransport"/> to time the PNG
/// screenshot transfer (the single largest measured cost in a real cleanup
/// run) and on-device input gestures (tap/swipe). Every other member passes
/// straight through with no added behavior.
/// </summary>
public sealed class TimingAndroidAutomationTransport : IAndroidAutomationTransport
{
    private readonly IAndroidAutomationTransport _inner;
    private readonly IOperationTimingCollector _timing;

    public TimingAndroidAutomationTransport(
        IAndroidAutomationTransport inner,
        IOperationTimingCollector timing)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _timing = timing ?? throw new ArgumentNullException(nameof(timing));
    }

    public async Task<byte[]> CaptureScreenshotPngAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _inner.CaptureScreenshotPngAsync(serial, cancellationToken);
        stopwatch.Stop();
        _timing.RecordCapture("screencap", stopwatch.Elapsed.TotalMilliseconds, result.Length);
        return result;
    }

    public Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
        CancellationToken cancellationToken = default) =>
        _inner.ListDevicesAsync(cancellationToken);

    public Task<AndroidDeviceMetadata> ReadMetadataAsync(
        string serial,
        CancellationToken cancellationToken = default) =>
        _inner.ReadMetadataAsync(serial, cancellationToken);

    public Task<string> CaptureUiHierarchyAsync(
        string serial,
        CancellationToken cancellationToken = default) =>
        _inner.CaptureUiHierarchyAsync(serial, cancellationToken);

    public Task PressBackAsync(
        string serial,
        CancellationToken cancellationToken = default) =>
        _inner.PressBackAsync(serial, cancellationToken);

    public Task OpenPokemonInventoryAsync(
        string serial,
        CancellationToken cancellationToken = default) =>
        _inner.OpenPokemonInventoryAsync(serial, cancellationToken);

    public Task EnterInventorySearchQueryAsync(
        string serial,
        string query,
        CancellationToken cancellationToken = default) =>
        _inner.EnterInventorySearchQueryAsync(serial, query, cancellationToken);

    public Task SubmitInventorySearchQueryAsync(
        string serial,
        CancellationToken cancellationToken = default) =>
        _inner.SubmitInventorySearchQueryAsync(serial, cancellationToken);

    public async Task TapAsync(
        string serial,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await _inner.TapAsync(serial, x, y, cancellationToken);
        stopwatch.Stop();
        _timing.RecordInput("tap", stopwatch.Elapsed.TotalMilliseconds);
    }

    public async Task SwipeAsync(
        string serial,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await _inner.SwipeAsync(serial, startX, startY, endX, endY, durationMilliseconds, cancellationToken);
        stopwatch.Stop();
        _timing.RecordInput("swipe", stopwatch.Elapsed.TotalMilliseconds);
    }
}
