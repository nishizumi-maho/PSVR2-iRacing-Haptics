namespace PSVR2iRacingHaptics.Core.Configuration;

public static class ProfileCatalog
{
    public static IReadOnlyList<string> Names { get; } =
        new[] { "Padrão", "Suave", "Forte", "Personalizado" };

    public static AppSettings Create(string name)
    {
        var settings = new AppSettings { ActiveProfile = name };

        switch (name)
        {
            case "Suave":
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
                break;

            case "Forte":
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
                break;
        }

        return settings;
    }
}
