using System.Text.Json;
using System.Text.Json.Serialization;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Core.Services;

public sealed record ProfilePackage
{
    public const string ExpectedFormat = "psvr2-iracing-haptics-profile";
    public const int CurrentFormatVersion = 1;

    public string Format { get; init; } = ExpectedFormat;
    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
    public string ExportedByVersion { get; init; } = "unknown";
    public HapticProfile Profile { get; init; } = new();
}

public sealed record ProfileImportPreview(
    string Name,
    string Description,
    int CustomTriggerCount,
    int TriggerConditionCount,
    bool IncidentHapticsEnabled,
    string SourcePath,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Versioned, data-only profile exchange. Imported packages never preserve a
/// factory identity and cannot change global device, safety or application
/// settings.
/// </summary>
public static class ProfilePackageService
{
    private const long MaximumPackageBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static async Task ExportAsync(
        HapticProfile profile,
        string path,
        string applicationVersion,
        CancellationToken cancellationToken = default)
    {
        var clone = Clone(profile);
        clone.IsBuiltIn = false;
        var package = new ProfilePackage
        {
            ExportedByVersion = string.IsNullOrWhiteSpace(applicationVersion)
                ? "unknown"
                : applicationVersion.Trim(),
            Profile = clone
        };
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Invalid profile package path."));
        await using var stream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            32 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(
            stream,
            package,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ProfileImportPreview> PreviewAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var package = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        var warnings = new List<string>();
        if (package.Profile.Configuration.Triggers.CustomTriggers.Any(trigger =>
                trigger.SourceMode == TriggerSourceMode.ReplaceBuiltIn))
        {
            warnings.Add(
                "At least one trigger completely replaces a built-in event detector.");
        }
        if (package.Profile.Configuration.Triggers.CustomTriggers.Any(trigger =>
                trigger.UseCustomEffect))
        {
            warnings.Add(
                "The package contains trigger-specific rumble patterns.");
        }
        if (package.ExportedByVersion == "unknown")
        {
            warnings.Add("The exporting application version was not recorded.");
        }

        return new ProfileImportPreview(
            package.Profile.Name,
            package.Profile.Description,
            package.Profile.Configuration.Triggers.CustomTriggers.Count,
            package.Profile.Configuration.Triggers.CustomTriggers.Sum(trigger =>
                trigger.Conditions.Count),
            package.Profile.Configuration.Incidents.Enabled,
            Path.GetFullPath(path),
            warnings);
    }

    public static async Task<HapticProfile> ImportAsync(
        AppSettings settings,
        string path,
        CancellationToken cancellationToken = default)
    {
        var package = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        var imported = Clone(package.Profile);
        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        imported.Name = UniqueName(settings, CleanName(imported.Name));
        imported.Description = string.IsNullOrWhiteSpace(imported.Description)
            ? "Imported profile."
            : imported.Description.Trim();
        settings.Profiles.Add(imported);
        return imported;
    }

    private static async Task<ProfilePackage> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The profile package does not exist.", fullPath);
        }
        if (file.Length is <= 0 or > MaximumPackageBytes)
        {
            throw new InvalidDataException(
                "The profile package is empty or exceeds the 5 MB safety limit.");
        }

        await using var stream = file.OpenRead();
        ProfilePackage? package;
        try
        {
            package = await JsonSerializer.DeserializeAsync<ProfilePackage>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The selected file is not a valid profile package.",
                exception);
        }

        if (package is null
            || !package.Format.Equals(
                ProfilePackage.ExpectedFormat,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The selected JSON is not a PSVR2 iRacing Haptics profile package.");
        }
        if (package.FormatVersion != ProfilePackage.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Profile package version {package.FormatVersion} is not supported.");
        }

        ValidateProfile(package.Profile);
        return package;
    }

    private static void ValidateProfile(HapticProfile profile)
    {
        if (profile.Configuration is null)
        {
            throw new InvalidDataException("The profile configuration is missing.");
        }
        profile.Configuration.Impacts ??= new ImpactSettings();
        profile.Configuration.Vertical ??= new VerticalImpactSettings();
        profile.Configuration.Incidents ??= new IncidentSettings();
        profile.Configuration.Triggers ??= new TelemetryTriggerSettings();
        profile.Configuration.Effects ??= new EffectSettings();
        profile.Configuration.Triggers.CustomTriggers ??= [];
        if (profile.Configuration.Triggers.CustomTriggers.Count > 128)
        {
            throw new InvalidDataException(
                "The package contains more than 128 custom triggers.");
        }
        if (profile.Configuration.Triggers.CustomTriggers.Any(trigger =>
                trigger is null
                || trigger.Conditions is null
                || trigger.Conditions.Count is 0 or > 32))
        {
            throw new InvalidDataException(
                "Every custom trigger must contain 1 to 32 valid conditions.");
        }
    }

    private static string CleanName(string? name)
    {
        var cleaned = string.IsNullOrWhiteSpace(name)
            ? "Imported profile"
            : name.Trim();
        return cleaned[..Math.Min(cleaned.Length, 60)];
    }

    private static string UniqueName(AppSettings settings, string requested)
    {
        if (!settings.Profiles.Any(profile =>
                profile.Name.Equals(requested, StringComparison.OrdinalIgnoreCase)))
        {
            return requested;
        }

        for (var suffix = 2; ; suffix++)
        {
            var suffixText = $" ({suffix})";
            var baseLength = Math.Max(1, 60 - suffixText.Length);
            var candidate = requested[..Math.Min(requested.Length, baseLength)]
                + suffixText;
            if (!settings.Profiles.Any(profile =>
                    profile.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private static HapticProfile Clone(HapticProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        return JsonSerializer.Deserialize<HapticProfile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not clone the profile.");
    }
}
