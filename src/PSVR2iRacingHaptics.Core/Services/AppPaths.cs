namespace PSVR2iRacingHaptics.Core.Services;

public sealed record AppPaths(
    string DataDirectory,
    string SettingsFile,
    string LogsDirectory,
    string RecordingsDirectory,
    bool IsPortable)
{
    public static AppPaths Resolve(
        IEnumerable<string>? commandLineArguments = null,
        string? applicationDirectory = null)
    {
        var baseDirectory = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
        var portable = commandLineArguments?.Any(
            x => string.Equals(x, "--portable", StringComparison.OrdinalIgnoreCase)) == true
            || File.Exists(Path.Combine(baseDirectory, "portable.mode"));

        var dataDirectory = portable
            ? Path.Combine(baseDirectory, "data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PSVR2iRacingHaptics");

        return new AppPaths(
            dataDirectory,
            Path.Combine(dataDirectory, "settings.json"),
            Path.Combine(dataDirectory, "logs"),
            Path.Combine(dataDirectory, "recordings"),
            portable);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RecordingsDirectory);
    }
}
