using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Configuration;

public static class HapticEventPolicy
{
    public static bool IsEnabled(DetectedHapticEvent detected, AppSettings settings)
    {
        if (!IsEnabled(detected.Kind, settings))
        {
            return false;
        }

        if (!IsIncident(detected.Kind))
        {
            return true;
        }

        if (settings.Incidents.SuppressWhenPhysicalImpactDetected
            && detected.HasRelatedPhysicalEvent)
        {
            return false;
        }

        return detected.IncidentType switch
        {
            IncidentType.OffTrack => settings.Incidents.OffTrackEnabled,
            IncidentType.LossOfControl => settings.Incidents.LossOfControlEnabled,
            IncidentType.Contact => settings.Incidents.ContactEnabled,
            IncidentType.Rollover => settings.Incidents.RolloverEnabled,
            _ => settings.Incidents.UnknownEnabled
        };
    }

    public static bool IsEnabled(HapticEventKind kind, AppSettings settings) =>
        kind switch
        {
            HapticEventKind.LightImpact =>
                settings.Impacts.Enabled && settings.Impacts.LightEnabled,
            HapticEventKind.MediumImpact =>
                settings.Impacts.Enabled && settings.Impacts.MediumEnabled,
            HapticEventKind.StrongImpact =>
                settings.Impacts.Enabled && settings.Impacts.StrongEnabled,
            HapticEventKind.RolloverImpact =>
                settings.Impacts.Enabled && settings.Impacts.RolloverEnabled,
            HapticEventKind.StrongKerb =>
                settings.Vertical.StrongKerbsEnabled
                || settings.Vertical.LightKerbsEnabled,
            HapticEventKind.WheelDrop => settings.Vertical.WheelDropsEnabled,
            HapticEventKind.Landing => settings.Vertical.LandingsEnabled,
            HapticEventKind.SevereVerticalCompression =>
                settings.Vertical.SevereCompressionEnabled,
            HapticEventKind.Incident1x =>
                settings.Incidents.Enabled && settings.Incidents.OnePointEnabled,
            HapticEventKind.Incident2x =>
                settings.Incidents.Enabled && settings.Incidents.TwoPointEnabled,
            HapticEventKind.Incident4x =>
                settings.Incidents.Enabled && settings.Incidents.FourPointEnabled,
            HapticEventKind.IncidentOther =>
                settings.Incidents.Enabled && settings.Incidents.OtherPointValuesEnabled,
            _ => false
        };

    private static bool IsIncident(HapticEventKind kind) =>
        kind is HapticEventKind.Incident1x
            or HapticEventKind.Incident2x
            or HapticEventKind.Incident4x
            or HapticEventKind.IncidentOther;
}
