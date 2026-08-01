using PSVR2iRacingHaptics.App;
using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Services;

namespace PSVR2iRacingHaptics.App.SmokeTests;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main()
    {
        ApplicationConfiguration.Initialize();
        var directory = Path.Combine(
            Path.GetTempPath(),
            "PSVR2iRacingHaptics.App.SmokeTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            await MainWindowCanBeCreatedAsync(directory);
            StartupFailureCreatesDiagnostic(directory);
            Console.WriteLine("Result: 2/2 Windows startup smoke checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("[FAILED] Windows startup smoke test");
            Console.WriteLine(exception);
            return 1;
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static async Task MainWindowCanBeCreatedAsync(string directory)
    {
        var paths = new AppPaths(
            Path.Combine(directory, "data"),
            Path.Combine(directory, "data", "settings.json"),
            Path.Combine(directory, "data", "logs"),
            Path.Combine(directory, "data", "recordings"),
            IsPortable: true);
        paths.EnsureDirectories();
        using var logger = new RotatingFileLogger(paths.LogsDirectory);
        var settingsService = new SettingsService(paths.SettingsFile, logger);
        var settings = await settingsService.LoadAsync();
        await using var coordinator = new AppCoordinator(
            paths,
            settings,
            settingsService,
            logger);
        using var form = new MainForm(coordinator);
        var handle = form.Handle;
        form.PerformLayout();
        form.Size = form.MinimumSize;
        form.PerformLayout();
        if (handle == IntPtr.Zero || !form.IsHandleCreated)
        {
            throw new InvalidOperationException(
                "The main window did not create a native Windows handle.");
        }
        if (form.Controls.Count == 0)
        {
            throw new InvalidOperationException(
                "The main window was created without its interface.");
        }
        Console.WriteLine("[OK] Main window can be created without Toolkit or iRacing.");
    }

    private static void StartupFailureCreatesDiagnostic(string directory)
    {
        var applicationDirectory = Path.Combine(directory, "portable-app");
        Directory.CreateDirectory(applicationDirectory);
        File.WriteAllText(
            Path.Combine(applicationDirectory, "portable.mode"),
            "Portable mode for startup smoke test.");
        var path = StartupFailureReporter.Write(
            new InvalidOperationException("Synthetic startup smoke-test failure."),
            applicationDirectory: applicationDirectory);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The startup diagnostic file was not created.");
        }
        var report = File.ReadAllText(path);
        if (!report.Contains(
                "Synthetic startup smoke-test failure.",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The startup diagnostic did not contain the exception.");
        }
        Console.WriteLine("[OK] Early startup failures create a diagnostic log.");
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Test cleanup must not mask the validation result.
        }
    }
}
