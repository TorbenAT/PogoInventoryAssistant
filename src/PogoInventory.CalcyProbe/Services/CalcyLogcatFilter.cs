using System.Globalization;
using System.Text.RegularExpressions;

namespace PogoInventory.CalcyProbe.Services;

public static partial class CalcyLogcatFilter
{
    public static IReadOnlyList<string> Filter(
        string logcat,
        string packageName,
        string? processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        logcat ??= string.Empty;

        var pid = ParsePid(processId);
        var terms = new[]
        {
            packageName,
            "calcy",
            "tesmath"
        };

        return logcat.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                terms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (pid is not null && LineHasPid(line, pid.Value)))
            .ToArray();
    }

    private static int? ParsePid(string? value)
    {
        var first = value?.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            ? pid
            : null;
    }

    private static bool LineHasPid(string line, int pid)
    {
        var match = ThreadtimePidRegex().Match(line);
        return match.Success &&
            int.TryParse(
                match.Groups["pid"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var actual) &&
            actual == pid;
    }

    [GeneratedRegex(@"^\s*\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+\s+(?<pid>\d+)\s+\d+")]
    private static partial Regex ThreadtimePidRegex();
}
