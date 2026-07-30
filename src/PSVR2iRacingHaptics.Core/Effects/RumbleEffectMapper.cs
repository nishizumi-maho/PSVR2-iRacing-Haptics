using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Effects;

public sealed class RumbleEffectMapper
{
    public RumbleEffect Map(DetectedHapticEvent detected, EffectSettings settings)
    {
        var pattern = detected.Kind switch
        {
            HapticEventKind.RolloverImpact => settings.Rollover,
            HapticEventKind.StrongImpact => settings.StrongImpact,
            HapticEventKind.MediumImpact => settings.MediumImpact,
            HapticEventKind.LightImpact => settings.LightImpact,
            HapticEventKind.StrongKerb => settings.StrongKerb,
            HapticEventKind.WheelDrop => settings.WheelDrop,
            HapticEventKind.Landing => settings.Landing,
            HapticEventKind.SevereVerticalCompression => settings.SevereCompression,
            _ => settings.LightImpact
        };

        return FromPattern(EventName(detected), detected.Priority, pattern);
    }

    public RumbleEffect CreateManual(
        byte frequencyHz,
        int durationMs,
        int pulseCount,
        int gapMs) =>
        FromPattern(
            "Manual test",
            110,
            new EffectPatternSettings
            {
                FrequencyHz = frequencyHz,
                DurationMs = durationMs,
                PulseCount = pulseCount,
                GapMs = gapMs
            });

    public static RumbleEffect FromPattern(
        string name,
        int priority,
        EffectPatternSettings pattern)
    {
        var pulses = new List<RumblePulse>();
        var count = Math.Clamp(pattern.PulseCount, 1, 8);
        var frequency = (byte)Math.Clamp(pattern.FrequencyHz, (byte)0, (byte)25);

        for (var index = 0; index < count; index++)
        {
            var hasAnother = index < count - 1 || pattern.TailDurationMs > 0;
            pulses.Add(new RumblePulse(
                frequency,
                Math.Max(10, pattern.DurationMs),
                hasAnother ? Math.Max(0, pattern.GapMs) : 0));
        }

        if (pattern.TailDurationMs > 0 && pattern.TailFrequencyHz > 0)
        {
            pulses.Add(new RumblePulse(
                (byte)Math.Clamp(pattern.TailFrequencyHz, (byte)1, (byte)25),
                pattern.TailDurationMs));
        }

        return new RumbleEffect(name, priority, pulses);
    }

    private static string EventName(DetectedHapticEvent detected) => detected.Kind switch
    {
        HapticEventKind.LightImpact => "Light impact",
        HapticEventKind.MediumImpact => "Medium impact",
        HapticEventKind.StrongImpact => "Strong impact",
        HapticEventKind.RolloverImpact => "Rollover impact",
        HapticEventKind.StrongKerb => "Strong kerb",
        HapticEventKind.WheelDrop => "Wheel drop",
        HapticEventKind.Landing => "Car landing",
        HapticEventKind.SevereVerticalCompression => "Severe vertical compression",
        _ => detected.Kind.ToString()
    };
}
