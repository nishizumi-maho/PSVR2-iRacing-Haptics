namespace PSVR2iRacingHaptics.Core.Configuration;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string ActiveProfile { get; set; } = "Padrão";
    public bool HapticsEnabled { get; set; } = true;
    public bool UseSimulatedRumbleDevice { get; set; }
    public ImpactSettings Impacts { get; set; } = new();
    public VerticalImpactSettings Vertical { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public EffectSettings Effects { get; set; } = new();

    public AppSettings DeepClone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}

public sealed class ImpactSettings
{
    public bool Enabled { get; set; } = true;
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

public sealed class SafetySettings
{
    public int MaximumContinuousRumbleMs { get; set; } = 300;
    public int MaximumEffectDurationMs { get; set; } = 700;
    public int MaximumCallsPerSecond { get; set; } = 20;
    public int NativeCallTimeoutMs { get; set; } = 1200;
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
        new() { FrequencyHz = 12, DurationMs = 75 };

    public EffectPatternSettings MediumImpact { get; set; } =
        new() { FrequencyHz = 18, DurationMs = 125 };

    public EffectPatternSettings StrongImpact { get; set; } =
        new()
        {
            FrequencyHz = 24,
            DurationMs = 145,
            PulseCount = 1,
            GapMs = 40,
            TailFrequencyHz = 21,
            TailDurationMs = 80
        };

    public EffectPatternSettings Rollover { get; set; } =
        new() { FrequencyHz = 22, DurationMs = 90, PulseCount = 2, GapMs = 45 };

    public EffectPatternSettings StrongKerb { get; set; } =
        new() { FrequencyHz = 13, DurationMs = 60 };

    public EffectPatternSettings WheelDrop { get; set; } =
        new() { FrequencyHz = 15, DurationMs = 80 };

    public EffectPatternSettings Landing { get; set; } =
        new()
        {
            FrequencyHz = 18,
            DurationMs = 60,
            GapMs = 30,
            TailFrequencyHz = 14,
            TailDurationMs = 50
        };

    public EffectPatternSettings SevereCompression { get; set; } =
        new() { FrequencyHz = 20, DurationMs = 105 };
}
