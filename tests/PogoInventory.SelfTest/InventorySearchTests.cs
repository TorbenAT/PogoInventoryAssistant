using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;

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
        return Task.CompletedTask;
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
