using System.Text;
using PSVR2iRacingHaptics.Core.Services;

namespace PSVR2iRacingHaptics.App;

public static class StartupFailureReporter
{
    private const string FileName = "startup-crash.log";

    public static string Write(
        Exception exception,
        IEnumerable<string>? commandLineArguments = null,
        string? applicationDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var arguments = commandLineArguments?.ToArray() ?? [];
        var report = BuildReport(exception, arguments);
        var preferredPath = ResolvePreferredPath(arguments, applicationDirectory);
        if (TryAppend(preferredPath, report))
        {
            return preferredPath;
        }

        var fallbackPath = Path.Combine(
            Path.GetTempPath(),
            "PSVR2iRacingHaptics",
            FileName);
        return TryAppend(fallbackPath, report)
            ? fallbackPath
            : "No startup diagnostic file could be written.";
    }

    private static string ResolvePreferredPath(
        string[] arguments,
        string? applicationDirectory)
    {
        try
        {
            var paths = AppPaths.Resolve(arguments, applicationDirectory);
            return Path.Combine(paths.LogsDirectory, FileName);
        }
        catch
        {
            return Path.Combine(
                Path.GetTempPath(),
                "PSVR2iRacingHaptics",
                FileName);
        }
    }

    private static bool TryAppend(string path, string report)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.AppendAllText(
                path,
                report,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildReport(Exception exception, string[] arguments)
    {
        var version = typeof(StartupFailureReporter).Assembly
            .GetName()
            .Version?.ToString(3) ?? "unknown";
        var portable = arguments.Any(argument =>
            string.Equals(
                argument,
                "--portable",
                StringComparison.OrdinalIgnoreCase));
        var report = new StringBuilder();
        report.AppendLine("---");
        report.AppendLine($"Timestamp (UTC): {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"Application version: {version}");
        report.AppendLine($"Operating system: {Environment.OSVersion}");
        report.AppendLine($".NET runtime: {Environment.Version}");
        report.AppendLine($"Portable argument: {portable}");
        report.AppendLine("Startup exception:");
        report.AppendLine(exception.ToString());
        return report.ToString();
    }
}
