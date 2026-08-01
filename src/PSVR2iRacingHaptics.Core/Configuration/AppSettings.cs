namespace PSVR2iRacingHaptics.Core.Configuration;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 6;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string ActiveProfile { get; set; } = "Default";
    public string ActiveProfileId { get; set; } = ProfileCatalog.DefaultProfileId;
    public bool AutoProfileSelectionEnabled { get; set; }
    public List<HapticProfile> Profiles { get; set; } = [];
    public List<ProfileAssignmentRule> ProfileRules { get; set; } = [];
    public bool HapticsEnabled { get; set; } = true;
    public bool UseSimulatedRumbleDevice { get; set; }
    public ImpactSettings Impacts { get; set; } = new();
    public VerticalImpactSettings Vertical { get; set; } = new();
    public IncidentSettings Incidents { get; set; } = new();
    public TelemetryTriggerSettings Triggers { get; set; } = new();
    public RecordingSettings Recording { get; set; } = new();
    public PhysicalCalibrationSettings PhysicalCalibration { get; set; } = new();
    public InputSettings Input { get; set; } = new();
    public ApplicationBehaviorSettings Application { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public EffectSettings Effects { get; set; } = new();

    public AppSettings DeepClone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}

public sealed class HapticProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New profile";
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public HapticProfileConfiguration Configuration { get; set; } = new();
}

public sealed class HapticProfileConfiguration
{
    public ImpactSettings Impacts { get; set; } = new();
    public VerticalImpactSettings Vertical { get; set; } = new();
    public IncidentSettings Incidents { get; set; } = new();
    public TelemetryTriggerSettings Triggers { get; set; } = new();
    public EffectSettings Effects { get; set; } = new();

    public HapticProfileConfiguration DeepClone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<HapticProfileConfiguration>(json)
            ?? new HapticProfileConfiguration();
    }
}

/// <summary>
/// An enabled rule matches when every non-empty pattern matches the detected
/// iRacing identity. Patterns accept '*' and '?' wildcards.
/// </summary>
public sealed class ProfileAssignmentRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Automatic profile rule";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public string ProfileId { get; set; } = ProfileCatalog.DefaultProfileId;
    public string CarPathPattern { get; set; } = string.Empty;
    public string CarNamePattern { get; set; } = string.Empty;
    public string CarClassPattern { get; set; } = string.Empty;
    public string TrackNamePattern { get; set; } = string.Empty;
    public string TrackConfigPattern { get; set; } = string.Empty;
}

public sealed class ImpactSettings
{
    public bool Enabled { get; set; } = true;
    public bool LightEnabled { get; set; } = true;
    public bool MediumEnabled { get; set; } = true;
    public bool StrongEnabled { get; set; } = true;
    public bool RolloverEnabled { get; set; } = true;
    public double Sensitivity { get; set; } = 1.0;
    public double LightThreshold { get; set; } = 1.45;
    public double MediumThreshold { get; set; } = 2.85;
    public double StrongThreshold { get; set; } = 5.0;
    public int CooldownMs { get; set; } = 260;
    public int RolloverCooldownMs { get; set; } = 110;
    public double MinimumSpeedMps { get; set; } = 2.5;
    public double HysteresisRatio { get; set; } = 0.62;
    public int WarmupMs { get; set; } = 900;
}

public sealed class VerticalImpactSettings
{
    public bool StrongKerbsEnabled { get; set; } = true;
    public bool LightKerbsEnabled { get; set; }
    public bool LandingsEnabled { get; set; } = true;
    public bool WheelDropsEnabled { get; set; } = true;
    public bool SevereCompressionEnabled { get; set; } = true;
    public double Sensitivity { get; set; } = 1.0;
    public double StrongKerbThreshold { get; set; } = 2.05;
    public double LandingThreshold { get; set; } = 2.25;
    public double SevereCompressionThreshold { get; set; } = 3.25;
    public double MinimumSpeedMps { get; set; } = 4.0;
    public int CooldownMs { get; set; } = 360;
    public double HysteresisRatio { get; set; } = 0.58;
    public int WarmupMs { get; set; } = 900;
}

public sealed class IncidentSettings
{
    public bool Enabled { get; set; }
    public IncidentPatternBasis PatternBasis { get; set; } =
        IncidentPatternBasis.PointValue;
    public bool OnePointEnabled { get; set; } = true;
    public bool TwoPointEnabled { get; set; } = true;
    public bool FourPointEnabled { get; set; } = true;
    public bool OtherPointValuesEnabled { get; set; } = true;
    public bool OffTrackEnabled { get; set; } = true;
    public bool LossOfControlEnabled { get; set; } = true;
    public bool ContactEnabled { get; set; } = true;
    public bool RolloverEnabled { get; set; } = true;
    public bool UnknownEnabled { get; set; } = true;
    public bool SuppressWhenPhysicalImpactDetected { get; set; } = true;
    public int CooldownMs { get; set; } = 650;
    public int EvidenceWindowMs { get; set; } = 1400;
}

