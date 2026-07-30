using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Detection;
using PSVR2iRacingHaptics.Core.Devices;
using PSVR2iRacingHaptics.Core.Effects;
using PSVR2iRacingHaptics.Core.Models;
using PSVR2iRacingHaptics.Core.Services;
using PSVR2iRacingHaptics.Core.Telemetry;
using PSVR2iRacingHaptics.Infrastructure.IRacing;
using PSVR2iRacingHaptics.Infrastructure.Psvr2;

namespace PSVR2iRacingHaptics.Tests;

internal static class Program
{
    private static readonly List<(string Name, Func<Task> Test)> Tests =
    [
        ("Configuração padrão sem arquivo", SettingsDefaults),
        ("Persistência e validação de configuração", SettingsRoundTrip),
        ("Filtro e cálculo de jerk", SignalProcessorCalculatesJerk),
        ("Aceleração normal não vira batida", NormalAccelerationIsIgnored),
        ("Frenagem forte não vira batida", HardBrakingIsIgnored),
        ("Zebra leve é ignorada por padrão", LightKerbIsIgnored),
        ("Zebra forte é detectada", StrongKerbIsDetected),
        ("Batida lateral é classificada", SideImpactIsDetected),
        ("Batida frontal é classificada", FrontImpactIsDetected),
        ("Colisão forte tem severidade forte", StrongCollisionIsDetected),
        ("Capotamento permite sequência de impactos", RolloverIsDetected),
        ("Pouso é detectado após perda de apoio", LandingIsDetected),
        ("Queda de roda usa assimetria da suspensão", WheelDropIsDetected),
        ("Telemetria inválida reseta aquecimento", InvalidTelemetryResetsPipeline),
        ("Mapeamento diferencia batida e pouso", EffectMappingDiffers),
        ("Efeito forte não é interrompido por zebra", StrongEffectRejectsKerb),
        ("Efeito forte substitui zebra", StrongEffectPreemptsKerb),
        ("Controlador envia zero após efeito", ControllerAlwaysSendsZero),
        ("Cancelamento envia zero", CancellationSendsZero),
        ("Parada de emergência envia zero", EmergencyStopSendsZero),
        ("Dispositivo indisponível rejeita efeito", UnavailableDeviceRejectsEffect),
        ("Gravação JSONL preserva frames e marcações", RecorderWritesReplayableJsonl),
        ("Comparação relaciona marcação e detecção", CalibrationMatchesDetection),
        ("Ausência da DLL não causa crash", MissingToolkitDoesNotCrash),
        ("Ausência do iRacing não causa crash", MissingIRacingDoesNotCrash)
    ];

