using System.Text;
using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

public static class ExplorationReportWriter
{
    public static async Task WriteSummaryAsync(
        string path,
        ExplorationSession session,
        ExplorationCoverage coverage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(coverage);
        var text = new StringBuilder()
            .AppendLine("# UI Exploration Summary")
            .AppendLine()
            .AppendLine($"- Session: `{session.SessionId}`")
            .AppendLine($"- Device: `{session.DeviceSerial}`")
            .AppendLine($"- Actions: {coverage.ActionCount}")
            .AppendLine($"- Observations: {coverage.ObservationCount}")
            .AppendLine($"- Verified states: {coverage.VerifiedStateCount}")
            .AppendLine($"- Verified transitions: {coverage.VerifiedTransitionCount}")
            .AppendLine($"- Recovery routes: {coverage.RecoveryRouteCount}")
            .AppendLine()
            .AppendLine("No irreversible action is represented by this report unless separately audited and approved.")
            .ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, text, cancellationToken);
    }
}
