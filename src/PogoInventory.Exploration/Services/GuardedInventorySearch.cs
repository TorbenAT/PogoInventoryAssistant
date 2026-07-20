using System.Security.Cryptography;
using PogoInventory.Automation.Models;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Exploration.Services;

public sealed record InventorySearchVisualEvidence
{
    public required string ScreenshotSha256 { get; init; }
    public required bool SearchFieldVisible { get; init; }
    public required bool KeyboardVisible { get; init; }
    public required bool QueryVisible { get; init; }
    public required bool ClearControlVisible { get; init; }
    public required int QueryInkPixels { get; init; }
    public required int QueryInkWidth { get; init; }
    public required string ResultSignature { get; init; }
}

public enum InventorySearchAction
{
    OpenSearch,
    ClearSearch,
    EnterQuery,
    SubmitQuery
}

public enum InventorySearchOutcome
{
    Progressed,
    Succeeded,
    UnsafePreState,
    ActionNotObserved,
    UnexpectedState
}

public sealed record InventorySearchAuthorization
{
    public required int Sequence { get; init; }
    public required InventorySearchAction Action { get; init; }
    public required string ExpectedPostcondition { get; init; }
}

/// <summary>
/// Owns the bounded input sequence for replacing and submitting an inventory
/// search. It never interprets Android shell syntax.
/// </summary>
public sealed class GuardedInventorySearch
{
    private enum Phase
    {
        NotStarted,
        NeedEditor,
        NeedClear,
        NeedText,
        NeedSubmit,
        Complete,
        Terminal
    }

    private Phase _phase;
    private InventorySearchVisualEvidence? _current;
    private InventorySearchAuthorization? _pending;
    private string? _blankResultSignature;

    public int InputActions { get; private set; }
    public string? Query { get; private set; }

    public InventorySearchOutcome Begin(
        InventorySearchVisualEvidence evidence,
        string query)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Query = InventorySearchQuery.Validate(query);
        ResetRuntime();
        _current = evidence;
        if (!evidence.SearchFieldVisible)
        {
            _phase = Phase.Terminal;
            return InventorySearchOutcome.UnsafePreState;
        }

        _phase = evidence.KeyboardVisible
            ? evidence.QueryVisible ? Phase.NeedClear : Phase.NeedText
            : evidence.QueryVisible ? Phase.NeedClear : Phase.NeedEditor;
        if (!evidence.QueryVisible)
        {
            _blankResultSignature = evidence.ResultSignature;
        }
        return InventorySearchOutcome.Progressed;
    }

    public InventorySearchAuthorization? AuthorizeNextAction()
    {
        if (_pending is not null || _phase is Phase.Complete or Phase.Terminal or Phase.NotStarted)
        {
            return null;
        }

        var action = _phase switch
        {
            Phase.NeedEditor => InventorySearchAction.OpenSearch,
            Phase.NeedClear => InventorySearchAction.ClearSearch,
            Phase.NeedText => InventorySearchAction.EnterQuery,
            Phase.NeedSubmit => InventorySearchAction.SubmitQuery,
            _ => throw new InvalidOperationException($"Unsupported search phase '{_phase}'.")
        };
        InputActions++;
        _pending = new InventorySearchAuthorization
        {
            Sequence = InputActions,
            Action = action,
            ExpectedPostcondition = action switch
            {
                InventorySearchAction.OpenSearch => "keyboard-visible empty search editor",
                InventorySearchAction.ClearSearch => "empty search field",
                InventorySearchAction.EnterQuery => "visibly populated search field",
                InventorySearchAction.SubmitQuery => "keyboard hidden and filtered result stable",
                _ => throw new InvalidOperationException()
            }
        };
        return _pending;
    }

    public InventorySearchOutcome ObservePostAction(InventorySearchVisualEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (_pending is null || _current is null)
        {
            throw new InvalidOperationException("No inventory-search action is awaiting evidence.");
        }

        var action = _pending.Action;
        _pending = null;
        _current = evidence;
        if (!evidence.SearchFieldVisible)
        {
            _phase = Phase.Terminal;
            return InventorySearchOutcome.UnexpectedState;
        }

        var observed = action switch
        {
            InventorySearchAction.OpenSearch => evidence.KeyboardVisible && !evidence.QueryVisible,
            InventorySearchAction.ClearSearch => !evidence.QueryVisible,
            InventorySearchAction.EnterQuery => evidence.KeyboardVisible && evidence.QueryVisible &&
                IsQueryLengthCompatible(evidence, Query!),
            InventorySearchAction.SubmitQuery =>
                !evidence.KeyboardVisible && evidence.QueryVisible &&
                IsQueryLengthCompatible(evidence, Query!) &&
                _blankResultSignature is not null &&
                evidence.ResultSignature != _blankResultSignature,
            _ => false
        };
        if (!observed)
        {
            _phase = Phase.Terminal;
            return InventorySearchOutcome.ActionNotObserved;
        }

        _phase = action switch
        {
            InventorySearchAction.OpenSearch => Phase.NeedText,
            InventorySearchAction.ClearSearch => evidence.KeyboardVisible
                ? Phase.NeedText
                : Phase.NeedEditor,
            InventorySearchAction.EnterQuery => Phase.NeedSubmit,
            InventorySearchAction.SubmitQuery => Phase.Complete,
            _ => Phase.Terminal
        };
        if (action == InventorySearchAction.ClearSearch)
        {
            _blankResultSignature = evidence.ResultSignature;
        }
        return _phase == Phase.Complete
            ? InventorySearchOutcome.Succeeded
            : InventorySearchOutcome.Progressed;
    }

    public static bool IsQueryLengthCompatible(
        InventorySearchVisualEvidence evidence,
        string expectedQuery)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedQuery);
        return evidence.QueryVisible &&
            evidence.QueryInkWidth >= expectedQuery.Length * 6 &&
            evidence.QueryInkWidth <= expectedQuery.Length * 42;
    }

    private void ResetRuntime()
    {
        _phase = Phase.NotStarted;
        _current = null;
        _pending = null;
        _blankResultSignature = null;
        InputActions = 0;
    }
}

