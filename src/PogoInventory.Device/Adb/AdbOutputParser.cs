using System.Globalization;
using System.Text.RegularExpressions;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;

namespace PogoInventory.Device.Adb;

public static partial class AdbOutputParser
{
    public static IReadOnlyList<AndroidDeviceDescriptor> ParseDeviceList(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var devices = new List<AndroidDeviceDescriptor>();

        foreach (var rawLine in SplitLines(output))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 ||
                line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase) ||
                line[0] == '*')
            {
                continue;
            }

            var parts = WhitespaceRegex().Split(line);
            if (parts.Length < 2)
            {
                throw new DeviceHarnessException(
                    DeviceErrorCode.InvalidAdbOutput,
                    $"Could not parse ADB device line: '{line}'.");
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in parts.Skip(2))
            {
                var colonIndex = part.IndexOf(':');
                if (colonIndex <= 0 || colonIndex == part.Length - 1)
                {
                    continue;
                }

                attributes[part[..colonIndex]] = part[(colonIndex + 1)..];
            }

            attributes.TryGetValue("product", out var product);
            attributes.TryGetValue("model", out var model);
            attributes.TryGetValue("device", out var device);
            attributes.TryGetValue("transport_id", out var transportId);

            devices.Add(new AndroidDeviceDescriptor
            {
                Serial = parts[0],
                State = ParseState(parts[1]),
                Product = NormalizeAttribute(product),
                Model = NormalizeAttribute(model),
                Device = NormalizeAttribute(device),
                TransportId = transportId,
                Attributes = attributes
            });
        }

        return devices;
    }

    public static IReadOnlyDictionary<string, string> ParseGetProperties(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in SplitLines(output))
        {
            var match = GetPropertyRegex().Match(rawLine.Trim());
            if (match.Success)
            {
                properties[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
        }

        return properties;
    }

    public static AndroidScreenInfo ParseScreenInfo(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var physical = PhysicalSizeRegex().Match(output);
        var overrideSize = OverrideSizeRegex().Match(output);

        if (!physical.Success && !overrideSize.Success)
        {
            throw new DeviceHarnessException(
                DeviceErrorCode.InvalidAdbOutput,
                "ADB returned no recognisable screen size.");
        }

        return new AndroidScreenInfo
        {
            PhysicalWidth = ParseGroup(physical, "width"),
            PhysicalHeight = ParseGroup(physical, "height"),
            OverrideWidth = ParseGroup(overrideSize, "width"),
            OverrideHeight = ParseGroup(overrideSize, "height")
        };
    }

    public static AndroidBatteryInfo ParseBatteryInfo(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in SplitLines(output))
        {
            var line = rawLine.Trim();
            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0 || colonIndex == line.Length - 1)
            {
                continue;
            }

            values[line[..colonIndex].Trim()] = line[(colonIndex + 1)..].Trim();
        }

        var level = ParseInt(values, "level");
        var scale = ParseInt(values, "scale");
        int? percentage = null;
        if (level is not null)
        {
            percentage = scale.HasValue && scale.Value > 0
                ? (int)Math.Round(level.Value * 100m / scale.Value, MidpointRounding.AwayFromZero)
                : level;
        }

        var temperatureTenths = ParseInt(values, "temperature");
        var statusCode = ParseInt(values, "status");
        values.TryGetValue("technology", out var technology);

        return new AndroidBatteryInfo
        {
            LevelPercent = percentage,
            TemperatureCelsius = temperatureTenths is null
                ? null
                : temperatureTenths.Value / 10m,
            StatusCode = statusCode,
            StatusName = StatusName(statusCode),
            HealthCode = ParseInt(values, "health"),
            AcPowered = ParseBool(values, "AC powered"),
            UsbPowered = ParseBool(values, "USB powered"),
            WirelessPowered = ParseBool(values, "Wireless powered"),
            Present = ParseBool(values, "present"),
            Technology = technology
        };
    }

    private static AndroidDeviceState ParseState(string value) =>
        value.ToLowerInvariant() switch
        {
            "device" => AndroidDeviceState.Authorized,
            "unauthorized" => AndroidDeviceState.Unauthorized,
            "offline" => AndroidDeviceState.Offline,
            "recovery" => AndroidDeviceState.Recovery,
            "sideload" => AndroidDeviceState.Sideload,
            "bootloader" => AndroidDeviceState.Bootloader,
            _ => AndroidDeviceState.Unknown
        };

    private static string? NormalizeAttribute(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace('_', ' ');

    private static int? ParseGroup(Match match, string groupName) =>
        match.Success && int.TryParse(
            match.Groups[groupName].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;

    private static int? ParseInt(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        values.TryGetValue(key, out var raw) &&
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool? ParseBool(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        if (!values.TryGetValue(key, out var raw))
        {
            return null;
        }

        return bool.TryParse(raw, out var value) ? value : null;
    }

    private static string? StatusName(int? statusCode) =>
        statusCode switch
        {
            1 => "Unknown",
            2 => "Charging",
            3 => "Discharging",
            4 => "Not charging",
            5 => "Full",
            _ => null
        };

    private static IEnumerable<string> SplitLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\[(?<key>[^\]]+)\]: \[(?<value>.*)\]$")]
    private static partial Regex GetPropertyRegex();

    [GeneratedRegex(@"Physical size:\s*(?<width>\d+)x(?<height>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PhysicalSizeRegex();

    [GeneratedRegex(@"Override size:\s*(?<width>\d+)x(?<height>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex OverrideSizeRegex();
}
