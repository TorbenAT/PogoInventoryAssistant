using System.Reflection;
using Microsoft.Data.Sqlite;
using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Persistence;

internal static class TagVerificationTests
{
public static async Task RunAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "pogo-tag-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var databasePath = Path.Combine(root, "inventory.db");
    try
    {
        var publicMethods = typeof(TagWorkflowService).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        AssertTrue(publicMethods
            .Where(method => method.Name == nameof(TagWorkflowService.RequestAndRecordAsync))
            .SelectMany(method => method.GetParameters())
            .All(parameter => parameter.ParameterType != typeof(bool)),
            "verified=true API no longer exists");

        var service = new TagWorkflowService(databasePath);
        await new InventoryPersistenceService(databasePath).InitializeAsync();
        var invalidResults = new[]
        {
            new TagExecutionResult { TagName = "AI-Indexed", Error = "invalid" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, Error = "invalid" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, Error = "invalid" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, AfterScreenshotHash = "after", AuditReference = "audit" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, BeforeScreenshotHash = "before", AuditReference = "audit" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, BeforeScreenshotHash = "same", AfterScreenshotHash = "same", AuditReference = "audit" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, BeforeScreenshotHash = "before", AfterScreenshotHash = "after" },
            new TagExecutionResult { TagName = "AI-Indexed", ActionExecuted = true, VisuallyVerified = true, BeforeScreenshotHash = "before", AfterScreenshotHash = "after", AuditReference = "audit", Error = "failure" }
        };
        foreach (var result in invalidResults)
        {
            AssertTrue(!result.IsCompleteVerification, "incomplete result cannot verify");
            await service.RequestAndRecordAsync("invalid", result);
        }
        AssertTrue(!await service.IsVerifiedAsync("invalid", "AI-Indexed"), "invalid result cannot be persisted as Verified");

        var valid = new TagExecutionResult
        {
            TagName = "AI-Review", ActionExecuted = true, VisuallyVerified = true,
            BeforeScreenshotHash = "before", AfterScreenshotHash = "after", AuditReference = "audit"
        };
        AssertTrue(valid.IsCompleteVerification, "complete result verifies");
        await service.RequestAndRecordAsync("valid", valid);
        AssertTrue(await service.IsVerifiedAsync("valid", "AI-Review"), "complete result is persisted as Verified");

        var coordinator = new RunCoordinator(databasePath);
        var item = new InventoryScanItem
        {
            SequenceNumber = 1, CapturedAtUtc = DateTimeOffset.UtcNow, ScreenshotFileName = "frame.png",
            ScreenshotSha256 = "screen", IdentityFingerprintBase64 = "fingerprint", IdentityFingerprintSha256 = "fingerprint",
            ScreenStateConfidence = 1
        };
        var cycle = await coordinator.CommitObservationAndTagsAsync("run", item, "frame.png");
        AssertEqual(0, cycle.VerifiedTags.Count, "coordinator cannot fabricate verification");
        AssertTrue(cycle.TagErrors.Count == 2, "missing executor records both tag failures");
        AssertTrue(!await service.IsVerifiedAsync("run:1", "AI-Indexed"), "missing executor never verifies");
    }
    finally
    {
        // SQLite may retain a pooled handle briefly on Windows; the unique temp
        // directory is intentionally left for the OS/user cleanup.
    }
}

private static void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

private static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
}
}
