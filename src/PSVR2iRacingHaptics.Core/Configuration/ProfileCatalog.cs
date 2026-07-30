using System.Text.Json;

namespace PSVR2iRacingHaptics.Core.Configuration;

/// <summary>
/// Owns profile identity, factory presets, migration helpers and profile CRUD.
/// Runtime-only settings such as the selected device and safety limits are not
/// stored in a profile.
/// </summary>
public static class ProfileCatalog
{
    public const string DefaultProfileId = "factory-default";
    public const string GentleProfileId = "factory-gentle";
    public const string StrongProfileId = "factory-strong";
    public const string CustomProfileId = "factory-custom";

    public static IReadOnlyList<string> Names { get; } =
        ["Default", "Gentle", "Strong", "Custom"];

    public static AppSettings Create(string name)
    {
        var settings = new AppSettings
        {
            Profiles = CreateFactoryProfiles().ToList()
        };
        var normalized = NormalizeName(name);
        var profile = settings.Profiles.First(x =>
            x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        ApplyProfile(settings, profile.Id);
        return settings;
    }

    /// <summary>
    /// Adds the factory catalog to legacy settings while preserving the exact
    /// detector, event-switch and effect values that the user already had.
    /// </summary>
    public static void EnsureCatalog(AppSettings settings)
    {
        settings.Profiles ??= [];
        settings.ProfileRules ??= [];
        settings.Profiles = settings.Profiles
            .Where(profile => profile is not null)
            .ToList();
        settings.ProfileRules = settings.ProfileRules
            .Where(rule => rule is not null)
            .ToList();

        if (settings.Profiles.Count == 0)
        {
            var legacyName = NormalizeName(settings.ActiveProfile);
            settings.Profiles.AddRange(CreateFactoryProfiles());
            var active = settings.Profiles.First(x =>
                x.Name.Equals(legacyName, StringComparison.OrdinalIgnoreCase));
            active.Configuration = CaptureConfiguration(settings);
            settings.ActiveProfileId = active.Id;
            settings.ActiveProfile = active.Name;
        }

        EnsureFactoryProfile(settings, DefaultProfileId, "Default");
        EnsureFactoryProfile(settings, GentleProfileId, "Gentle");
        EnsureFactoryProfile(settings, StrongProfileId, "Strong");
        EnsureFactoryProfile(settings, CustomProfileId, "Custom");

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in settings.Profiles)
        {
            profile.Id = UniqueId(profile.Id, usedIds);
            profile.Name = UniqueName(
                NormalizeUserProfileName(profile.Name),
                usedNames);
            profile.Description ??= string.Empty;
            profile.Configuration ??= new HapticProfileConfiguration();
            profile.Configuration.Impacts ??= new ImpactSettings();
            profile.Configuration.Vertical ??= new VerticalImpactSettings();
            profile.Configuration.Incidents ??= new IncidentSettings();
            profile.Configuration.Effects ??= new EffectSettings();
        }

        var activeProfile = FindProfile(settings, settings.ActiveProfileId)
            ?? FindProfile(settings, settings.ActiveProfile)
            ?? settings.Profiles.First(x => x.Id == DefaultProfileId);
        settings.ActiveProfileId = activeProfile.Id;
        settings.ActiveProfile = activeProfile.Name;

        var validProfileIds = settings.Profiles
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        settings.ProfileRules = settings.ProfileRules
            .Where(rule => rule is not null && validProfileIds.Contains(rule.ProfileId))
            .Select(rule =>
            {
                rule.Id = UniqueId(rule.Id, usedRuleIds);
                rule.Name = string.IsNullOrWhiteSpace(rule.Name)
                    ? "Automatic profile rule"
                    : rule.Name.Trim();
                rule.Priority = Math.Clamp(rule.Priority, -1000, 1000);
                rule.CarPathPattern = CleanPattern(rule.CarPathPattern);
                rule.CarNamePattern = CleanPattern(rule.CarNamePattern);
                rule.CarClassPattern = CleanPattern(rule.CarClassPattern);
                rule.TrackNamePattern = CleanPattern(rule.TrackNamePattern);
                rule.TrackConfigPattern = CleanPattern(rule.TrackConfigPattern);
                return rule;
            })
            .ToList();
    }

