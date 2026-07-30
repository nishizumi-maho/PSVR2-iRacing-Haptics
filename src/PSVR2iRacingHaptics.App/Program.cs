using PSVR2iRacingHaptics.Core.Services;

namespace PSVR2iRacingHaptics.App;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var paths = AppPaths.Resolve(args);
        paths.EnsureDirectories();
        using var logger = new RotatingFileLogger(paths.LogsDirectory);
        var settingsService = new SettingsService(paths.SettingsFile, logger);
        var settings = await settingsService.LoadAsync();

        if (args.Any(x => string.Equals(x, "--simulator", StringComparison.OrdinalIgnoreCase)))
        {
            settings.UseSimulatedRumbleDevice = true;
        }

        await using var coordinator = new AppCoordinator(
            paths,
            settings,
            settingsService,
            logger);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += async (_, eventArgs) =>
        {
            logger.Error("Exceção não tratada na interface.", eventArgs.Exception);
            await coordinator.EmergencyStopAsync("exceção não tratada na interface");
            MessageBox.Show(
                "Ocorreu um erro inesperado. A vibração foi desligada e o erro foi registrado.",
                "PSVR2 iRacing Haptics",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        using var form = new MainForm(coordinator);
        Application.Run(form);
    }
}
