using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Win32;

namespace PSVR2iRacingHaptics.App;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string Message);

public static class ApplicationIntegrationService
{
    private const string RunValueName = "PSVR2iRacingHaptics";
    private const string LatestReleaseApi =
        "https://api.github.com/repos/nishizumi-maho/"
        + "PSVR2-iRacing-Haptics/releases/latest";
    private static readonly HttpClient Http = CreateHttpClient();

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true)
            ?? throw new InvalidOperationException(
                "Could not open the current user's Windows startup registry key.");
        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "Could not determine the application executable path.");
            key.SetValue(
                RunValueName,
                $"\"{executable}\"",
                RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(
            LatestReleaseApi,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString() ?? string.Empty
            : string.Empty;
        var url = root.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString() ?? string.Empty
            : string.Empty;
        if (!Version.TryParse(NormalizeVersion(currentVersion), out var current))
        {
            current = new Version(0, 0, 0);
        }
        if (!Version.TryParse(NormalizeVersion(tag), out var latest))
        {
            throw new InvalidDataException(
                "GitHub returned a release tag that is not a semantic version.");
        }
        var available = latest > current;
        return new UpdateCheckResult(
            available,
            current.ToString(),
            latest.ToString(),
            url,
            available
                ? $"Version {latest} is available."
                : $"Version {current} is current.");
    }

    private static string NormalizeVersion(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }
        var separator = normalized.IndexOfAny(['-', '+']);
        return separator >= 0 ? normalized[..separator] : normalized;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PSVR2-iRacing-Haptics", "1.1"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
