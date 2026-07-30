using System.Text.Json;
using System.Text.Json.Serialization;
using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Core.Services;

public sealed class SettingsService
{
    private readonly string _path;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public SettingsService(string path, IAppLogger? logger = null)
    {
        _path = path;
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                var defaults = Validate(Migrate(new AppSettings()));
                ProfileCatalog.ApplyActiveProfile(defaults);
                return defaults;
            }

            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                _jsonOptions,
                cancellationToken).ConfigureAwait(false);

            settings ??= new AppSettings();
            var previousSchemaVersion = settings.SchemaVersion;
            settings = Migrate(settings);
            if (previousSchemaVersion < AppSettings.CurrentSchemaVersion)
            {
                _logger.Info(
                    $"Settings migrated from schema {previousSchemaVersion} "
                    + $"to {AppSettings.CurrentSchemaVersion}.");
            }

            settings = Validate(settings);
            ProfileCatalog.ApplyActiveProfile(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            var backup = _path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try
            {
                File.Copy(_path, backup, overwrite: false);
            }
            catch
            {
                // The backup is only a best-effort attempt to preserve the invalid file.
            }

            _logger.Error("Invalid settings file; default values were loaded.", ex);
            var defaults = Validate(Migrate(new AppSettings()));
            ProfileCatalog.ApplyActiveProfile(defaults);
            return defaults;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppSettings> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings = settings.DeepClone();
        settings = Migrate(settings);
        ProfileCatalog.EnsureCatalog(settings);
        ProfileCatalog.CaptureActiveProfile(settings);
        settings = Validate(settings);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("Invalid settings path.");
            Directory.CreateDirectory(directory);

            var temporaryPath = _path + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    _jsonOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
            _logger.Info($"Settings saved: {_path}");
            return settings.DeepClone();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AppSettings Validate(AppSettings settings)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.Impacts ??= new ImpactSettings();
        settings.Vertical ??= new VerticalImpactSettings();
        settings.Incidents ??= new IncidentSettings();
        settings.Triggers ??= new TelemetryTriggerSettings();
        settings.Recording ??= new RecordingSettings();
        settings.PhysicalCalibration ??= new PhysicalCalibrationSettings();
        settings.Input ??= new InputSettings();
        settings.Application ??= new ApplicationBehaviorSettings();
        settings.Safety ??= new SafetySettings();
        settings.Effects ??= new EffectSettings();
        ProfileCatalog.EnsureCatalog(settings);

        ValidateConfiguration(
            settings.Impacts,
            settings.Vertical,
            settings.Incidents,
            settings.Triggers,
            settings.Effects);
        foreach (var profile in settings.Profiles)
        {
            ValidateConfiguration(
                profile.Configuration.Impacts,
                profile.Configuration.Vertical,
                profile.Configuration.Incidents,
                profile.Configuration.Triggers,
                profile.Configuration.Effects);
        }

        settings.Safety.MaximumContinuousRumbleMs = Math.Clamp(
            settings.Safety.MaximumContinuousRumbleMs,
            20,
            1000);
        settings.Safety.MaximumEffectDurationMs = Math.Clamp(
            settings.Safety.MaximumEffectDurationMs,
            50,
            3000);
        settings.Safety.MaximumCallsPerSecond = Math.Clamp(
            settings.Safety.MaximumCallsPerSecond,
            2,
            50);
        settings.Safety.NativeCallTimeoutMs = Math.Clamp(
            settings.Safety.NativeCallTimeoutMs,
            200,
            5000);
        settings.Recording.CircularBufferSeconds = Math.Clamp(
            settings.Recording.CircularBufferSeconds,
            10,
            300);
        if (settings.PhysicalCalibration.Completed
            && !settings.PhysicalCalibration.UsableRangeFound)
        {
            settings.PhysicalCalibration.MinimumClearlyPerceptibleFrequencyHz = 0;
            settings.PhysicalCalibration.PreferredFrequencyHz = 0;
            settings.PhysicalCalibration.MaximumComfortableFrequencyHz = 0;
            settings.PhysicalCalibration.MinimumClearlyPerceptibleDurationMs = 0;
            settings.PhysicalCalibration.PreferredDurationMs = 0;
        }
        else
        {
            settings.PhysicalCalibration.MinimumClearlyPerceptibleFrequencyHz =
                (byte)Math.Clamp(
                    settings.PhysicalCalibration.MinimumClearlyPerceptibleFrequencyHz,
                    (byte)0,
                    (byte)25);
            settings.PhysicalCalibration.PreferredFrequencyHz = (byte)Math.Clamp(
                settings.PhysicalCalibration.PreferredFrequencyHz,
                settings.PhysicalCalibration.MinimumClearlyPerceptibleFrequencyHz,
                (byte)25);
            settings.PhysicalCalibration.MaximumComfortableFrequencyHz =
                (byte)Math.Clamp(
                    settings.PhysicalCalibration.MaximumComfortableFrequencyHz,
                    settings.PhysicalCalibration.PreferredFrequencyHz,
                    (byte)25);
            settings.PhysicalCalibration.MinimumClearlyPerceptibleDurationMs =
                Math.Clamp(
                    settings.PhysicalCalibration.MinimumClearlyPerceptibleDurationMs,
                    20,
                    1000);
            settings.PhysicalCalibration.PreferredDurationMs = Math.Clamp(
                settings.PhysicalCalibration.PreferredDurationMs,
                settings.PhysicalCalibration.MinimumClearlyPerceptibleDurationMs,
                1000);
        }
        ValidateInput(settings.Input);

        return settings;
    }

    private static void ValidateInput(InputSettings input)
    {
        input.Bindings ??= [];
        var defaults = InputSettings.CreateDefaults()
            .ToDictionary(binding => binding.Action);
        input.Bindings = Enum.GetValues<InputAction>()
            .Select(action =>
                input.Bindings.FirstOrDefault(binding =>
                    binding is not null && binding.Action == action)
                ?? defaults[action])
            .ToList();
        foreach (var binding in input.Bindings)
        {
            binding.VirtualKey = Math.Clamp(binding.VirtualKey, 0, 255);
            binding.JoystickDeviceId = Math.Clamp(binding.JoystickDeviceId, 0, 15);
            binding.JoystickButtonNumber = Math.Clamp(
                binding.JoystickButtonNumber,
                0,
                32);
            var allowedModifiers = KeyboardModifier.Alt
                | KeyboardModifier.Control
                | KeyboardModifier.Shift
                | KeyboardModifier.Windows;
            binding.KeyboardModifiers &= allowedModifiers;
            if (binding.VirtualKey == 0)
            {
                binding.KeyboardEnabled = false;
            }
            if (binding.JoystickButtonNumber == 0)
            {
                binding.JoystickEnabled = false;
            }
        }
    }

    private static void ValidateConfiguration(
        ImpactSettings impacts,
        VerticalImpactSettings vertical,
        IncidentSettings incidents,
        TelemetryTriggerSettings triggers,
        EffectSettings effects)
    {
        impacts.Sensitivity = Math.Clamp(impacts.Sensitivity, 0.2, 3.0);
        impacts.LightThreshold = Math.Clamp(impacts.LightThreshold, 0.2, 20);
        impacts.MediumThreshold = Math.Max(
            impacts.LightThreshold + 0.05,
            Math.Clamp(impacts.MediumThreshold, 0.25, 25));
        impacts.StrongThreshold = Math.Max(
            impacts.MediumThreshold + 0.05,
            Math.Clamp(impacts.StrongThreshold, 0.3, 30));
        impacts.CooldownMs = Math.Clamp(impacts.CooldownMs, 50, 5000);
        impacts.RolloverCooldownMs = Math.Clamp(
            impacts.RolloverCooldownMs,
            50,
            2000);
        impacts.MinimumSpeedMps = Math.Clamp(impacts.MinimumSpeedMps, 0, 100);
        impacts.HysteresisRatio = Math.Clamp(impacts.HysteresisRatio, 0.1, 0.95);
        impacts.WarmupMs = Math.Clamp(impacts.WarmupMs, 0, 5000);

        vertical.Sensitivity = Math.Clamp(vertical.Sensitivity, 0.2, 3.0);
        vertical.StrongKerbThreshold = Math.Clamp(
            vertical.StrongKerbThreshold,
            0.2,
            30);
        vertical.LandingThreshold = Math.Clamp(vertical.LandingThreshold, 0.2, 30);
        vertical.SevereCompressionThreshold = Math.Clamp(
            vertical.SevereCompressionThreshold,
            0.2,
            40);
        vertical.CooldownMs = Math.Clamp(vertical.CooldownMs, 50, 5000);
        vertical.MinimumSpeedMps = Math.Clamp(vertical.MinimumSpeedMps, 0, 100);
        vertical.HysteresisRatio = Math.Clamp(vertical.HysteresisRatio, 0.1, 0.95);
        vertical.WarmupMs = Math.Clamp(vertical.WarmupMs, 0, 5000);

        incidents.CooldownMs = Math.Clamp(incidents.CooldownMs, 50, 5000);
        incidents.EvidenceWindowMs = Math.Clamp(incidents.EvidenceWindowMs, 250, 5000);
        if (!Enum.IsDefined(incidents.PatternBasis))
        {
            incidents.PatternBasis = IncidentPatternBasis.PointValue;
        }

        ValidateTriggers(triggers);

        effects.LightImpact ??= new EffectPatternSettings();
        effects.MediumImpact ??= new EffectPatternSettings();
        effects.StrongImpact ??= new EffectPatternSettings();
        effects.Rollover ??= new EffectPatternSettings();
        effects.StrongKerb ??= new EffectPatternSettings();
        effects.WheelDrop ??= new EffectPatternSettings();
        effects.Landing ??= new EffectPatternSettings();
        effects.SevereCompression ??= new EffectPatternSettings();
        effects.Incident1x ??= new EffectPatternSettings();
        effects.Incident2x ??= new EffectPatternSettings();
        effects.Incident4x ??= new EffectPatternSettings();
        effects.IncidentOther ??= new EffectPatternSettings();
        effects.IncidentOffTrack ??= new EffectPatternSettings();
        effects.IncidentLossOfControl ??= new EffectPatternSettings();
        effects.IncidentContact ??= new EffectPatternSettings();
        effects.IncidentRollover ??= new EffectPatternSettings();
        effects.IncidentUnknown ??= new EffectPatternSettings();

        foreach (var pattern in EnumeratePatterns(effects))
        {
            ValidatePattern(pattern);
        }
    }

    private static void ValidateTriggers(TelemetryTriggerSettings triggers)
    {
        triggers.CustomTriggers ??= [];
        triggers.CustomTriggers = triggers.CustomTriggers
            .Where(trigger => trigger is not null)
            .Take(128)
            .ToList();

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trigger in triggers.CustomTriggers)
        {
            trigger.Id = string.IsNullOrWhiteSpace(trigger.Id)
                ? Guid.NewGuid().ToString("N")
                : trigger.Id.Trim();
            while (!usedIds.Add(trigger.Id))
            {
                trigger.Id = Guid.NewGuid().ToString("N");
            }

            trigger.Name = string.IsNullOrWhiteSpace(trigger.Name)
                ? "Recovered telemetry trigger"
                : trigger.Name.Trim()[..Math.Min(trigger.Name.Trim().Length, 80)];
            trigger.Description = (trigger.Description ?? string.Empty).Trim();
            if (trigger.Description.Length > 500)
            {
                trigger.Description = trigger.Description[..500];
            }
            if (!Enum.IsDefined(trigger.TargetEvent)
                || trigger.TargetEvent
                    == PSVR2iRacingHaptics.Core.Models.HapticEventKind.None)
            {
                trigger.TargetEvent =
                    PSVR2iRacingHaptics.Core.Models.HapticEventKind.LightImpact;
            }
            if (!Enum.IsDefined(trigger.SourceMode))
            {
                trigger.SourceMode = TriggerSourceMode.Additive;
            }
            if (!Enum.IsDefined(trigger.MatchMode))
            {
                trigger.MatchMode = TriggerMatchMode.AllConditions;
            }

            trigger.HoldMilliseconds = Math.Clamp(trigger.HoldMilliseconds, 0, 10_000);
            trigger.CooldownMilliseconds = Math.Clamp(
                trigger.CooldownMilliseconds,
                0,
                30_000);
            trigger.ReleaseMilliseconds = Math.Clamp(
                trigger.ReleaseMilliseconds,
                0,
                10_000);
            trigger.Priority = Math.Clamp(trigger.Priority, 1, 200);
            trigger.Conditions ??= [];
            trigger.Conditions = trigger.Conditions
                .Where(condition => condition is not null)
                .Take(32)
                .ToList();
            foreach (var condition in trigger.Conditions)
            {
                if (!Enum.IsDefined(condition.Signal))
                {
                    condition.Signal = TelemetrySignal.HorizontalImpulseG;
                }
                if (!Enum.IsDefined(condition.Comparison))
                {
                    condition.Comparison = TriggerComparison.GreaterThanOrEqual;
                }
                if (!Enum.IsDefined(condition.MissingSignalBehavior))
                {
                    condition.MissingSignalBehavior = MissingSignalBehavior.FailCondition;
                }
                condition.Value = FiniteClamp(condition.Value, -1_000_000, 1_000_000);
                condition.SecondValue = FiniteClamp(
                    condition.SecondValue,
                    -1_000_000,
                    1_000_000);
                condition.EqualityTolerance = FiniteClamp(
                    condition.EqualityTolerance,
                    0,
                    1_000_000);
                if ((condition.Comparison is TriggerComparison.BetweenInclusive
                    or TriggerComparison.OutsideInclusive)
                    && condition.SecondValue < condition.Value)
                {
                    (condition.Value, condition.SecondValue) =
                        (condition.SecondValue, condition.Value);
                }
            }

            trigger.CustomEffect ??= new EffectPatternSettings
            {
                FrequencyHz = 14,
                DurationMs = 120
            };
            ValidatePattern(trigger.CustomEffect);
        }
    }

    private static void ValidatePattern(EffectPatternSettings pattern)
    {
        pattern.FrequencyHz = (byte)Math.Clamp(pattern.FrequencyHz, (byte)0, (byte)25);
        pattern.TailFrequencyHz = (byte)Math.Clamp(
            pattern.TailFrequencyHz,
            (byte)0,
            (byte)25);
        pattern.DurationMs = Math.Clamp(pattern.DurationMs, 10, 1000);
        pattern.TailDurationMs = Math.Clamp(pattern.TailDurationMs, 0, 1000);
        pattern.PulseCount = Math.Clamp(pattern.PulseCount, 1, 8);
        pattern.GapMs = Math.Clamp(pattern.GapMs, 0, 1000);
    }

    private static double FiniteClamp(double value, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;

    private static AppSettings Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion >= AppSettings.CurrentSchemaVersion)
        {
            return settings;
        }

        if (settings.SchemaVersion <= 1)
        {
            var normalizedProfile = ProfileCatalog.NormalizeName(settings.ActiveProfile);
            var targetProfile = normalizedProfile is "Gentle" or "Strong"
                ? ProfileCatalog.Create(normalizedProfile)
                : new AppSettings();

            UpgradeLegacyPattern(
                settings.Effects.LightImpact,
                targetProfile.Effects.LightImpact,
                legacyDurationMs: 75);
            UpgradeLegacyPattern(
                settings.Effects.MediumImpact,
                targetProfile.Effects.MediumImpact,
                legacyDurationMs: 125);
            UpgradeLegacyPattern(
                settings.Effects.StrongImpact,
                targetProfile.Effects.StrongImpact,
                legacyDurationMs: 145,
                legacyGapMs: 40,
                legacyTailDurationMs: 80);
            UpgradeLegacyPattern(
                settings.Effects.Rollover,
                targetProfile.Effects.Rollover,
                legacyDurationMs: 90,
                legacyGapMs: 45);
            UpgradeLegacyPattern(
                settings.Effects.StrongKerb,
                targetProfile.Effects.StrongKerb,
                legacyDurationMs: 60,
                legacyFrequencyHz: 13);
            UpgradeLegacyPattern(
                settings.Effects.WheelDrop,
                targetProfile.Effects.WheelDrop,
                legacyDurationMs: 80,
                legacyFrequencyHz: 15);
            UpgradeLegacyPattern(
                settings.Effects.Landing,
                targetProfile.Effects.Landing,
                legacyDurationMs: 60,
                legacyGapMs: 30,
                legacyTailDurationMs: 50,
                legacyFrequencyHz: 18,
                legacyTailFrequencyHz: 14);
            UpgradeLegacyPattern(
                settings.Effects.SevereCompression,
                targetProfile.Effects.SevereCompression,
                legacyDurationMs: 105);

            if (settings.Safety.MaximumContinuousRumbleMs == 300)
            {
                settings.Safety.MaximumContinuousRumbleMs =
                    targetProfile.Safety.MaximumContinuousRumbleMs;
            }
            if (settings.Safety.MaximumEffectDurationMs == 700)
            {
                settings.Safety.MaximumEffectDurationMs =
                    targetProfile.Safety.MaximumEffectDurationMs;
            }
        }

        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        return settings;
    }

    private static void UpgradeLegacyPattern(
        EffectPatternSettings current,
        EffectPatternSettings target,
        int legacyDurationMs,
        int? legacyGapMs = null,
        int? legacyTailDurationMs = null,
        byte? legacyFrequencyHz = null,
        byte? legacyTailFrequencyHz = null)
    {
        if (legacyFrequencyHz.HasValue
            && current.FrequencyHz == legacyFrequencyHz.Value)
        {
            current.FrequencyHz = target.FrequencyHz;
        }
        if (current.DurationMs == legacyDurationMs)
        {
            current.DurationMs = target.DurationMs;
        }
        if (legacyGapMs.HasValue && current.GapMs == legacyGapMs.Value)
        {
            current.GapMs = target.GapMs;
        }
        if (legacyTailDurationMs.HasValue
            && current.TailDurationMs == legacyTailDurationMs.Value)
        {
            current.TailDurationMs = target.TailDurationMs;
        }
        if (legacyTailFrequencyHz.HasValue
            && current.TailFrequencyHz == legacyTailFrequencyHz.Value)
        {
            current.TailFrequencyHz = target.TailFrequencyHz;
        }
    }

    private static IEnumerable<EffectPatternSettings> EnumeratePatterns(EffectSettings effects)
    {
        yield return effects.LightImpact;
        yield return effects.MediumImpact;
        yield return effects.StrongImpact;
        yield return effects.Rollover;
        yield return effects.StrongKerb;
        yield return effects.WheelDrop;
        yield return effects.Landing;
        yield return effects.SevereCompression;
        yield return effects.Incident1x;
        yield return effects.Incident2x;
        yield return effects.Incident4x;
        yield return effects.IncidentOther;
        yield return effects.IncidentOffTrack;
        yield return effects.IncidentLossOfControl;
        yield return effects.IncidentContact;
        yield return effects.IncidentRollover;
        yield return effects.IncidentUnknown;
    }
}
