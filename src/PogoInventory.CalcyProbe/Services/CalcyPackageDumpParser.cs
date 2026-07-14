using System.Globalization;
using System.Text.RegularExpressions;
using PogoInventory.CalcyProbe.Models;

namespace PogoInventory.CalcyProbe.Services;

public static partial class CalcyPackageDumpParser
{
    public static CalcyPackageMetadata Parse(
        string packageName,
        string packageDump,
        string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        packageDump ??= string.Empty;
        packagePath ??= string.Empty;

        var installed = packagePath.Contains("package:", StringComparison.OrdinalIgnoreCase) ||
            packageDump.Contains($"Package [{packageName}]", StringComparison.Ordinal) ||
            packageDump.Contains($"pkg=Package{{", StringComparison.Ordinal) &&
            packageDump.Contains(packageName, StringComparison.Ordinal);

        var activities = new HashSet<string>(StringComparer.Ordinal);
        var services = new HashSet<string>(StringComparer.Ordinal);
        var receivers = new HashSet<string>(StringComparer.Ordinal);
        var requestedPermissions = new HashSet<string>(StringComparer.Ordinal);
        var grantedPermissions = new HashSet<string>(StringComparer.Ordinal);

        var section = PackageDumpSection.None;
        foreach (var rawLine in NormalizeLines(packageDump))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            section = IdentifySection(trimmed, section);

            foreach (Match match in ComponentRegex().Matches(trimmed))
            {
                var component = match.Value;
                switch (section)
                {
                    case PackageDumpSection.Activity:
                        activities.Add(component);
                        break;
                    case PackageDumpSection.Service:
                        services.Add(component);
                        break;
                    case PackageDumpSection.Receiver:
                        receivers.Add(component);
                        break;
                }
            }

            if (section == PackageDumpSection.RequestedPermissions &&
                PermissionRegex().Match(trimmed) is { Success: true } permissionMatch)
            {
                requestedPermissions.Add(permissionMatch.Value);
            }

            if (section is PackageDumpSection.InstallPermissions or PackageDumpSection.RuntimePermissions &&
                PermissionGrantRegex().Match(trimmed) is { Success: true } grantedMatch &&
                string.Equals(
                    grantedMatch.Groups["granted"].Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                grantedPermissions.Add(grantedMatch.Groups["permission"].Value);
            }
        }

        return new CalcyPackageMetadata
        {
            PackageName = packageName,
            IsInstalled = installed,
            VersionName = MatchValue(packageDump, VersionNameRegex()),
            VersionCode = ParseLong(MatchValue(packageDump, VersionCodeRegex())),
            TargetSdk = ParseInt(MatchValue(packageDump, TargetSdkRegex())),
            MinSdk = ParseInt(MatchValue(packageDump, MinSdkRegex())),
            UserId = ParseInt(MatchValue(packageDump, UserIdRegex())),
            Enabled = ParseEnabled(MatchValue(packageDump, EnabledRegex())),
            FirstInstallTime = ParseTimestamp(MatchValue(packageDump, FirstInstallTimeRegex())),
            LastUpdateTime = ParseTimestamp(MatchValue(packageDump, LastUpdateTimeRegex())),
            Activities = activities.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Services = services.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Receivers = receivers.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            RequestedPermissions = requestedPermissions.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            GrantedPermissions = grantedPermissions.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static IEnumerable<string> NormalizeLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static PackageDumpSection IdentifySection(
        string line,
        PackageDumpSection current)
    {
        if (line.Contains("Activity Resolver Table", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("Activities:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.Activity;
        }

        if (line.Contains("Service Resolver Table", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("Services:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.Service;
        }

        if (line.Contains("Receiver Resolver Table", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("Receivers:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.Receiver;
        }

        if (line.Equals("requested permissions:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.RequestedPermissions;
        }

        if (line.Equals("install permissions:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.InstallPermissions;
        }

        if (line.EndsWith("runtime permissions:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.RuntimePermissions;
        }

        if (line.Equals("Packages:", StringComparison.OrdinalIgnoreCase))
        {
            return PackageDumpSection.None;
        }

        return current;
    }

    private static string? MatchValue(string input, Regex expression)
    {
        var match = expression.Match(input);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static bool? ParseEnabled(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "true" or "1" or "enabled" => true,
            "false" or "0" or "disabled" => false,
            _ => null
        };

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var result)
            ? result
            : null;
    }

    private enum PackageDumpSection
    {
        None,
        Activity,
        Service,
        Receiver,
        RequestedPermissions,
        InstallPermissions,
        RuntimePermissions
    }

    [GeneratedRegex(@"[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+/[A-Za-z0-9_.$]+")]
    private static partial Regex ComponentRegex();

    [GeneratedRegex(@"android\.permission\.[A-Z0-9_]+")]
    private static partial Regex PermissionRegex();

    [GeneratedRegex(@"(?<permission>android\.permission\.[A-Z0-9_]+):\s+granted=(?<granted>true|false)", RegexOptions.IgnoreCase)]
    private static partial Regex PermissionGrantRegex();

    [GeneratedRegex(@"\bversionName=(?<value>[^\s]+)")]
    private static partial Regex VersionNameRegex();

    [GeneratedRegex(@"\bversionCode=(?<value>\d+)")]
    private static partial Regex VersionCodeRegex();

    [GeneratedRegex(@"\btargetSdk=(?<value>\d+)")]
    private static partial Regex TargetSdkRegex();

    [GeneratedRegex(@"\bminSdk=(?<value>\d+)")]
    private static partial Regex MinSdkRegex();

    [GeneratedRegex(@"\buserId=(?<value>\d+)")]
    private static partial Regex UserIdRegex();

    [GeneratedRegex(@"\benabled=(?<value>[^\s]+)")]
    private static partial Regex EnabledRegex();

    [GeneratedRegex(@"\bfirstInstallTime=(?<value>[^\r\n]+)")]
    private static partial Regex FirstInstallTimeRegex();

    [GeneratedRegex(@"\blastUpdateTime=(?<value>[^\r\n]+)")]
    private static partial Regex LastUpdateTimeRegex();
}
