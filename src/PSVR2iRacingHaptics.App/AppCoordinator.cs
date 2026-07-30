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

namespace PSVR2iRacingHaptics.App;

public sealed class AppCoordinator : IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly SettingsService _settingsService;
    private readonly IAppLogger _logger;
    private readonly object _settingsLock = new();
    private readonly object _stateLock = new();
    private readonly HapticDetectionPipeline _pipeline = new();
    private readonly RumbleEffectMapper _effectMapper = new();
    private readonly TelemetryRecorder _recorder;
    private readonly IRacingSharedMemoryClient _iracing;
    private readonly TelemetrySimulator _simulator;
    private readonly SimulatedRumbleDevice _simulatedRumble;
    private Psvr2ToolkitClient _toolkit;
    private ITelemetryClient? _activeTelemetry;
    private TelemetryReplayClient? _replay;
    private RumbleController _rumbleController;
    private CancellationTokenSource? _lifetime;
    private Task _toolkitPolling = Task.CompletedTask;
    private AppSettings _settings;
    private AppRuntimeState _state = new();
    private DateTimeOffset _lastDiagnosticsPublish = DateTimeOffset.MinValue;
    private bool _disposed;

    public AppCoordinator(
        AppPaths paths,
        AppSettings settings,
        SettingsService settingsService,
        IAppLogger logger)
    {
        _paths = paths;
        _settings = settings;
        _settingsService = settingsService;
        _logger = logger;
        _recorder = new TelemetryRecorder(logger);
        _iracing = new IRacingSharedMemoryClient(logger);
        _simulator = new TelemetrySimulator(logger);
        _simulatedRumble = new SimulatedRumbleDevice(logger);
        _toolkit = new Psvr2ToolkitClient(settings.Safety, logger);
        _rumbleController = CreateRumbleController(settings);
        HookRumbleController(_rumbleController);
        HookToolkit(_toolkit);
        _logger.LineWritten += OnLogLine;
    }

    public event EventHandler<AppRuntimeState>? StateChanged;
    public event EventHandler<string>? LogLine;
    public event EventHandler<DetectedHapticEvent>? EventDetected;

    public AppSettings Settings
    {
        get
        {
            lock (_settingsLock)
            {
                return _settings.DeepClone();
            }
        }
    }

    public AppRuntimeState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public string RecordingsDirectory => _paths.RecordingsDirectory;
    public string DataDirectory => _paths.DataDirectory;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_lifetime is not null)
        {
            return;
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.Info(
            $"PSVR2 iRacing Haptics v{Application.ProductVersion} started; "
            + $"data mode={(_paths.IsPortable ? "portable" : "LocalAppData")}.");

        await _toolkit.InitializeAsync(_lifetime.Token).ConfigureAwait(false);
        await ActivateTelemetryAsync(_iracing, simulated: false, _lifetime.Token)
            .ConfigureAwait(false);
        await _rumbleController.SetEnabledAsync(Settings.HapticsEnabled, _lifetime.Token)
            .ConfigureAwait(false);
        _toolkitPolling = PollToolkitAsync(_lifetime.Token);
        PublishState();
    }

    public async Task ApplySettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        lock (_settingsLock)
        {
            _settings = settings.DeepClone();
        }

        await _settingsService.SaveAsync(Settings, cancellationToken).ConfigureAwait(false);
        await RecreateRumbleControllerAsync(cancellationToken).ConfigureAwait(false);
        _pipeline.Reset();
        PublishState();
    }

    public async Task ApplyProfileAsync(
        string profile,
        CancellationToken cancellationToken = default)
    {
        var current = Settings;
        var settings = ProfileCatalog.Create(profile);
        settings.UseSimulatedRumbleDevice = current.UseSimulatedRumbleDevice;
        settings.HapticsEnabled = current.HapticsEnabled;
        settings.Impacts.Enabled = current.Impacts.Enabled;
        settings.Impacts.LightEnabled = current.Impacts.LightEnabled;
        settings.Impacts.MediumEnabled = current.Impacts.MediumEnabled;
        settings.Impacts.StrongEnabled = current.Impacts.StrongEnabled;
        settings.Impacts.RolloverEnabled = current.Impacts.RolloverEnabled;
        settings.Vertical.StrongKerbsEnabled = current.Vertical.StrongKerbsEnabled;
        settings.Vertical.LightKerbsEnabled = current.Vertical.LightKerbsEnabled;
        settings.Vertical.LandingsEnabled = current.Vertical.LandingsEnabled;
        settings.Vertical.WheelDropsEnabled = current.Vertical.WheelDropsEnabled;
        settings.Vertical.SevereCompressionEnabled =
            current.Vertical.SevereCompressionEnabled;
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSimulatedRumbleAsync(
        bool simulated,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        settings.UseSimulatedRumbleDevice = simulated;
        settings.ActiveProfile = "Custom";
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> PlayManualTestAsync(
        byte frequencyHz,
        int durationMs,
        int pulseCount,
        int gapMs,
        CancellationToken cancellationToken = default)
    {
        var effect = _effectMapper.CreateManual(
            frequencyHz,
            durationMs,
            pulseCount,
            gapMs);
        return _rumbleController.TryPlayAsync(effect, cancellationToken);
    }

    public Task EmergencyStopAsync(
        string reason = "emergency stop button",
        CancellationToken cancellationToken = default) =>
        _rumbleController.EmergencyStopAsync(reason, cancellationToken);

    public async Task UseTelemetrySimulatorAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (enabled)
        {
            await ActivateTelemetryAsync(_simulator, simulated: true, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await ActivateTelemetryAsync(_iracing, simulated: false, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task PlayScenarioAsync(
        TelemetryScenario scenario,
        CancellationToken cancellationToken = default) =>
        _simulator.PlayScenarioAsync(scenario, cancellationToken);

    public async Task StartRecordingAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        path ??= Path.Combine(
            _paths.RecordingsDirectory,
            $"telemetry-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        await _recorder.StartAsync(path, cancellationToken).ConfigureAwait(false);
        UpdateState(state => state with { Recording = true });
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _recorder.StopAsync(cancellationToken).ConfigureAwait(false);
        UpdateState(state => state with { Recording = false });
    }

    public Task MarkAsync(string marker, CancellationToken cancellationToken = default) =>
        _recorder.MarkAsync(marker, cancellationToken);

    public Task<CalibrationReport> AnalyzeRecordingAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        CalibrationAnalyzer.AnalyzeAsync(path, Settings, cancellationToken);

    public async Task StartReplayAsync(
        string path,
        double speedMultiplier,
        CancellationToken cancellationToken = default)
    {
        if (_replay is not null)
        {
            await _replay.DisposeAsync().ConfigureAwait(false);
        }
        _replay = new TelemetryReplayClient(path, speedMultiplier, _logger);
        await ActivateTelemetryAsync(_replay, simulated: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StopReplayAsync(CancellationToken cancellationToken = default)
    {
        if (_replay is not null)
        {
            await _replay.StopAsync(cancellationToken).ConfigureAwait(false);
            await _replay.DisposeAsync().ConfigureAwait(false);
            _replay = null;
        }
        await ActivateTelemetryAsync(_iracing, simulated: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ActivateTelemetryAsync(
        ITelemetryClient client,
        bool simulated,
        CancellationToken cancellationToken)
    {
        if (ReferenceEquals(_activeTelemetry, client))
        {
            return;
        }

        if (_activeTelemetry is not null)
        {
            UnhookTelemetry(_activeTelemetry);
            await _activeTelemetry.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        _pipeline.Reset();
        await _rumbleController.EmergencyStopAsync(
            "telemetry source changed",
            cancellationToken).ConfigureAwait(false);
        _activeTelemetry = client;
        HookTelemetry(client);
        await client.StartAsync(cancellationToken).ConfigureAwait(false);
        UpdateState(state => state with
        {
            SimulatedTelemetry = simulated,
            TelemetryStatus = client.StatusDescription,
            IRacingConnected = client.IsConnected,
            DriverInCar = false
        });
    }

    private void OnTelemetryFrame(object? sender, TelemetryFrame frame)
    {
        PipelineResult result;
        try
        {
            result = _pipeline.Process(frame, Settings);
        }
        catch (Exception ex)
        {
            _logger.Error("Detection pipeline failed.", ex);
            _ = _rumbleController.EmergencyStopAsync("detector exception");
            return;
        }

        if (_recorder.IsRecording)
        {
            _ = _recorder.RecordFrameAsync(frame, result.SelectedEvent);
        }

        if (!frame.IsConnected || !frame.IsDriverInCar)
        {
            _ = _rumbleController.EmergencyStopAsync(
                !frame.IsConnected ? "iRacing connection lost" : "driver out of car");
        }

        if (result.SelectedEvent is not null)
        {
            var detected = result.SelectedEvent;
            _logger.Info(
                $"Event={detected.Kind}; severity={detected.Severity}; "
                + $"score={detected.Score:F2}; {detected.Reason}");
            EventDetected?.Invoke(this, detected);
            var settings = Settings;
            var eventEnabled = HapticEventPolicy.IsEnabled(detected.Kind, settings);
            if (settings.HapticsEnabled && eventEnabled)
            {
                var effect = _effectMapper.Map(detected, settings.Effects);
                _ = _rumbleController.TryPlayAsync(effect);
            }
            else
            {
                _logger.Info(
                    $"Haptic output suppressed for {detected.Kind}: "
                    + (settings.HapticsEnabled
                        ? "this event category is disabled."
                        : "all haptics are disabled."));
            }
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastDiagnosticsPublish).TotalMilliseconds >= 45
            || result.SelectedEvent is not null)
        {
            _lastDiagnosticsPublish = now;
            UpdateState(state => state with
            {
                IRacingConnected = frame.IsConnected,
                DriverInCar = frame.IsDriverInCar,
                TelemetryStatus = _activeTelemetry?.StatusDescription ?? "Disconnected",
                Diagnostics = result.Diagnostics,
                LastEvent = result.SelectedEvent is null
                    ? state.LastEvent
                    : FormatLastEvent(result.SelectedEvent, Settings)
            });
        }
    }

    private void OnTelemetryConnectionChanged(object? sender, bool connected)
    {
        if (!connected)
        {
            _ = _rumbleController.EmergencyStopAsync("telemetry source disconnected");
        }
        UpdateState(state => state with
        {
            IRacingConnected = connected,
            DriverInCar = connected && state.DriverInCar,
            TelemetryStatus = _activeTelemetry?.StatusDescription ?? "Disconnected"
        });
    }

    private async Task PollToolkitAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var status = await _toolkit.RefreshStatusAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!status.DriverActive && !Settings.UseSimulatedRumbleDevice)
                {
                    await _rumbleController.EmergencyStopAsync(
                        "Toolkit driver inactive",
                        cancellationToken).ConfigureAwait(false);
                }
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RecreateRumbleControllerAsync(CancellationToken cancellationToken)
    {
        var previous = _rumbleController;
        await previous.EmergencyStopAsync("settings changed", cancellationToken)
            .ConfigureAwait(false);
        await previous.DisposeAsync().ConfigureAwait(false);
        _rumbleController = CreateRumbleController(Settings);
        HookRumbleController(_rumbleController);
        await _rumbleController.SetEnabledAsync(Settings.HapticsEnabled, cancellationToken)
            .ConfigureAwait(false);
    }

    private RumbleController CreateRumbleController(AppSettings settings)
    {
        IHmdRumbleDevice device = settings.UseSimulatedRumbleDevice
            ? _simulatedRumble
            : _toolkit;
        return new RumbleController(
            device,
            settings.Safety,
            _logger,
            disposeDevice: false);
    }

    private void HookTelemetry(ITelemetryClient telemetry)
    {
        telemetry.FrameReceived += OnTelemetryFrame;
        telemetry.ConnectionChanged += OnTelemetryConnectionChanged;
    }

    private void UnhookTelemetry(ITelemetryClient telemetry)
    {
        telemetry.FrameReceived -= OnTelemetryFrame;
        telemetry.ConnectionChanged -= OnTelemetryConnectionChanged;
    }

    private void HookToolkit(Psvr2ToolkitClient toolkit)
    {
        toolkit.StatusChanged += (_, status) =>
        {
            UpdateState(state => state with
            {
                Toolkit = status,
                RumbleDeviceStatus = Settings.UseSimulatedRumbleDevice
                    ? _simulatedRumble.StatusDescription
                    : toolkit.StatusDescription
            });
        };
    }

    private void HookRumbleController(RumbleController controller)
    {
        controller.StatusChanged += (_, status) =>
        {
            UpdateState(state => state with
            {
                HapticsEnabled = status.Enabled,
                SimulatedRumble = Settings.UseSimulatedRumbleDevice,
                RumbleDeviceStatus = Settings.UseSimulatedRumbleDevice
                    ? _simulatedRumble.StatusDescription
                    : _toolkit.StatusDescription,
                Rumble = status
            });
        };
    }

    private void OnLogLine(object? sender, string line) => LogLine?.Invoke(this, line);

    private void UpdateState(Func<AppRuntimeState, AppRuntimeState> update)
    {
        AppRuntimeState state;
        lock (_stateLock)
        {
            _state = update(_state);
            state = _state;
        }
        StateChanged?.Invoke(this, state);
    }

    private void PublishState() => UpdateState(state => state with
    {
        Toolkit = _toolkit.Status,
        HapticsEnabled = Settings.HapticsEnabled,
        SimulatedRumble = Settings.UseSimulatedRumbleDevice,
        RumbleDeviceStatus = Settings.UseSimulatedRumbleDevice
            ? _simulatedRumble.StatusDescription
            : _toolkit.StatusDescription,
        TelemetryStatus = _activeTelemetry?.StatusDescription ?? state.TelemetryStatus
    });

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        var lifetime = Interlocked.Exchange(ref _lifetime, null);
        lifetime?.Cancel();
        await _rumbleController.EmergencyStopAsync("application shutdown")
            .ConfigureAwait(false);

        if (_activeTelemetry is not null)
        {
            UnhookTelemetry(_activeTelemetry);
            await _activeTelemetry.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        await _recorder.DisposeAsync().ConfigureAwait(false);
        await _rumbleController.DisposeAsync().ConfigureAwait(false);
        await _iracing.DisposeAsync().ConfigureAwait(false);
        await _simulator.DisposeAsync().ConfigureAwait(false);
        if (_replay is not null)
        {
            await _replay.DisposeAsync().ConfigureAwait(false);
        }
        await _simulatedRumble.DisposeAsync().ConfigureAwait(false);
        await _toolkit.DisposeAsync().ConfigureAwait(false);

        try
        {
            await _toolkitPolling.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
        }
        lifetime?.Dispose();
        _logger.LineWritten -= OnLogLine;
        _logger.Info("Application closed after requesting Rumble OFF.");
    }

    private static string FormatLastEvent(
        DetectedHapticEvent detected,
        AppSettings settings)
    {
        var outputEnabled = settings.HapticsEnabled
            && HapticEventPolicy.IsEnabled(detected.Kind, settings);
        return $"{detected.Kind} ({detected.Score:F2})"
            + (outputEnabled ? "" : " — haptic output disabled");
    }
}