    public static IReadOnlyList<HapticProfile> CreateFactoryProfiles() =>
    [
        FactoryProfile(
            DefaultProfileId,
            "Default",
            "Balanced detection and recognizable, conservative effects."),
        FactoryProfile(
            GentleProfileId,
            "Gentle",
            "Higher detection thresholds and milder effects."),
        FactoryProfile(
            StrongProfileId,
            "Strong",
            "Lower detection thresholds and more pronounced effects."),
        FactoryProfile(
            CustomProfileId,
            "Custom",
            "Editable starting point for a personal setup.")
    ];

    public static HapticProfile? FindProfile(AppSettings settings, string? idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        return settings.Profiles.FirstOrDefault(profile =>
                   profile.Id.Equals(idOrName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? settings.Profiles.FirstOrDefault(profile =>
                profile.Name.Equals(idOrName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static HapticProfile ActiveProfile(AppSettings settings) =>
        FindProfile(settings, settings.ActiveProfileId)
        ?? throw new InvalidOperationException("The active profile does not exist.");

    public static void ApplyProfile(AppSettings settings, string idOrName)
    {
        EnsureCatalog(settings);
        var profile = FindProfile(settings, idOrName)
            ?? throw new ArgumentException("The requested profile does not exist.", nameof(idOrName));
        ApplyConfiguration(settings, profile.Configuration);
        settings.ActiveProfileId = profile.Id;
        settings.ActiveProfile = profile.Name;
    }

    public static void ApplyActiveProfile(AppSettings settings) =>
        ApplyProfile(settings, settings.ActiveProfileId);

    public static void CaptureActiveProfile(AppSettings settings)
    {
        EnsureCatalog(settings);
        var profile = ActiveProfile(settings);
        profile.Configuration = CaptureConfiguration(settings);
        settings.ActiveProfile = profile.Name;
    }

    public static HapticProfile AddProfile(
        AppSettings settings,
        string name,
        bool copyCurrent = true)
    {
        EnsureCatalog(settings);
        var normalizedName = ValidateNewName(settings, name);
        var profile = new HapticProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            Description = "User-created profile.",
            IsBuiltIn = false,
            Configuration = copyCurrent
                ? CaptureConfiguration(settings)
                : FactoryConfiguration("Default")
        };
        settings.Profiles.Add(profile);
        return profile;
    }

    public static HapticProfile DuplicateProfile(
        AppSettings settings,
        string sourceId,
        string newName)
    {
        EnsureCatalog(settings);
        var source = FindProfile(settings, sourceId)
            ?? throw new ArgumentException("The source profile does not exist.", nameof(sourceId));
        var profile = AddProfile(settings, newName, copyCurrent: false);
        profile.Configuration = source.Configuration.DeepClone();
        profile.Description = $"Copy of {source.Name}.";
        return profile;
    }

    public static void RenameProfile(AppSettings settings, string profileId, string newName)
    {
        EnsureCatalog(settings);
        var profile = FindProfile(settings, profileId)
            ?? throw new ArgumentException("The profile does not exist.", nameof(profileId));
        if (profile.IsBuiltIn)
        {
            throw new InvalidOperationException(
                "Factory profile names cannot be changed. Duplicate it instead.");
        }

        var candidate = newName.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 60)
        {
            throw new ArgumentException(
                "Profile names must contain 1 to 60 characters.",
                nameof(newName));
        }
        if (settings.Profiles.Any(x =>
                !ReferenceEquals(x, profile)
                && x.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A profile with that name already exists.");
        }

        profile.Name = candidate;
        if (settings.ActiveProfileId == profile.Id)
        {
            settings.ActiveProfile = candidate;
        }
    }

    public static void DeleteProfile(AppSettings settings, string profileId)
    {
        EnsureCatalog(settings);
        var profile = FindProfile(settings, profileId)
            ?? throw new ArgumentException("The profile does not exist.", nameof(profileId));
        if (profile.IsBuiltIn)
        {
            throw new InvalidOperationException(
                "Factory profiles cannot be deleted. They can be reset or duplicated.");
        }

        settings.Profiles.Remove(profile);
        settings.ProfileRules.RemoveAll(rule =>
            rule.ProfileId.Equals(profile.Id, StringComparison.OrdinalIgnoreCase));
        if (settings.ActiveProfileId.Equals(profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            ApplyProfile(settings, DefaultProfileId);
        }
    }

    public static void ResetFactoryProfile(AppSettings settings, string profileId)
    {
        EnsureCatalog(settings);
        var profile = FindProfile(settings, profileId)
            ?? throw new ArgumentException("The profile does not exist.", nameof(profileId));
        if (!profile.IsBuiltIn)
        {
            throw new InvalidOperationException("Only factory profiles can be reset.");
        }

        profile.Configuration = FactoryConfiguration(profile.Name);
        if (settings.ActiveProfileId.Equals(profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            ApplyConfiguration(settings, profile.Configuration);
        }
    }

    public static HapticProfileConfiguration CaptureConfiguration(AppSettings settings) =>
        new()
        {
            Impacts = Clone(settings.Impacts),
            Vertical = Clone(settings.Vertical),
            Incidents = Clone(settings.Incidents),
            Effects = Clone(settings.Effects)
        };

    public static void ApplyConfiguration(
        AppSettings settings,
        HapticProfileConfiguration configuration)
    {
        settings.Impacts = Clone(configuration.Impacts);
        settings.Vertical = Clone(configuration.Vertical);
        settings.Incidents = Clone(configuration.Incidents);
        settings.Effects = Clone(configuration.Effects);
    }

    public static string NormalizeName(string? name) =>
        name?.Trim() switch
        {
            "Padrão" or "Padrao" or "Default" => "Default",
            "Suave" or "Gentle" => "Gentle",
            "Forte" or "Strong" => "Strong",
            "Personalizado" or "Custom" => "Custom",
            null or "" => "Default",
            _ => "Custom"
        };

    private static HapticProfile FactoryProfile(
        string id,
        string name,
        string description) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            IsBuiltIn = true,
            Configuration = FactoryConfiguration(name)
        };

    private static HapticProfileConfiguration FactoryConfiguration(string name)
    {
        var settings = new AppSettings();
        switch (NormalizeName(name))
        {
            case "Gentle":
                settings.Impacts.Sensitivity = 0.82;
                settings.Impacts.LightThreshold = 1.8;
                settings.Impacts.MediumThreshold = 3.4;
                settings.Impacts.StrongThreshold = 5.8;
                settings.Vertical.Sensitivity = 0.82;
                settings.Vertical.StrongKerbThreshold = 2.5;
                settings.Vertical.LandingThreshold = 2.75;
                settings.Effects.LightImpact.FrequencyHz = 10;
                settings.Effects.MediumImpact.FrequencyHz = 15;
                settings.Effects.StrongImpact.FrequencyHz = 21;
                settings.Effects.StrongKerb.FrequencyHz = 10;
                settings.Effects.Landing.FrequencyHz = 16;
                settings.Effects.LightImpact.DurationMs = 100;
                settings.Effects.MediumImpact.DurationMs = 140;
                settings.Effects.StrongImpact.DurationMs = 180;
                settings.Effects.StrongImpact.TailDurationMs = 90;
                settings.Effects.Rollover.DurationMs = 100;
                settings.Effects.StrongKerb.DurationMs = 95;
                settings.Effects.WheelDrop.DurationMs = 110;
                settings.Effects.Landing.DurationMs = 120;
                settings.Effects.Landing.TailDurationMs = 90;
                settings.Effects.SevereCompression.DurationMs = 130;
                settings.Effects.Incident1x.DurationMs = 90;
                settings.Effects.Incident2x.DurationMs = 100;
                settings.Effects.Incident4x.DurationMs = 130;
                settings.Effects.IncidentOffTrack.DurationMs = 90;
                settings.Effects.IncidentLossOfControl.DurationMs = 95;
                settings.Effects.IncidentContact.DurationMs = 130;
                settings.Effects.IncidentRollover.DurationMs = 105;
                settings.Effects.IncidentUnknown.DurationMs = 100;
                break;

            case "Strong":
                settings.Impacts.Sensitivity = 1.18;
                settings.Impacts.LightThreshold = 1.25;
                settings.Impacts.MediumThreshold = 2.45;
                settings.Impacts.StrongThreshold = 4.25;
                settings.Vertical.Sensitivity = 1.15;
                settings.Vertical.StrongKerbThreshold = 1.75;
                settings.Vertical.LandingThreshold = 1.95;
                settings.Effects.LightImpact.FrequencyHz = 14;
                settings.Effects.MediumImpact.FrequencyHz = 20;
                settings.Effects.StrongImpact.FrequencyHz = 25;
                settings.Effects.StrongKerb.FrequencyHz = 16;
                settings.Effects.Landing.FrequencyHz = 21;
                settings.Effects.LightImpact.DurationMs = 140;
                settings.Effects.MediumImpact.DurationMs = 190;
                settings.Effects.StrongImpact.DurationMs = 230;
                settings.Effects.StrongImpact.TailDurationMs = 120;
                settings.Effects.Rollover.DurationMs = 140;
                settings.Effects.StrongKerb.DurationMs = 130;
                settings.Effects.WheelDrop.DurationMs = 150;
                settings.Effects.Landing.DurationMs = 160;
                settings.Effects.Landing.TailDurationMs = 130;
                settings.Effects.SevereCompression.DurationMs = 180;
                settings.Effects.Incident1x.DurationMs = 120;
                settings.Effects.Incident2x.DurationMs = 135;
                settings.Effects.Incident4x.DurationMs = 170;
                settings.Effects.IncidentOffTrack.DurationMs = 120;
                settings.Effects.IncidentLossOfControl.DurationMs = 130;
                settings.Effects.IncidentContact.DurationMs = 175;
                settings.Effects.IncidentRollover.DurationMs = 145;
                settings.Effects.IncidentUnknown.DurationMs = 135;
                break;
        }

        return CaptureConfiguration(settings);
    }

    private static void EnsureFactoryProfile(
        AppSettings settings,
        string id,
        string name)
    {
        var existing = settings.Profiles.FirstOrDefault(x =>
            x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Id = id;
            existing.Name = name;
            existing.IsBuiltIn = true;
            return;
        }

        settings.Profiles.Add(CreateFactoryProfiles().First(x => x.Id == id));
    }

    private static string ValidateNewName(AppSettings settings, string name)
    {
        var candidate = name.Trim();
        if (candidate.Length is < 1 or > 60)
        {
            throw new ArgumentException(
                "Profile names must contain 1 to 60 characters.",
                nameof(name));
        }
        if (settings.Profiles.Any(x =>
                x.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A profile with that name already exists.");
        }
        return candidate;
    }

    private static string NormalizeUserProfileName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? "Recovered profile"
            : name.Trim()[..Math.Min(name.Trim().Length, 60)];

    private static string UniqueId(string? requested, ISet<string> used)
    {
        var id = string.IsNullOrWhiteSpace(requested)
            ? Guid.NewGuid().ToString("N")
            : requested.Trim();
        while (!used.Add(id))
        {
            id = Guid.NewGuid().ToString("N");
        }
        return id;
    }

    private static string UniqueName(string requested, ISet<string> used)
    {
        if (used.Add(requested))
        {
            return requested;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{requested} ({suffix})";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string CleanPattern(string? value) =>
        value?.Trim() ?? string.Empty;

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Could not clone {typeof(T).Name}.");
    }
}
