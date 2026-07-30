namespace PSVR2iRacingHaptics.Core.Configuration;

public static class ProfileCatalog
{
    public static IReadOnlyList<string> Names { get; } =
        new[] { "Default", "Gentle", "Strong", "Custom" };

    public static AppSettings Create(string name)
    {
        name = NormalizeName(name);
        var settings = new AppSettings { ActiveProfile = name };

        switch (name)
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
                break;
        }

        return settings;
    }

    public static string NormalizeName(string? name) =>
        name?.Trim() switch
        {
            "Padrão" or "Padrao" or "Default" => "Default",
            "Suave" or "Gentle" => "Gentle",
            "Forte" or "Strong" => "Strong",
            "Personalizado" or "Custom" => "Custom",
            null or "" => "Custom",
            _ => "Custom"
        };
}
