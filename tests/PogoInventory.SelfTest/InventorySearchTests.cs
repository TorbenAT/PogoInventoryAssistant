using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;

internal static class InventorySearchTests
{
    public static async Task RunEncodingAsync()
    {
        AssertEqual("age0-7", AndroidInputTextEncoder.EncodeInventorySearchQuery("age0-7"));
        AssertEqual("%s#Trade", AndroidInputTextEncoder.EncodeInventorySearchQuery("#Trade"));
        AssertEqual("!\\#Trade", AndroidInputTextEncoder.EncodeInventorySearchQuery("!#Trade"));
        AssertEqual("name%swith%sboxes", AndroidInputTextEncoder.EncodeInventorySearchQuery("name with boxes"));
        AssertThrows(() => AndroidInputTextEncoder.EncodeInventorySearchQuery("age0-7\n"));
        AssertEqual(
            "x\\;input%stap%s1%s1",
            AndroidInputTextEncoder.EncodeInventorySearchQuery("x;input tap 1 1"));

        var runner = new RecordingAdbProcessRunner(new[] { Success(), Success() });
        var transport = new AdbAndroidDeviceTransport(
            runner,
            new DeviceHarnessOptions { CommandTimeout = TimeSpan.FromSeconds(2) });
        await transport.EnterInventorySearchQueryAsync("ABC", "#Trade");
        await transport.SubmitInventorySearchQueryAsync("ABC");
        AssertEqual(
            "-s ABC shell input text %s#Trade",
            string.Join(" ", runner.Commands[0]));
        AssertEqual(
            "-s ABC shell input keyevent KEYCODE_ENTER",
            string.Join(" ", runner.Commands[1]));
    }

    public static Task RunWorkflowAsync()
    {
        var workflow = new GuardedInventorySearch();
        AssertEqual(
            InventorySearchOutcome.Progressed,
            workflow.Begin(Evidence(query: true, keyboard: false, result: "old"), "age0-7"));
        AssertEqual(InventorySearchAction.ClearSearch, workflow.AuthorizeNextAction()?.Action);
        AssertEqual(
            InventorySearchOutcome.Progressed,
            workflow.ObservePostAction(Evidence(query: false, keyboard: false, result: "blank")));
        AssertEqual(InventorySearchAction.OpenSearch, workflow.AuthorizeNextAction()?.Action);
        AssertEqual(
            InventorySearchOutcome.Progressed,
            workflow.ObservePostAction(Evidence(query: false, keyboard: true, result: "blank")));
        AssertEqual(InventorySearchAction.EnterQuery, workflow.AuthorizeNextAction()?.Action);
        AssertEqual(
            InventorySearchOutcome.Progressed,
            workflow.ObservePostAction(Evidence(query: true, keyboard: true, result: "filtered")));
        AssertEqual(InventorySearchAction.SubmitQuery, workflow.AuthorizeNextAction()?.Action);
        AssertEqual(
            InventorySearchOutcome.Succeeded,
            workflow.ObservePostAction(Evidence(query: true, keyboard: false, result: "filtered")));
        AssertEqual(4, workflow.InputActions);
        AssertTrue(workflow.AuthorizeNextAction() is null, "completed workflow authorizes no input");

        var missing = new GuardedInventorySearch();
        AssertEqual(
            InventorySearchOutcome.UnsafePreState,
            missing.Begin(Evidence(field: false), "#Trade"));
        AssertTrue(missing.AuthorizeNextAction() is null, "unsafe pre-state authorizes no input");

        var unchanged = new GuardedInventorySearch();
        unchanged.Begin(Evidence(query: false, keyboard: false, result: "blank"), "#Trade");
        unchanged.AuthorizeNextAction();
        AssertEqual(
            InventorySearchOutcome.ActionNotObserved,
            unchanged.ObservePostAction(Evidence(query: false, keyboard: false, result: "blank")));
        AssertTrue(unchanged.AuthorizeNextAction() is null, "failed action cannot loop");

        AssertTrue(
            InventorySearchVisualAnalyzer.IsPotentialPostcondition(
                InventorySearchAction.OpenSearch,
                Evidence(query: false, keyboard: true),
                "age0-1825"),
            "Open-search postcondition did not accept a visible empty editor.");
        AssertTrue(
            !InventorySearchVisualAnalyzer.IsPotentialPostcondition(
                InventorySearchAction.OpenSearch,
                Evidence(query: false, keyboard: false),
                "age0-1825"),
            "Open-search postcondition accepted a frame before the keyboard appeared.");
        AssertTrue(
            InventorySearchVisualAnalyzer.IsPotentialPostcondition(
                InventorySearchAction.EnterQuery,
                Evidence(query: true, keyboard: true) with { QueryInkWidth = 80 },
                "age0-1825"),
            "Enter-query postcondition rejected compatible visible query evidence.");
        var locator = new VisualControlLocator();
        AssertTrue(locator.LocateInventoryCard(PngEncoder.Encode(InventoryFixture(hasCard: true))) is not null &&
            !locator.IsVerifiedEmptyInventorySearchResult(PngEncoder.Encode(InventoryFixture(hasCard: true))),
            "a visible first-row card is never reclassified as an empty search");
        AssertTrue(locator.LocateInventoryCard(PngEncoder.Encode(InventoryFixture(hasCard: false))) is null &&
            locator.IsVerifiedEmptyInventorySearchResult(PngEncoder.Encode(InventoryFixture(hasCard: false))),
            "three-cell empty inventory geometry becomes an oracle empty proof before any first-card tap");
        AssertTrue(PokemonGoGameState.Inventory ==
            new PokemonGoGameStateDetector().Detect(PngEncoder.Encode(InventoryFixture(hasCard: false))).State,
            "a verified empty Inventory search cannot fall through to MainMenu routing");
        return Task.CompletedTask;
    }

