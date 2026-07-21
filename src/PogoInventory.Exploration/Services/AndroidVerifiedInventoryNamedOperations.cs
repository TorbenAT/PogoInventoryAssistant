using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

/// <summary>
/// Real-device adapter for the verified sequence. All input is expressed as a
/// named operation and every input has a bounded, visual postcondition check.
/// The adapter never builds or executes an ADB command.
/// </summary>
public sealed class AndroidVerifiedInventoryNamedOperations : IVerifiedInventoryNamedOperations
{
    private readonly IAndroidAutomationTransport _transport;
    private readonly string _serial;
    private readonly InventoryAutomationProfile _automationProfile;
    private readonly AppraisalVisualProfile? _appraisalProfile;
    private readonly string _evidenceDirectory;
    private readonly PokemonGoGameStateDetector _detector = new();
    private readonly VisualControlLocator _locator = new();
    private readonly InventorySearchVisualAnalyzer _searchAnalyzer = new();
    private readonly PokemonDetailsIdentityAnalyzer _identityAnalyzer;
    private readonly GuardedInventoryRecovery _recovery = new();
    private AndroidDeviceMetadata? _metadata;
    private PokemonIdentityConsensus? _lastIdentity;
    private byte[]? _lastScreenshot;
    private int _evidenceOrdinal;