public sealed class InventorySearchVisualAnalyzer
{
    public InventorySearchVisualEvidence Analyze(byte[] screenshotPng)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var image = PngDecoder.Decode(screenshotPng);
        var fieldBackground = Ratio(image, 0.15, 0.15, 0.93, 0.215, IsSearchBackground);
        var query = MeasureInk(image, 0.24, 0.155, 0.80, 0.21);
        var clearInk = Ratio(image, 0.86, 0.155, 0.93, 0.215, IsDarkInk);
        var keyboardLight = Ratio(image, 0.02, 0.57, 0.98, 0.91, IsKeyboardKey);
        var keyboardInk = Ratio(image, 0.02, 0.57, 0.98, 0.91, IsDarkInk);

        var clearVisible = clearInk >= 0.010;
        return new InventorySearchVisualEvidence
        {
            ScreenshotSha256 = Convert.ToHexString(SHA256.HashData(screenshotPng)).ToLowerInvariant(),
            SearchFieldVisible = fieldBackground >= 0.45,
            KeyboardVisible = keyboardLight >= 0.35 && keyboardInk >= 0.015,
            QueryVisible = clearVisible && query.Count >= 80 && query.Width >= 12,
            ClearControlVisible = clearVisible,
            QueryInkPixels = query.Count,
            QueryInkWidth = query.Width,
            ResultSignature = StableRegionSignature(image, 0.05, 0.23, 0.95, 0.56)
        };
    }

    private static (int Count, int Width) MeasureInk(
        PixelImage image, double x1, double y1, double x2, double y2)
    {
        var left = (int)(image.Width * x1);
        var right = Math.Min(image.Width, (int)(image.Width * x2));
        var top = (int)(image.Height * y1);
        var bottom = Math.Min(image.Height, (int)(image.Height * y2));
        var count = 0;
        var minimumX = right;
        var maximumX = left;
        for (var y = top; y < bottom; y += 2)
        {
            for (var x = left; x < right; x += 2)
            {
                if (!IsDarkInk(image.GetPixel(x, y)))
                {
                    continue;
                }
                count++;
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
            }
        }
        return (count, count == 0 ? 0 : maximumX - minimumX + 1);
    }

    private static double Ratio(
        PixelImage image, double x1, double y1, double x2, double y2,
        Func<Rgba32, bool> predicate)
    {
        var matched = 0;
        var total = 0;
        for (var y = (int)(image.Height * y1); y < (int)(image.Height * y2); y += 4)
        {
            for (var x = (int)(image.Width * x1); x < (int)(image.Width * x2); x += 4)
            {
                total++;
                if (predicate(image.GetPixel(x, y)))
                {
                    matched++;
                }
            }
        }
        return total == 0 ? 0 : matched / (double)total;
    }

    private static string StableRegionSignature(
        PixelImage image, double x1, double y1, double x2, double y2)
    {
        var bytes = new byte[16 * 8];
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 16; column++)
            {
                var x = (int)(image.Width * (x1 + ((column + 0.5) * (x2 - x1) / 16)));
                var y = (int)(image.Height * (y1 + ((row + 0.5) * (y2 - y1) / 8)));
                var pixel = image.GetPixel(
                    Math.Clamp(x, 0, image.Width - 1),
                    Math.Clamp(y, 0, image.Height - 1));
                bytes[(row * 16) + column] = (byte)((pixel.R + pixel.G + pixel.B) / 3);
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsSearchBackground(Rgba32 pixel) =>
        pixel.R >= 190 && pixel.G >= 205 && pixel.B >= 175 &&
        pixel.G >= pixel.R - 15 && pixel.G >= pixel.B;

    private static bool IsKeyboardKey(Rgba32 pixel) =>
        pixel.R >= 195 && pixel.G >= 195 && pixel.B >= 195 &&
        Math.Abs(pixel.R - pixel.G) <= 8 &&
        Math.Abs(pixel.G - pixel.B) <= 8;

    private static bool IsDarkInk(Rgba32 pixel) =>
        pixel.R <= 115 && pixel.G <= 145 && pixel.B <= 155;
}
