internal static class VerifiedSequenceRuntimeTests
{
    public static Task RunAsync()
    {
        var root = FindRepositoryRoot();
        var host = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Exploration",
            "Services", "AndroidVerifiedInventoryNamedOperations.cs"));
        var sequence = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Automation",
            "Services", "VerifiedInventoryTaskSequence.cs"));
        Assert(host.Contains("CaptureRecoveryFramesAsync", StringComparison.Ordinal),
            "host uses guarded recovery frame consensus");
        Assert(host.Contains("DecideAppraisalContinuation", StringComparison.Ordinal),
            "appraisal intro continuation delegates to GuardedInventoryRecovery");
        Assert(host.Contains("ExecuteRecoveryActionAsync", StringComparison.Ordinal),
            "recovery actions are authorized before input");
        var exitStart = host.IndexOf("ExitAppraisalAsync", StringComparison.Ordinal);
        var exitEnd = host.IndexOf("ReadTagObservationAsync", exitStart, StringComparison.Ordinal);
        var exit = host[exitStart..exitEnd];
        Assert(!exit.Contains("PressBackAsync", StringComparison.Ordinal),
            "appraisal exit never sends Back");
        var returnStart = host.IndexOf("ReturnToInventoryAsync", StringComparison.Ordinal);
        var returnEnd = host.IndexOf("ApplyIndexTagAsync", returnStart, StringComparison.Ordinal);
        var recovery = host[returnStart..returnEnd];
        Assert(recovery.Contains("_recovery.Begin", StringComparison.Ordinal) &&
            recovery.Contains("AuthorizeNextAction", StringComparison.Ordinal) &&
            recovery.Contains("ObservePostAction", StringComparison.Ordinal),
            "return to Inventory delegates all actions to guarded recovery");
        Assert(!recovery.Contains("for (var action = 0; action < 3", StringComparison.Ordinal),
            "return to Inventory has no blind Back loop");
        var advanceStart = host.IndexOf("AdvanceToNextPokemonAsync", StringComparison.Ordinal);
        var advanceEnd = host.IndexOf("ReturnToInventoryAsync", advanceStart, StringComparison.Ordinal);
        var advance = host[advanceStart..advanceEnd];
        Assert(advance.Contains("ObserveSwipeTransitionAsync", StringComparison.Ordinal) &&
            advance.Contains("CaptureIndependentDetailsFramesAsync", StringComparison.Ordinal),
            "cursor requires observed transition and three independent post frames");
        Assert(!advance.Contains("before.Screenshot },\n            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot },\n            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot", StringComparison.Ordinal),
            "cursor does not synthesize before/after/after consensus");
        Assert(sequence.Contains("ControlledStopped", StringComparison.Ordinal) &&
            sequence.Contains("TerminalUnknown", StringComparison.Ordinal) &&
            sequence.Contains("Completed", StringComparison.Ordinal),
            "checkpoint states distinguish controlled, terminal and completed outcomes");
        return Task.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