    private static async Task<int> Main()
    {
        var failures = new List<string>();
        Console.WriteLine($"Executando {Tests.Count} testes...");
        foreach (var (name, test) in Tests)
        {
            try
            {
                await test();
                Console.WriteLine($"[OK] {name}");
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.Message}");
                Console.WriteLine($"[FALHOU] {name}");
                Console.WriteLine($"         {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            failures.Count == 0
                ? $"Resultado: {Tests.Count}/{Tests.Count} testes aprovados."
                : $"Resultado: {Tests.Count - failures.Count}/{Tests.Count} aprovados.");
        foreach (var failure in failures)
        {
            Console.WriteLine(" - " + failure);
        }
        return failures.Count == 0 ? 0 : 1;
    }

    private static async Task SettingsDefaults()
    {
        var directory = TempDirectory();
        try
        {
            var service = new SettingsService(Path.Combine(directory, "settings.json"));
            var settings = await service.LoadAsync();
            Equal("Padrão", settings.ActiveProfile);
            True(settings.Impacts.Enabled);
            False(settings.Vertical.LightKerbsEnabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task SettingsRoundTrip()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(path);
            var settings = new AppSettings
            {
                ActiveProfile = "Personalizado",
                UseSimulatedRumbleDevice = true
            };
            settings.Impacts.LightThreshold = 2.2;
            settings.Effects.StrongImpact.FrequencyHz = 99;
            await service.SaveAsync(settings);
            var loaded = await service.LoadAsync();
            Equal("Personalizado", loaded.ActiveProfile);
            True(loaded.UseSimulatedRumbleDevice);
            Near(2.2, loaded.Impacts.LightThreshold, 0.001);
            Equal((byte)25, loaded.Effects.StrongImpact.FrequencyHz);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task SignalProcessorCalculatesJerk()
    {
        var processor = new TelemetrySignalProcessor();
        var start = DateTimeOffset.UtcNow;
        processor.Process(ValidFrame(start, 1));
        var processed = processor.Process(ValidFrame(start.AddMilliseconds(16.667), 2) with
        {
            LatAccelMps2 = 30
        });
        True(processed.LatJerk > 100, $"Jerk inesperado: {processed.LatJerk:F2}");
        True(processed.HorizontalImpulseG > 0.5);
        return Task.CompletedTask;
    }

    private static Task NormalAccelerationIsIgnored()
    {
        var events = Detect(TelemetryScenario.NormalAcceleration);
        False(events.Any(IsCollision), "Aceleração normal gerou colisão.");
        return Task.CompletedTask;
    }

    private static Task HardBrakingIsIgnored()
    {
        var events = Detect(TelemetryScenario.HardBraking);
        False(events.Any(IsCollision), "Frenagem/bloqueio gerou colisão. " + Describe(events));
        return Task.CompletedTask;
    }

    private static Task LightKerbIsIgnored()
    {
        var events = Detect(TelemetryScenario.LightKerb);
        False(events.Any(x => x.Kind == HapticEventKind.StrongKerb));
        return Task.CompletedTask;
    }

    private static Task StrongKerbIsDetected()
    {
        var events = Detect(TelemetryScenario.StrongKerb);
        True(events.Any(x => x.Kind == HapticEventKind.StrongKerb), Describe(events));
        return Task.CompletedTask;
    }

    private static Task SideImpactIsDetected()
    {
        var events = Detect(TelemetryScenario.SideImpact);
        True(events.Any(x => IsCollision(x) && x.Direction == ImpactDirection.Lateral),
            Describe(events));
        return Task.CompletedTask;
    }

    private static Task FrontImpactIsDetected()
    {
        var events = Detect(TelemetryScenario.FrontImpact);
        True(events.Any(x => IsCollision(x) && x.Direction == ImpactDirection.Front),
            Describe(events));
        return Task.CompletedTask;
    }

    private static Task StrongCollisionIsDetected()
    {
        var events = Detect(TelemetryScenario.StrongCollision);
        True(events.Any(x => x.Kind == HapticEventKind.StrongImpact), Describe(events));
        return Task.CompletedTask;
    }

    private static Task RolloverIsDetected()
    {
        var events = Detect(TelemetryScenario.Rollover);
        True(events.Count(x => x.Kind == HapticEventKind.RolloverImpact) >= 1, Describe(events));
        return Task.CompletedTask;
    }

    private static Task LandingIsDetected()
    {
        var events = Detect(TelemetryScenario.Landing);
        True(events.Any(x => x.Kind == HapticEventKind.Landing), Describe(events));
        return Task.CompletedTask;
    }

    private static Task WheelDropIsDetected()
    {
        var events = Detect(TelemetryScenario.WheelDrop);
        True(events.Any(x => x.Kind == HapticEventKind.WheelDrop), Describe(events));
        return Task.CompletedTask;
    }

    private static Task InvalidTelemetryResetsPipeline()
    {
        var pipeline = new HapticDetectionPipeline();
        var settings = new AppSettings();
        var start = DateTimeOffset.UtcNow;
        for (var index = 0; index < 70; index++)
        {
            pipeline.Process(ValidFrame(start.AddSeconds(index / 60.0), index), settings);
        }
        pipeline.Process(TelemetryFrame.Disconnected(start.AddSeconds(2)), settings);
        var result = pipeline.Process(ValidFrame(start.AddSeconds(2.1), 100) with
        {
            LatAccelMps2 = 100
        }, settings);
        True(result.SelectedEvent is null, "Evento foi aceito sem novo aquecimento.");
        False(result.Diagnostics.IsWarm);
        return Task.CompletedTask;
    }

    private static Task EffectMappingDiffers()
    {
        var mapper = new RumbleEffectMapper();
        var settings = new AppSettings();
        var diag = new ProcessedTelemetry();
        var impact = mapper.Map(new DetectedHapticEvent(
            DateTimeOffset.UtcNow,
            HapticEventKind.StrongImpact,
            EventSeverity.Strong,
            8,
            100,
            ImpactDirection.Front,
            "teste",
            diag), settings.Effects);
        var landing = mapper.Map(new DetectedHapticEvent(
            DateTimeOffset.UtcNow,
            HapticEventKind.Landing,
            EventSeverity.Medium,
            3,
            70,
            ImpactDirection.NotApplicable,
            "teste",
            diag), settings.Effects);
        True(impact.Pulses[0].FrequencyHz != landing.Pulses[0].FrequencyHz);
        True(landing.Pulses.Count == 2);
        return Task.CompletedTask;
    }

    private static async Task StrongEffectRejectsKerb()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        var strong = new RumbleEffect("forte", 100, [new RumblePulse(24, 80)]);
        var kerb = new RumbleEffect("zebra", 40, [new RumblePulse(13, 20)]);
        True(await controller.TryPlayAsync(strong));
        await Task.Delay(10);
        False(await controller.TryPlayAsync(kerb));
        await Task.Delay(120);
    }

    private static async Task StrongEffectPreemptsKerb()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        var kerb = new RumbleEffect("zebra", 40, [new RumblePulse(13, 100)]);
        var strong = new RumbleEffect("forte", 100, [new RumblePulse(24, 30)]);
        True(await controller.TryPlayAsync(kerb));
        await Task.Delay(10);
        True(await controller.TryPlayAsync(strong));
        await Task.Delay(140);
        True(device.Commands.Any(x => x.FrequencyHz == 24));
    }

    private static async Task ControllerAlwaysSendsZero()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        True(await controller.TryPlayAsync(
            new RumbleEffect("teste", 50, [new RumblePulse(18, 20)])));
        await Task.Delay(80);
        var commands = device.Commands.ToArray();
        True(commands.Any(x => x.FrequencyHz == 18));
        Equal((byte)0, commands[^1].FrequencyHz);
    }

    private static async Task CancellationSendsZero()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        using var cancellation = new CancellationTokenSource();
        True(await controller.TryPlayAsync(
            new RumbleEffect("cancelar", 50, [new RumblePulse(20, 200)]),
            cancellation.Token));
        await Task.Delay(15);
        cancellation.Cancel();
        await Task.Delay(70);
        Equal((byte)0, device.Commands.ToArray()[^1].FrequencyHz);
    }

