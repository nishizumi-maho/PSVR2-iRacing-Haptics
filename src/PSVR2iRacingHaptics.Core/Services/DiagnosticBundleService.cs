using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Core.Services;

public sealed record DiagnosticBundleResult(
    string Path,
    int LogFileCount,
    bool IncludedRecording,
    long SizeBytes);

public static partial class DiagnosticBundleService
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private const long MaximumRecordingBytes = 25 * 1024 * 1024;

    public static async Task<DiagnosticBundleResult> CreateAsync(
        string outputPath,
        AppPaths paths,
        AppSettings settings,
        string applicationVersion,
        string? recordingPath = null,
        CancellationToken cancellationToken = default)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidOperationException("Invalid diagnostic bundle path."));

        var logs = Directory.Exists(paths.LogsDirectory)
            ? Directory.EnumerateFiles(paths.LogsDirectory, "*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Where(file => file.Length <= MaximumLogBytes)
                .Take(5)
                .ToArray()
            : Array.Empty<FileInfo>();
        var includeRecording = recordingPath is not null
            && File.Exists(recordingPath)
            && new FileInfo(recordingPath).Length <= MaximumRecordingBytes;

        await using (var output = new FileStream(
            fullOutputPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = new
            {
                bundleFormat = "psvr2-iracing-haptics-diagnostics",
                bundleVersion = 1,
                createdAt = DateTimeOffset.UtcNow,
                applicationVersion,
                operatingSystem = Environment.OSVersion.VersionString,
                framework = System.Runtime.InteropServices.RuntimeInformation
                    .FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation
                    .OSArchitecture.ToString(),
                dataMode = paths.IsPortable ? "portable" : "LocalAppData",
                includedLogFiles = logs.Length,
                includedRecording = includeRecording,
                privacy = "User profile paths and the current Windows user name are redacted."
            };
            await WriteTextEntryAsync(
                archive,
                "manifest.json",
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);

            var settingsJson = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true });
            await WriteTextEntryAsync(
                archive,
                "settings.redacted.json",
                Redact(settingsJson),
                cancellationToken).ConfigureAwait(false);

            foreach (var log in logs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await File.ReadAllTextAsync(
                    log.FullName,
                    cancellationToken).ConfigureAwait(false);
                await WriteTextEntryAsync(
                    archive,
                    $"logs/{log.Name}",
                    Redact(text),
                    cancellationToken).ConfigureAwait(false);
            }

            if (includeRecording)
            {
                var entry = archive.CreateEntry(
                    $"recording/{Path.GetFileName(recordingPath!)}",
                    CompressionLevel.Optimal);
                await using var destination = entry.Open();
                await using var source = new FileStream(
                    recordingPath!,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, cancellationToken)
                    .ConfigureAwait(false);
            }

            await WriteTextEntryAsync(
                archive,
                "README.txt",
                "This bundle contains diagnostic data only. It does not contain "
                + "PSVR2 Toolkit binaries, credentials, crash dumps or USB traffic.\n"
                + "Review the files before sharing them publicly.\n",
                cancellationToken).ConfigureAwait(false);
        }

        var size = new FileInfo(fullOutputPath).Length;
        return new DiagnosticBundleResult(
            fullOutputPath,
            logs.Length,
            includeRecording,
            size);
    }

    public static string Redact(string value)
    {
        var redacted = UserProfilePattern().Replace(value, "%USERPROFILE%");
        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            redacted = redacted.Replace(
                userName,
                "%USERNAME%",
                StringComparison.OrdinalIgnoreCase);
        }
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            redacted = redacted.Replace(
                profile,
                "%USERPROFILE%",
                StringComparison.OrdinalIgnoreCase);
        }
        return redacted;
    }

    private static async Task WriteTextEntryAsync(
        ZipArchive archive,
        string name,
        string text,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: false);
        await writer.WriteAsync(text.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    [GeneratedRegex(
        "(?i)[A-Z]:\\\\Users\\\\[^\\\\\"\\r\\n]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePattern();
}
