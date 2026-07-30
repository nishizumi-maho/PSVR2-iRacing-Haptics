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
    private readonly object _pipelineLock = new();
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
    private string _lastProfileContextFingerprint = string.Empty;
    private bool _disposed;

    public AppCoordinator(
        AppPaths paths,
        AppSettings settings,
        SettingsService settingsService,
        IAppLogger logger)
    {
        _paths = paths;
        _settings = settings;
        _state = new AppRuntimeState
        {
            ActiveProfileId = settings.ActiveProfileId,
            ActiveProfileName = settings.ActiveProfile,
            AutoProfileSelectionEnabled = settings.AutoProfileSelectionEnabled
        };
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
        var saved = await _settingsService.SaveAsync(settings, cancellationToken)
            .ConfigureAwait(false);
        lock (_settingsLock)
        {
            _settings = saved;
        }

        await RecreateRumbleControllerAsync(cancellationToken).ConfigureAwait(false);
        lock (_pipelineLock)
        {
            _pipeline.Reset();
        }
        _lastProfileContextFingerprint = string.Empty;
        PublishState();
    }

    public async Task ApplyProfileAsync(
        string profileIdOrName,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        ProfileCatalog.ApplyProfile(settings, profileIdOrName);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Profile activated manually: {settings.ActiveProfile}.");
    }

    public async Task SetSimulatedRumbleAsync(
        bool simulated,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        settings.UseSimulatedRumbleDevice = simulated;
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateProfileAsync(
        string name,
        bool copyCurrent = true,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var profile = ProfileCatalog.AddProfile(settings, name, copyCurrent);
        ProfileCatalog.ApplyProfile(settings, profile.Id);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Profile created: {profile.Name}.");
    }

    public async Task DuplicateProfileAsync(
        string sourceProfileId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var profile = ProfileCatalog.DuplicateProfile(
            settings,
            sourceProfileId,
            newName);
        ProfileCatalog.ApplyProfile(settings, profile.Id);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Profile duplicated: {profile.Name}.");
    }

    public async Task RenameProfileAsync(
        string profileId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        ProfileCatalog.RenameProfile(settings, profileId, newName);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Profile renamed to {newName.Trim()}.");
    }

    public async Task DeleteProfileAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var profileName = ProfileCatalog.FindProfile(settings, profileId)?.Name
            ?? profileId;
        ProfileCatalog.DeleteProfile(settings, profileId);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Profile deleted: {profileName}.");
    }

    public async Task ResetFactoryProfileAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var profileName = ProfileCatalog.FindProfile(settings, profileId)?.Name
            ?? profileId;
        ProfileCatalog.ResetFactoryProfile(settings, profileId);
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Factory profile reset: {profileName}.");
    }

    public async Task SetAutomaticProfileSelectionAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        settings.AutoProfileSelectionEnabled = enabled;
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _lastProfileContextFingerprint = string.Empty;
        EvaluateAutomaticProfile(State.TelemetryContext);
    }

    public async Task UpsertProfileRuleAsync(
        ProfileAssignmentRule rule,
        CancellationToken cancellationToken = default)
    {
        if (!ProfileRuleMatcher.HasAtLeastOneFilter(rule))
        {
            throw new ArgumentException(
                "At least one car or track pattern is required.",
                nameof(rule));
        }

        var settings = Settings;
        if (ProfileCatalog.FindProfile(settings, rule.ProfileId) is null)
        {
            throw new ArgumentException("The selected profile does not exist.", nameof(rule));
        }

        var existing = settings.ProfileRules.FirstOrDefault(candidate =>
            candidate.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            settings.ProfileRules.Add(rule);
        }
        else
        {
            var index = settings.ProfileRules.IndexOf(existing);
            settings.ProfileRules[index] = rule;
        }

        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _lastProfileContextFingerprint = string.Empty;
        EvaluateAutomaticProfile(State.TelemetryContext);
    }

    public async Task DeleteProfileRuleAsync(
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        settings.ProfileRules.RemoveAll(rule =>
            rule.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _lastProfileContextFingerprint = string.Empty;
        EvaluateAutomaticProfile(State.TelemetryContext);
    }

    public async Task ApplyCalibrationRecommendationsAsync(
        CalibrationReport report,
        CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var applied = 0;
        foreach (var recommendation in report.Recommendations.Where(x => x.CanApply))
        {
            switch (recommendation.SettingPath)
            {
                case "Impacts.LightThreshold":
                    settings.Impacts.LightThreshold = recommendation.SuggestedValue;
                    break;
                case "Impacts.MediumThreshold":
                    settings.Impacts.MediumThreshold = recommendation.SuggestedValue;
                    break;
                case "Impacts.StrongThreshold":
                    settings.Impacts.StrongThreshold = recommendation.SuggestedValue;
                    break;
                case "Vertical.StrongKerbThreshold":
                    settings.Vertical.StrongKerbThreshold = recommendation.SuggestedValue;
                    break;
                case "Vertical.LandingThreshold":
                    settings.Vertical.LandingThreshold = recommendation.SuggestedValue;
                    break;
                case "Vertical.SevereCompressionThreshold":
                    settings.Vertical.SevereCompressionThreshold =
                        recommendation.SuggestedValue;
                    break;
                default:
                    continue;
            }
            applied++;
        }

        if (applied == 0)
        {
            throw new InvalidOperationException(
                "This report does not contain any safe automatic recommendations.");
        }
        await ApplySettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        _logger.Info(
            $"Applied {applied} calibration recommendation(s) to profile "
            + $"{settings.ActiveProfile}.");
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

        lock (_pipelineLock)
        {
            _pipeline.Reset();
        }
        _lastProfileContextFingerprint = string.Empty;
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
        EvaluateAutomaticProfile(frame.Context);
        var settings = CurrentSettingsReference();
        PipelineResult result;
        try
        {
            lock (_pipelineLock)
            {
                result = _pipeline.Process(frame, settings);
            }
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

        foreach (var detected in result.Candidates)
        {
            _logger.Info(
                $"Event={detected.Kind}; severity={detected.Severity}; "
                + $"score={detected.Score:F2}; {detected.Reason}");
        }

        if (result.SelectedEvent is not null)
        {
            EventDetected?.Invoke(this, result.SelectedEvent);
        }

        if (result.Candidates.Count > 0)
        {
            var enabledCandidates = result.Candidates
                .Where(detected => HapticEventPolicy.IsEnabled(detected, settings))
                .ToArray();
            if (settings.HapticsEnabled && enabledCandidates.Length > 0)
            {
                PlayCandidateEffects(enabledCandidates, settings);
            }
            else
            {
                foreach (var detected in result.Candidates)
                {
                    _logger.Info(
                        $"Haptic output suppressed for {detected.Kind}: "
                        + (settings.HapticsEnabled
                            ? SuppressionReason(detected, settings)
                            : "all haptics are disabled."));
                }
            }
        }

        var incident = result.Candidates.FirstOrDefault(IsIncident);
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
                TelemetryContext = frame.Context,
                ActiveProfileId = settings.ActiveProfileId,
                ActiveProfileName = settings.ActiveProfile,
                Diagnostics = result.Diagnostics,
                LastEvent = result.SelectedEvent is null
                    ? state.LastEvent
                    : FormatLastEvent(result.SelectedEvent, settings),
                LastIncident = incident is null
                    ? state.LastIncident
                    : FormatIncident(incident)
            });
        }
    }

    private void PlayCandidateEffects(
        IReadOnlyList<DetectedHapticEvent> enabledCandidates,
        AppSettings settings)
    {
        var physical = enabledCandidates.FirstOrDefault(candidate => !IsIncident(candidate));
        var incident = enabledCandidates.FirstOrDefault(IsIncident);
        if (physical is not null)
        {
            var physicalEffect = _effectMapper.Map(
                physical,
                settings.Effects,
                settings.Incidents);
            _ = _rumbleController.TryPlayAsync(physicalEffect);
            if (incident is not null)
            {
                _ = PlayIncidentAfterPhysicalAsync(
                    incident,
                    physicalEffect.TotalDurationMs + 45);
            }
            return;
        }

        if (incident is not null)
        {
            _ = _rumbleController.TryPlayAsync(
                _effectMapper.Map(
                    incident,
                    settings.Effects,
                    settings.Incidents));
        }
    }

    private async Task PlayIncidentAfterPhysicalAsync(
        DetectedHapticEvent incident,
        int delayMilliseconds)
    {
        try
        {
            var cancellationToken = _lifetime?.Token ?? CancellationToken.None;
            await Task.Delay(
                Math.Clamp(delayMilliseconds, 50, 1000),
                cancellationToken).ConfigureAwait(false);
            var settings = CurrentSettingsReference();
            if (!State.DriverInCar
                || !settings.HapticsEnabled
                || !HapticEventPolicy.IsEnabled(incident, settings))
            {
                return;
            }
            await _rumbleController.TryPlayAsync(
                _effectMapper.Map(
                    incident,
                    settings.Effects,
                    settings.Incidents),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void EvaluateAutomaticProfile(TelemetryContext context)
    {
        var fingerprint = context.Fingerprint;
        if (fingerprint == _lastProfileContextFingerprint)
        {
            return;
        }
        _lastProfileContextFingerprint = fingerprint;

        AppSettings? changedSettings = null;
        string status;
        lock (_settingsLock)
        {
            if (!_settings.AutoProfileSelectionEnabled)
            {
                status = "Automatic profile selection is off.";
            }
            else
            {
                var match = ProfileRuleMatcher.Select(_settings, context);
                if (match is null)
                {
                    status = context.HasIdentity
                        ? "No enabled profile rule matched the current car and track."
                        : "Waiting for iRacing car and track identity.";
                }
                else if (_settings.ActiveProfileId.Equals(
                             match.Profile.Id,
                             StringComparison.OrdinalIgnoreCase))
                {
                    status = match.Description;
                }
                else
                {
                    ProfileCatalog.ApplyProfile(_settings, match.Profile.Id);
                    changedSettings = _settings.DeepClone();
                    status = match.Description;
                }
            }
        }

        if (changedSettings is not null)
        {
            lock (_pipelineLock)
            {
                _pipeline.Reset();
            }
            _ = _rumbleController.EmergencyStopAsync("automatic profile changed");
            _logger.Info(status);
            _ = PersistAutomaticProfileAsync(changedSettings);
        }

        var current = CurrentSettingsReference();
        UpdateState(state => state with
        {
            ActiveProfileId = current.ActiveProfileId,
            ActiveProfileName = current.ActiveProfile,
            AutoProfileSelectionEnabled = current.AutoProfileSelectionEnabled,
            ProfileSelectionStatus = status,
            TelemetryContext = context
        });
    }

    private async Task PersistAutomaticProfileAsync(AppSettings settings)
    {
        try
        {
            await _settingsService.SaveAsync(
                settings,
                _lifetime?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error("Could not persist the automatically selected profile.", ex);
        }
    }

    private AppSettings CurrentSettingsReference()
    {
        lock (_settingsLock)
        {
            return _settings;
        }
    }

    private static bool IsIncident(DetectedHapticEvent detected) =>
        detected.Kind is HapticEventKind.Incident1x
            or HapticEventKind.Incident2x
            or HapticEventKind.Incident4x
            or HapticEventKind.IncidentOther;

    private static string SuppressionReason(
        DetectedHapticEvent detected,
        AppSettings settings)
    {
        if (IsIncident(detected)
            && settings.Incidents.SuppressWhenPhysicalImpactDetected
            && detected.HasRelatedPhysicalEvent)
        {
            return "duplicate incident notification is suppressed because a related "
                + "physical impact was detected.";
        }
        return "this event point value, inferred type or category is disabled.";
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

    private void PublishState()
    {
        var settings = CurrentSettingsReference();
        UpdateState(state => state with
        {
            Toolkit = _toolkit.Status,
            HapticsEnabled = settings.HapticsEnabled,
            SimulatedRumble = settings.UseSimulatedRumbleDevice,
            ActiveProfileId = settings.ActiveProfileId,
            ActiveProfileName = settings.ActiveProfile,
            AutoProfileSelectionEnabled = settings.AutoProfileSelectionEnabled,
            RumbleDeviceStatus = settings.UseSimulatedRumbleDevice
                ? _simulatedRumble.StatusDescription
                : _toolkit.StatusDescription,
            TelemetryStatus = _activeTelemetry?.StatusDescription ?? state.TelemetryStatus
        });
    }

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
            && HapticEventPolicy.IsEnabled(detected, settings);
        return $"{detected.Kind} ({detected.Score:F2})"
            + (outputEnabled ? "" : " — haptic output disabled");
    }

    private static string FormatIncident(DetectedHapticEvent incident) =>
        $"{incident.IncidentPoints}x / {incident.IncidentType}"
        + (incident.HasRelatedPhysicalEvent ? " / related impact" : "");
}
