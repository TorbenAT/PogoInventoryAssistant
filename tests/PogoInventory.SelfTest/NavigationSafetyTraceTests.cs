using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;

internal static class NavigationSafetyTraceTests
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "pogo-navigation-trace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var screenshot = FakeAndroidDeviceTransport.CreateDefaultScreenshotPng();
            var detection = new PokemonGoGameStateDetection
            {
                State = PokemonGoGameState.Inventory,
                Confidence = 1,
                Evidence = new[] { "synthetic" },
                ScreenshotSha256 = Convert.ToHexString(SHA256.HashData(screenshot)).ToLowerInvariant()
            };
            var recorder = new NavigationSafetyTraceRecorder(directory);
            recorder.BeginCycle(1);
            for (var index = 0; index < 4; index++)
                await recorder.ObserveFrameAsync(screenshot, detection, null, null, CancellationToken.None);
            await recorder.AuthorizeAsync(
                "guarded-back",
                "Inventory",
                "PokemonDetails",
                "AUTHORIZED",
                screenshot,
                detection,
                null,
                null,
                CancellationToken.None);
            await recorder.RecordInputSentAsync("PressBack", "Back", CancellationToken.None);
            for (var index = 0; index < 5; index++)
                await recorder.ObserveFrameAsync(screenshot, detection, null, null, CancellationToken.None);
            recorder.RecordPostcondition("Inventory", "PASS");
            await recorder.CompletePostFramesAsync(
                FakeAndroidDeviceTransport.CreateSingleAuthorized(),
                "FAKE-001",
                CancellationToken.None);

            var entries = File.ReadLines(Path.Combine(directory, "action-trace.jsonl"))
                .Select(line => JsonSerializer.Deserialize<NavigationSafetyTraceEntry>(line)!)
                .Where(entry => entry is not null)
                .Cast<NavigationSafetyTraceEntry>()
                .ToArray();
            Assert(entries.Count(entry => entry.Phase == "PRECONDITION_FRAME_1") == 1,
                "trace records one first precondition frame");
            Assert(entries.Count(entry => entry.Phase.StartsWith("POST_INPUT_FRAME_", StringComparison.Ordinal)) == 5,
                "trace records exactly five post-input frames");
            Assert(entries.Any(entry => entry.Phase == "INPUT_SENT"),
                "trace records transport input only after authorization");
            Assert(entries[^1].Phase == "POSTCONDITION",
                "postcondition is emitted after all post-input frames");
            Assert(entries.Select(entry => entry.MonotonicSequence)
                .SequenceEqual(Enumerable.Range(1, entries.Length).Select(value => (long)value)),
                "trace sequence is contiguous and monotonic");

            var hostPath = FindRepositoryFile("src", "PogoInventory.Exploration", "Services",
                "AndroidVerifiedInventoryNamedOperations.cs");
            var host = await File.ReadAllTextAsync(hostPath);
            Assert(host.Contains("SwipeAsync", StringComparison.Ordinal),
                "host retains the named swipe transport operation");
            Assert(host.Contains("ClassifySwipeProgression", StringComparison.Ordinal),
                "host retains guarded cursor classification");
            Assert(host.Contains("CompleteTraceAsync", StringComparison.Ordinal),
                "host resolves postcondition evidence before the next caller action");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found for navigation trace test.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