    public AndroidVerifiedInventoryNamedOperations(
        IAndroidAutomationTransport transport,
        string serial,
        InventoryAutomationProfile automationProfile,
        string evidenceDirectory,
        AppraisalVisualProfile? appraisalProfile = null,
        PokemonIdentityFingerprintProfile? identityProfile = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        _serial = serial;
        _automationProfile = automationProfile ?? throw new ArgumentNullException(nameof(automationProfile));
        _automationProfile.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory);
        _evidenceDirectory = Path.GetFullPath(evidenceDirectory);
        _appraisalProfile = appraisalProfile;
        _identityAnalyzer = new PokemonDetailsIdentityAnalyzer(identityProfile);
    }

    public async Task<VerifiedSequenceState> EnsureFilteredInventoryAsync(
        string query, CancellationToken cancellationToken)
    {
        await EnsureMetadataAsync(cancellationToken);
        var state = await WaitForStateAsync(
            new[] { PokemonGoGameState.Inventory, PokemonGoGameState.GameplayMap, PokemonGoGameState.MainMenu },
            cancellationToken);
        if (state.State == PokemonGoGameState.GameplayMap)
        {
            var menu = _locator.LocateMainMenuPokeball(state.Screenshot);
            if (menu is null) return VerifiedSequenceState.Unknown;
            await TapAndVerifyAsync(menu.Target, PokemonGoGameState.MainMenu, "open-main-menu", cancellationToken);
            state = await WaitForStateAsync(new[] { PokemonGoGameState.MainMenu }, cancellationToken);
        }
        if (state.State == PokemonGoGameState.MainMenu)
        {
            var inventory = _locator.LocatePokemonInventory(state.Screenshot);
            if (inventory is null) return VerifiedSequenceState.Unknown;
            await TapAndVerifyAsync(inventory.Target, PokemonGoGameState.Inventory, "open-inventory", cancellationToken);
        }

        var current = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var workflow = new GuardedInventorySearch();
        var evidence = _searchAnalyzer.Analyze(current.Screenshot);
        var begin = workflow.Begin(evidence, query);
        if (begin == InventorySearchOutcome.UnsafePreState) return VerifiedSequenceState.Unknown;
        for (var actionCount = 0; actionCount < 4; actionCount++)
        {
            var authorization = workflow.AuthorizeNextAction();
            if (authorization is null) break;
            switch (authorization.Action)
            {
                case InventorySearchAction.OpenSearch:
                    await _transport.TapAsync(_serial, await PixelXAsync(0.5005, cancellationToken),
                        await PixelYAsync(0.1881, cancellationToken), cancellationToken);
                    break;
                case InventorySearchAction.ClearSearch:
                    await TapNamedAsync(new NormalizedPoint { X = 0.9175, Y = 0.1881 }, "clear-search", cancellationToken);
                    break;
                case InventorySearchAction.EnterQuery:
                    await _transport.EnterInventorySearchQueryAsync(_serial, query, cancellationToken);
                    break;
                case InventorySearchAction.SubmitQuery:
                    await _transport.SubmitInventorySearchQueryAsync(_serial, cancellationToken);
                    break;
            }
            var after = await CaptureAsync($"search-{authorization.Sequence}", cancellationToken);
            var outcome = workflow.ObservePostAction(_searchAnalyzer.Analyze(after));
            if (outcome is InventorySearchOutcome.ActionNotObserved or InventorySearchOutcome.UnexpectedState)
                return VerifiedSequenceState.Unknown;
            if (outcome == InventorySearchOutcome.Succeeded) break;
        }
        var filtered = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        return filtered.State == PokemonGoGameState.Inventory
            ? VerifiedSequenceState.Inventory
            : VerifiedSequenceState.Unknown;
    }

    public async Task<VerifiedSequenceState> OpenFirstPokemonAsync(CancellationToken cancellationToken)
    {
        var inventory = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        var located = _locator.LocateInventoryCard(inventory.Screenshot);
        if (located is null) return VerifiedSequenceState.Unknown;
        await TapNamedAsync(located.Target, "open-first-pokemon", cancellationToken);
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        return details.State == PokemonGoGameState.PokemonDetails
            ? VerifiedSequenceState.PokemonDetails
            : VerifiedSequenceState.Unknown;
    }

    public async Task<PokemonIdentityConsensus> CaptureIdentityAsync(CancellationToken cancellationToken)
    {
        var frames = new List<PokemonIdentityFrame>(3);
        for (var index = 0; index < 3; index++)
        {
            var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
            frames.Add(new PokemonIdentityFrame { ScreenshotPng = details.Screenshot });
            await SaveEvidenceAsync($"identity-{index + 1}", details.Screenshot, cancellationToken);
            if (index < 2) await Task.Delay(_automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        _lastIdentity = _identityAnalyzer.Consensus(frames);
        return _lastIdentity;
    }

    public async Task<string> CaptureAppraisalAsync(CancellationToken cancellationToken)
    {
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var menu = _locator.LocateDetailsMenu(details.Screenshot);
        if (menu is null) return "Partial";
        await TapNamedAsync(menu.Target, "open-details-menu", cancellationToken);
        var menuState = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonMenu }, cancellationToken);
        var appraise = _locator.LocateAppraiseMenuItem(menuState.Screenshot);
        if (appraise is null) return "Partial";
        await TapNamedAsync(appraise.Target, "open-appraisal", cancellationToken);
        var appraisal = await WaitForStateAsync(new[] { PokemonGoGameState.Appraisal }, cancellationToken);
        await SaveEvidenceAsync("appraisal", appraisal.Screenshot, cancellationToken);
        return appraisal.State == PokemonGoGameState.Appraisal ? "AppraisalObserved" : "Partial";
    }

    public async Task<VerifiedSequenceState> ExitAppraisalAsync(CancellationToken cancellationToken)
    {
        var current = await WaitForStateAsync(
            new[] { PokemonGoGameState.Appraisal, PokemonGoGameState.PokemonDetails }, cancellationToken);
        if (current.State == PokemonGoGameState.PokemonDetails) return VerifiedSequenceState.PokemonDetails;
        await _transport.PressBackAsync(_serial, cancellationToken);
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        return details.State == PokemonGoGameState.PokemonDetails
            ? VerifiedSequenceState.PokemonDetails
            : VerifiedSequenceState.Unknown;
    }

    public async Task<VerifiedTagObservation> ReadTagObservationAsync(CancellationToken cancellationToken)
    {
        var details = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var identity = _identityAnalyzer.Analyze(details.Screenshot);
        return new VerifiedTagObservation
        {
            TagCount = identity.Tags.TagCount,
            KnownTagNames = identity.Tags.TagNames,
            NamesComplete = identity.Tags.TagNames.Count >= identity.Tags.TagCount,
            Section = identity.Tags.Section,
            Evidence = new[] { identity.EvidenceSha256, "details-tag-pills" }
        };
    }

    public async Task<VerifiedSequenceState> AdvanceToNextPokemonAsync(
        PokemonIdentityConsensus previous, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var before = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var start = _automationProfile.NextPokemonSwipe.Start.ToPixels(width, height);
        var end = _automationProfile.NextPokemonSwipe.End.ToPixels(width, height);
        await _transport.SwipeAsync(_serial, start.X, start.Y, end.X, end.Y,
            _automationProfile.NextPokemonSwipe.DurationMilliseconds, cancellationToken);
        var after = await WaitForStateAsync(new[] { PokemonGoGameState.PokemonDetails }, cancellationToken);
        var overlap = _identityAnalyzer.Consensus(new[]
        {
            new PokemonIdentityFrame { ScreenshotPng = before.Screenshot },
            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot },
            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot }
        });
        var same = string.Equals(previous.StableFingerprintSha256, overlap.StableFingerprintSha256,
            StringComparison.Ordinal);
        await WriteAuditAsync("advance", new
        {
            Before = Convert.ToHexString(SHA256.HashData(before.Screenshot)).ToLowerInvariant(),
            After = Convert.ToHexString(SHA256.HashData(after.Screenshot)).ToLowerInvariant(),
            SamePokemon = same,
            Action = "one-allow-listed-swipe"
        }, cancellationToken);
        if (same) return VerifiedSequenceState.Unknown;
        _lastScreenshot = after.Screenshot;
        return VerifiedSequenceState.PokemonDetails;
    }

    public async Task<VerifiedSequenceState> ReturnToInventoryAsync(CancellationToken cancellationToken)
    {
        for (var action = 0; action < 3; action++)
        {
            var current = await WaitForStateAsync(
                new[] { PokemonGoGameState.Inventory, PokemonGoGameState.PokemonDetails,
                    PokemonGoGameState.PokemonMenu, PokemonGoGameState.Appraisal }, cancellationToken);
            if (current.State == PokemonGoGameState.Inventory) return VerifiedSequenceState.Inventory;
            await _transport.PressBackAsync(_serial, cancellationToken);
        }
        var result = await WaitForStateAsync(new[] { PokemonGoGameState.Inventory }, cancellationToken);
        return result.State == PokemonGoGameState.Inventory ? VerifiedSequenceState.Inventory : VerifiedSequenceState.Unknown;
    }

    public Task<IReadOnlyList<string>> ApplyIndexTagAsync(string tagName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Tag mutation is disabled for the read-only first acceptance.");

    public Task<IReadOnlyList<string>> ApplyClassificationTagAsync(string tagName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Tag mutation is disabled for the read-only first acceptance.");

    private async Task<(PokemonGoGameState State, byte[] Screenshot)> WaitForStateAsync(
        IReadOnlyCollection<PokemonGoGameState> expected, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_automationProfile.StateTimeoutSeconds);
        PokemonGoGameState? last = null;
        var consecutive = 0;
        byte[]? screenshot = null;
        while (DateTime.UtcNow < deadline)
        {
            screenshot = await CaptureAsync("state", cancellationToken);
            var detection = _detector.Detect(screenshot, _appraisalProfile);
            if (expected.Contains(detection.State))
            {
                consecutive = last == detection.State ? consecutive + 1 : 1;
                last = detection.State;
                if (consecutive >= 3) return (detection.State, screenshot);
            }
            else
            {
                last = detection.State;
                consecutive = 0;
                if (detection.State == PokemonGoGameState.Unknown)
                    await WriteAuditAsync("unknown", detection, cancellationToken);
            }
            await Task.Delay(_automationProfile.StatePollMilliseconds, cancellationToken);
        }
        return (last ?? PokemonGoGameState.Unknown, screenshot ?? Array.Empty<byte>());
    }

    private async Task TapAndVerifyAsync(NormalizedPoint point, PokemonGoGameState expected,
        string name, CancellationToken cancellationToken)
    {
        await TapNamedAsync(point, name, cancellationToken);
        var result = await WaitForStateAsync(new[] { expected }, cancellationToken);
        if (result.State != expected) throw new InvalidOperationException($"{name} postcondition was not verified.");
    }

    private async Task TapNamedAsync(NormalizedPoint point, string name, CancellationToken cancellationToken)
    {
        var (width, height) = await ScreenSizeAsync(cancellationToken);
        var (x, y) = point.ToPixels(width, height);
        await _transport.TapAsync(_serial, x, y, cancellationToken);
        await WriteAuditAsync(name, new { Action = "named-tap", X = x, Y = y }, cancellationToken);
    }

    private async Task<byte[]> CaptureAsync(string label, CancellationToken cancellationToken)
    {
        var screenshot = await _transport.CaptureScreenshotPngAsync(_serial, cancellationToken);
        _lastScreenshot = screenshot;
        return screenshot;
    }

    private async Task<AndroidDeviceMetadata> EnsureMetadataAsync(CancellationToken cancellationToken) =>
        _metadata ??= await _transport.ReadMetadataAsync(_serial, cancellationToken);

    private async Task<(int Width, int Height)> ScreenSizeAsync(CancellationToken cancellationToken)
    {
        var metadata = await EnsureMetadataAsync(cancellationToken);
        var width = metadata.Screen.EffectiveWidth ?? throw new InvalidOperationException("Android width unavailable.");
        var height = metadata.Screen.EffectiveHeight ?? throw new InvalidOperationException("Android height unavailable.");
        return (width, height);
    }

    private async Task<int> PixelXAsync(double x, CancellationToken cancellationToken)
    {
        var screen = await ScreenSizeAsync(cancellationToken);
        return new NormalizedPoint { X = x, Y = 0 }.ToPixels(screen.Width, 2).X;
    }

    private async Task<int> PixelYAsync(double y, CancellationToken cancellationToken)
    {
        var screen = await ScreenSizeAsync(cancellationToken);
        return new NormalizedPoint { X = 0, Y = y }.ToPixels(2, screen.Height).Y;
    }

    private async Task SaveEvidenceAsync(string label, byte[] screenshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_evidenceDirectory);
        var safe = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(_evidenceDirectory, $"{++_evidenceOrdinal:D4}-{safe}.png");
        await File.WriteAllBytesAsync(path, screenshot, cancellationToken);
    }

    private async Task WriteAuditAsync(string label, object value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_evidenceDirectory);
        var safe = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(_evidenceDirectory, $"{++_evidenceOrdinal:D4}-{safe}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
}
