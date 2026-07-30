using System.Globalization;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Infrastructure.IRacing;

/// <summary>
/// Extracts only the stable car and track identity fields needed for profile
/// selection. This intentionally avoids a general-purpose YAML dependency and
/// tolerates unrelated malformed SessionInfo content.
/// </summary>
public static class IRacingSessionInfoParser
{
    public static TelemetryContext Parse(string? yaml, int sessionInfoUpdate = -1)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new TelemetryContext { SessionInfoUpdate = sessionInfoUpdate };
        }

        var weekend = new Dictionary<string, string>(StringComparer.Ordinal);
        var drivers = new List<Dictionary<string, string>>();
        Dictionary<string, string>? currentDriver = null;
        string section = string.Empty;
        var inDrivers = false;
        int? driverCarIdx = null;

        foreach (var rawLine in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmed = rawLine.Trim();
            var indentation = rawLine.Length - rawLine.TrimStart().Length;
            if (indentation == 0 && trimmed.EndsWith(':'))
            {
                section = trimmed[..^1];
                inDrivers = false;
                currentDriver = null;
                continue;
            }

            if (section == "WeekendInfo")
            {
                if (TrySplit(trimmed, out var key, out var value))
                {
                    weekend[key] = Scalar(value);
                }
                continue;
            }

            if (section != "DriverInfo")
            {
                continue;
            }

            if (trimmed == "Drivers:")
            {
                inDrivers = true;
                currentDriver = null;
                continue;
            }

            if (!inDrivers)
            {
                if (TrySplit(trimmed, out var key, out var value)
                    && key == "DriverCarIdx"
                    && TryInt(Scalar(value), out var parsedIndex))
                {
                    driverCarIdx = parsedIndex;
                }
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                currentDriver = new Dictionary<string, string>(StringComparer.Ordinal);
                drivers.Add(currentDriver);
                trimmed = trimmed[2..].TrimStart();
            }

            if (currentDriver is not null
                && TrySplit(trimmed, out var driverKey, out var driverValue))
            {
                currentDriver[driverKey] = Scalar(driverValue);
            }
        }

        var driver = drivers.FirstOrDefault(candidate =>
            driverCarIdx.HasValue
            && candidate.TryGetValue("CarIdx", out var value)
            && TryInt(value, out var carIdx)
            && carIdx == driverCarIdx.Value);

        return new TelemetryContext
        {
            SessionInfoUpdate = sessionInfoUpdate,
            DriverCarIdx = driverCarIdx,
            CarId = GetInt(driver, "CarID"),
            CarClassId = GetInt(driver, "CarClassID"),
            CarPath = Get(driver, "CarPath"),
            CarName = FirstNonEmpty(
                Get(driver, "CarScreenName"),
                Get(driver, "CarScreenNameShort")),
            CarClass = Get(driver, "CarClassShortName"),
            TrackId = GetInt(weekend, "TrackID"),
            TrackName = Get(weekend, "TrackName"),
            TrackDisplayName = FirstNonEmpty(
                Get(weekend, "TrackDisplayName"),
                Get(weekend, "TrackDisplayShortName")),
            TrackConfigName = Get(weekend, "TrackConfigName")
        };
    }

    private static bool TrySplit(string line, out string key, out string value)
    {
        var separator = line.IndexOf(':');
        if (separator <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = line[..separator].Trim();
        value = line[(separator + 1)..].Trim();
        return key.Length > 0;
    }

    private static string Scalar(string value)
    {
        value = value.Trim();
        if (value.StartsWith("!!str ", StringComparison.Ordinal))
        {
            value = value[6..].TrimStart();
        }
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }
        return value;
    }

    private static string Get(
        IReadOnlyDictionary<string, string>? values,
        string key) =>
        values is not null && values.TryGetValue(key, out var value)
            ? value
            : string.Empty;

    private static int? GetInt(
        IReadOnlyDictionary<string, string>? values,
        string key) =>
        TryInt(Get(values, key), out var value) ? value : null;

    private static bool TryInt(string value, out int parsed) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out parsed);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
