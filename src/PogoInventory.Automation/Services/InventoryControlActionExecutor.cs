using PogoInventory.Automation.Models;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;

namespace PogoInventory.Automation.Services;

public sealed class InventoryControlActionExecutor
{
    private readonly IAndroidAutomationTransport _transport;
    private readonly InventoryControlProfile _profile;

    public InventoryControlActionExecutor(
        IAndroidAutomationTransport transport,
        InventoryControlProfile profile)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
    }

    public async Task ExecuteAsync(
        string serial,
        AndroidScreenInfo screen,
        AutomationActionKind action,
        string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(screen);

        if (action == AutomationActionKind.EnterInventorySearchText)
        {
            await _transport.EnterInventorySearchQueryAsync(
                serial,
                InventorySearchQuery.Validate(searchQuery ?? InventorySearchQuery.Unindexed),
                cancellationToken);
            return;
        }

        var point = action switch
        {
            AutomationActionKind.OpenPokemonInventory => _profile.OpenInventory,
            AutomationActionKind.OpenInventorySearch => _profile.OpenSearch,
            AutomationActionKind.ClearInventorySearch => _profile.ClearSearch,
            AutomationActionKind.ApplyInventorySearch => _profile.ApplySearch,
            AutomationActionKind.TapFirstSearchResult => _profile.FirstSearchResult,
            AutomationActionKind.CloseAppraisal => _profile.CloseAppraisal,
            AutomationActionKind.OpenPokemonTagMenu => _profile.OpenTagMenu,
            AutomationActionKind.SelectConfiguredTag => _profile.SelectAiIndexed,
            AutomationActionKind.ConfirmConfiguredTag => _profile.ConfirmTag,
            AutomationActionKind.DismissKnownInformationalPopup => _profile.DismissNewMegaLevel,
            _ => throw new InvalidOperationException(
                $"Action '{action}' is not an inventory control-profile action.")
        };

        var width = screen.EffectiveWidth ?? throw new InvalidOperationException("Screen width is unavailable.");
        var height = screen.EffectiveHeight ?? throw new InvalidOperationException("Screen height is unavailable.");
        var (x, y) = point.ToPixels(width, height);
        await _transport.TapAsync(serial, x, y, cancellationToken);
    }
}
