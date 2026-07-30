using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Configuration;

public static class HapticEventPolicy
{
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
            _ => false
        };
}
