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
                return new AppSettings();
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

            return Validate(settings);
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
            return new AppSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings = Validate(Migrate(settings));
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
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AppSettings Validate(AppSettings settings)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.ActiveProfile = ProfileCatalog.NormalizeName(settings.ActiveProfile);

        settings.Impacts.Sensitivity = Math.Clamp(settings.Impacts.Sensitivity, 0.2, 3.0);
        settings.Impacts.LightThreshold = Math.Clamp(settings.Impacts.LightThreshold, 0.2, 20);
        settings.Impacts.MediumThreshold = Math.Max(
            settings.Impacts.LightThreshold + 0.05,
            Math.Clamp(settings.Impacts.MediumThreshold, 0.25, 25));
        settings.Impacts.StrongThreshold = Math.Max(
            settings.Impacts.MediumThreshold + 0.05,
            Math.Clamp(settings.Impacts.StrongThreshold, 0.3, 30));
        settings.Impacts.CooldownMs = Math.Clamp(settings.Impacts.CooldownMs, 50, 5000);
        settings.Impacts.RolloverCooldownMs = Math.Clamp(
            settings.Impacts.RolloverCooldownMs,
            50,
            2000);
        settings.Impacts.MinimumSpeedMps = Math.Clamp(
            settings.Impacts.MinimumSpeedMps,
            0,
            100);
        settings.Impacts.HysteresisRatio = Math.Clamp(
            settings.Impacts.HysteresisRatio,
            0.1,
            0.95);

        settings.Vertical.Sensitivity = Math.Clamp(settings.Vertical.Sensitivity, 0.2, 3.0);
        settings.Vertical.StrongKerbThreshold = Math.Clamp(
            settings.Vertical.StrongKerbThreshold,
            0.2,
            30);
        settings.Vertical.LandingThreshold = Math.Clamp(
            settings.Vertical.LandingThreshold,
            0.2,
            30);
        settings.Vertical.SevereCompressionThreshold = Math.Clamp(
            settings.Vertical.SevereCompressionThreshold,
            0.2,
            40);
        settings.Vertical.CooldownMs = Math.Clamp(settings.Vertical.CooldownMs, 50, 5000);
        settings.Vertical.MinimumSpeedMps = Math.Clamp(
            settings.Vertical.MinimumSpeedMps,
            0,
            100);

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

        foreach (var pattern in EnumeratePatterns(settings.Effects))
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

        return settings;
    }

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
    }
}