    private static PixelImage InventoryFixture(bool hasCard)
    {
        const int width = 360;
        const int height = 720;
        var rgba = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
        for (var index = 3; index < rgba.Length; index += 4) rgba[index] = 255;
        Fill(rgba, width, height, .04, .24, .96, .90, 220, 230, 210);
        Fill(rgba, width, height, .08, .15, .92, .22, 210, 225, 205);
        Fill(rgba, width, height, .20, .08, .80, .15, 190, 200, 190);
        if (hasCard)
        {
            Fill(rgba, width, height, .08, .34, .28, .44, 35, 125, 150);
            Fill(rgba, width, height, .38, .34, .58, .44, 35, 125, 150);
            Fill(rgba, width, height, .70, .34, .90, .44, 35, 125, 150);
        }
        return new PixelImage(width, height, rgba);
    }

    private static void Fill(byte[] rgba, int width, int height, double left, double top, double right, double bottom,
        byte r, byte g, byte b)
    {
        for (var y = (int)(height * top); y < (int)(height * bottom); y++)
        for (var x = (int)(width * left); x < (int)(width * right); x++)
        {
            var offset = (y * width + x) * 4;
            rgba[offset] = r; rgba[offset + 1] = g; rgba[offset + 2] = b; rgba[offset + 3] = 255;
        }
    }

    private static InventorySearchVisualEvidence Evidence(
        bool field = true,
        bool query = false,
        bool keyboard = false,
        string result = "blank") =>
        new()
        {
            ScreenshotSha256 = "sha",
            SearchFieldVisible = field,
            KeyboardVisible = keyboard,
            QueryVisible = query,
            ClearControlVisible = query,
            QueryInkPixels = query ? 200 : 0,
            QueryInkWidth = query ? 80 : 0,
            ResultSignature = result
        };

    private static AdbProcessResult Success() =>
        new() { ExitCode = 0, StandardOutput = Array.Empty<byte>(), StandardError = string.Empty };

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', received '{actual}'.");
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("Expected ArgumentException.");
    }
}