public enum IncidentPatternBasis
{
    PointValue = 0,
    InferredType = 1
}

public sealed class SafetySettings
{
    public int MaximumContinuousRumbleMs { get; set; } = 250;
    public int MaximumEffectDurationMs { get; set; } = 550;
    public int MaximumCallsPerSecond { get; set; } = 20;
    public int NativeCallTimeoutMs { get; set; } = 1200;
}

public sealed class RecordingSettings
{
    public bool CircularBufferEnabled { get; set; } = true;
    public int CircularBufferSeconds { get; set; } = 60;
}

public sealed class PhysicalCalibrationSettings
{
    public bool Completed { get; set; }
    public bool UsableRangeFound { get; set; }
    public byte MinimumClearlyPerceptibleFrequencyHz { get; set; } = 10;
    public byte PreferredFrequencyHz { get; set; } = 16;
    public byte MaximumComfortableFrequencyHz { get; set; } = 22;
    public int MinimumClearlyPerceptibleDurationMs { get; set; } = 90;
    public int PreferredDurationMs { get; set; } = 140;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ApplicationBehaviorSettings
{
    public bool MinimizeToNotificationArea { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool CheckForUpdatesOnStartup { get; set; } = true;
}

public sealed class EffectPatternSettings
{
    public byte FrequencyHz { get; set; }
    public int DurationMs { get; set; }
    public int PulseCount { get; set; } = 1;
    public int GapMs { get; set; }
    public byte TailFrequencyHz { get; set; }
    public int TailDurationMs { get; set; }
}

public sealed class EffectSettings
{
    public EffectPatternSettings LightImpact { get; set; } =
        new() { FrequencyHz = 12, DurationMs = 120 };

    public EffectPatternSettings MediumImpact { get; set; } =
        new() { FrequencyHz = 18, DurationMs = 160 };

    public EffectPatternSettings StrongImpact { get; set; } =
        new()
        {
            FrequencyHz = 24,
            DurationMs = 200,
            PulseCount = 1,
            GapMs = 55,
            TailFrequencyHz = 21,
            TailDurationMs = 100
        };

    public EffectPatternSettings Rollover { get; set; } =
        new() { FrequencyHz = 22, DurationMs = 120, PulseCount = 2, GapMs = 65 };

    public EffectPatternSettings StrongKerb { get; set; } =
        new() { FrequencyHz = 14, DurationMs = 110 };

    public EffectPatternSettings WheelDrop { get; set; } =
        new() { FrequencyHz = 16, DurationMs = 130 };

    public EffectPatternSettings Landing { get; set; } =
        new()
        {
            FrequencyHz = 19,
            DurationMs = 140,
            GapMs = 60,
            TailFrequencyHz = 15,
            TailDurationMs = 110
        };

    public EffectPatternSettings SevereCompression { get; set; } =
        new() { FrequencyHz = 20, DurationMs = 150 };

    public EffectPatternSettings Incident1x { get; set; } =
        new() { FrequencyHz = 12, DurationMs = 105 };

    public EffectPatternSettings Incident2x { get; set; } =
        new() { FrequencyHz = 16, DurationMs = 115, PulseCount = 2, GapMs = 65 };

    public EffectPatternSettings Incident4x { get; set; } =
        new()
        {
            FrequencyHz = 20,
            DurationMs = 150,
            GapMs = 55,
            TailFrequencyHz = 16,
            TailDurationMs = 90
        };

    public EffectPatternSettings IncidentOther { get; set; } =
        new() { FrequencyHz = 14, DurationMs = 120 };

    public EffectPatternSettings IncidentOffTrack { get; set; } =
        new() { FrequencyHz = 11, DurationMs = 105 };

    public EffectPatternSettings IncidentLossOfControl { get; set; } =
        new() { FrequencyHz = 15, DurationMs = 110, PulseCount = 2, GapMs = 70 };

    public EffectPatternSettings IncidentContact { get; set; } =
        new() { FrequencyHz = 20, DurationMs = 155 };

    public EffectPatternSettings IncidentRollover { get; set; } =
        new() { FrequencyHz = 22, DurationMs = 125, PulseCount = 2, GapMs = 65 };

    public EffectPatternSettings IncidentUnknown { get; set; } =
        new() { FrequencyHz = 13, DurationMs = 120 };
}
