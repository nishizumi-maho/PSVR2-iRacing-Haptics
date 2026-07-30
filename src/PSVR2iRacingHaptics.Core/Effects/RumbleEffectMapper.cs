using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Effects;

public sealed class RumbleEffectMapper
{
    public RumbleEffect Map(
        DetectedHapticEvent detected,
        EffectSettings settings,
        IncidentSettings? incidentSettings = null,
        TelemetryTriggerSettings? triggerSettings = null)
    {
        var customTrigger = detected.TriggerId is null
            ? null
            : triggerSettings?.CustomTriggers.FirstOrDefault(trigger =>
                trigger.Enabled
                && trigger.UseCustomEffect
                && trigger.Id.Equals(
                    detected.TriggerId,
                    StringComparison.OrdinalIgnoreCase));
        var pattern = customTrigger?.CustomEffect
            ?? (IsIncident(detected.Kind)
                && incidentSettings?.PatternBasis == IncidentPatternBasis.InferredType
                    ? PatternForIncidentType(detected.IncidentType, settings)
                    : PatternForKind(detected.Kind, settings));

        return FromPattern(EventName(detected), detected.Priority, pattern);
    }

    private static EffectPatternSettings PatternForKind(
        HapticEventKind kind,
        EffectSettings settings) =>
        kind switch
        {
            HapticEventKind.RolloverImpact => settings.Rollover,
            HapticEventKind.StrongImpact => settings.StrongImpact,
            HapticEventKind.MediumImpact => settings.MediumImpact,
            HapticEventKind.SideImpact
                or HapticEventKind.FrontImpact
                or HapticEventKind.RearImpact => settings.MediumImpact,
            HapticEventKind.LightImpact => settings.LightImpact,
            HapticEventKind.StrongKerb => settings.StrongKerb,
            HapticEventKind.WheelDrop => settings.WheelDrop,
            HapticEventKind.Landing => settings.Landing,
            HapticEventKind.SevereVerticalCompression => settings.SevereCompression,
            HapticEventKind.Incident1x => settings.Incident1x,
            HapticEventKind.Incident2x => settings.Incident2x,
            HapticEventKind.Incident4x => settings.Incident4x,
            HapticEventKind.IncidentOther => settings.IncidentOther,
            _ => settings.LightImpact
        };

    private static EffectPatternSettings PatternForIncidentType(
        IncidentType type,
        EffectSettings settings) =>
        type switch
        {
            IncidentType.OffTrack => settings.IncidentOffTrack,
            IncidentType.LossOfControl => settings.IncidentLossOfControl,
            IncidentType.Contact => settings.IncidentContact,
            IncidentType.Rollover => settings.IncidentRollover,
            _ => settings.IncidentUnknown
        };

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
        HapticEventKind.SideImpact => "Side impact",
        HapticEventKind.FrontImpact => "Front impact",
        HapticEventKind.RearImpact => "Rear impact",
        HapticEventKind.RolloverImpact => "Rollover impact",
        HapticEventKind.StrongKerb => "Strong kerb",
        HapticEventKind.WheelDrop => "Wheel drop",
        HapticEventKind.Landing => "Car landing",
        HapticEventKind.SevereVerticalCompression => "Severe vertical compression",
        HapticEventKind.Incident1x => $"1x incident ({IncidentName(detected)})",
        HapticEventKind.Incident2x => $"2x incident ({IncidentName(detected)})",
        HapticEventKind.Incident4x => $"4x incident ({IncidentName(detected)})",
        HapticEventKind.IncidentOther =>
            $"{detected.IncidentPoints}x incident ({IncidentName(detected)})",
        _ => detected.Kind.ToString()
    };

    private static string IncidentName(DetectedHapticEvent detected) =>
        detected.IncidentType switch
        {
            IncidentType.OffTrack => "off track",
            IncidentType.LossOfControl => "loss of control",
            IncidentType.Contact => "contact",
            IncidentType.Rollover => "rollover",
            _ => "unclassified"
        };

    private static bool IsIncident(HapticEventKind kind) =>
        kind is HapticEventKind.Incident1x
            or HapticEventKind.Incident2x
            or HapticEventKind.Incident4x
            or HapticEventKind.IncidentOther;
}