    private static async Task EmergencyStopSendsZero()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        True(await controller.TryPlayAsync(
            new RumbleEffect("parar", 50, [new RumblePulse(21, 200)])));
        await Task.Delay(15);
        await controller.EmergencyStopAsync();
        Equal((byte)0, device.Commands.ToArray()[^1].FrequencyHz);
    }

    private static async Task UnavailableDeviceRejectsEffect()
    {
        var device = new SimulatedRumbleDevice();
        device.SetAvailable(false);
        await using var controller = Controller(device);
        False(await controller.TryPlayAsync(
            new RumbleEffect("indisponível", 50, [new RumblePulse(20, 20)])));
    }

    private static async Task RecorderWritesReplayableJsonl()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "recording.jsonl");
            await using var recorder = new TelemetryRecorder();
            await recorder.StartAsync(path);
            var frame = ValidFrame(DateTimeOffset.UtcNow, 1);
            await recorder.RecordFrameAsync(frame);
            await recorder.MarkAsync("Isto foi uma batida");
            await recorder.StopAsync();
            var entries = new List<TelemetryLogEntry>();
            await foreach (var entry in TelemetryReplayClient.ReadEntriesAsync(path))
            {
                entries.Add(entry);
            }
            Equal(2, entries.Count);
            Equal("frame", entries[0].EntryType);
            Equal("marker", entries[1].EntryType);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task CalibrationMatchesDetection()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "calibration.jsonl");
            await using var recorder = new TelemetryRecorder();
            await recorder.StartAsync(path);
            var frames = TelemetryScenarioFactory.Create(TelemetryScenario.StrongCollision);
            foreach (var frame in frames)
            {
                await recorder.RecordFrameAsync(frame);
                if (Math.Abs(frame.LatAccelMps2) > 50)
                {
                    await recorder.MarkAsync("Isto foi uma batida");
                }
            }
            await recorder.StopAsync();
            var report = await CalibrationAnalyzer.AnalyzeAsync(path, new AppSettings());
            Equal(1, report.MarkerCount);
            True(report.MatchedCount == 1, $"Relatório: {report}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task MissingToolkitDoesNotCrash()
    {
        await using var client = new Psvr2ToolkitClient(new SafetySettings());
        var status = await client.InitializeAsync();
        False(status.ApiInitialized);
        False(client.IsAvailable);
    }

    private static async Task MissingIRacingDoesNotCrash()
    {
        await using var client = new IRacingSharedMemoryClient();
        await client.StartAsync(CancellationToken.None);
        await Task.Delay(20);
        await client.StopAsync(CancellationToken.None);
        False(client.IsConnected);
    }

    private static List<DetectedHapticEvent> Detect(TelemetryScenario scenario)
    {
        var pipeline = new HapticDetectionPipeline();
        var settings = new AppSettings();
        var events = new List<DetectedHapticEvent>();
        foreach (var frame in TelemetryScenarioFactory.Create(scenario))
        {
            var result = pipeline.Process(frame, settings);
            if (result.SelectedEvent is not null)
            {
                events.Add(result.SelectedEvent);
            }
        }
        return events;
    }

    private static RumbleController Controller(SimulatedRumbleDevice device) =>
        new(
            device,
            new SafetySettings
            {
                MaximumContinuousRumbleMs = 250,
                MaximumEffectDurationMs = 500,
                MaximumCallsPerSecond = 40
            });

    private static TelemetryFrame ValidFrame(DateTimeOffset timestamp, long sequence) =>
        new()
        {
            Timestamp = timestamp,
            Sequence = sequence,
            IsConnected = true,
            IsValid = true,
            IsOnTrack = true,
            IsOnTrackCar = true,
            SessionState = 4,
            SpeedMps = 30,
            VertAccelMps2 = (float)TelemetrySignalProcessor.GravityMps2,
            IncidentCount = 0
        };

    private static bool IsCollision(DetectedHapticEvent detected) =>
        detected.Kind is HapticEventKind.LightImpact
            or HapticEventKind.MediumImpact
            or HapticEventKind.StrongImpact
            or HapticEventKind.RolloverImpact;

    private static string Describe(IEnumerable<DetectedHapticEvent> events) =>
        "Eventos: " + string.Join(
            ", ",
            events.Select(x => $"{x.Kind}/{x.Direction}/{x.Score:F2}"));

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "psvr2-haptics-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void True(bool condition, string message = "Esperado verdadeiro.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message = "Esperado falso.") =>
        True(!condition, message);

    private static void Equal<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Esperado {expected}; recebido {actual}.");
        }
    }

    private static void Near(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"Esperado {expected} ± {tolerance}; recebido {actual}.");
        }
    }
}
