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
        ("Default settings without a file", SettingsDefaults),
        ("Settings persistence and validation", SettingsRoundTrip),
        ("Settings seed the four factory profiles", SettingsSeedFactoryProfiles),
        ("Profile CRUD preserves factory profiles", ProfileCrudWorks),
        ("Profiles preserve independent detector settings", ProfilesPreserveIndependentSettings),
        ("Profile rules persist with settings", ProfileRulesPersist),
        ("Version 1 settings receive balanced effects", SettingsMigratesLegacyEffects),
        ("Migration preserves custom duration", SettingsMigrationPreservesCustomDuration),
        ("Legacy Portuguese profile names are migrated", LegacyProfileNameMigrates),
        ("Automatic rules match exact car and track identity", ProfileRuleExactMatch),
        ("Automatic rules support case-insensitive wildcards", ProfileRuleWildcardMatch),
        ("Automatic rules prefer priority then specificity", ProfileRuleOrdering),
        ("Automatic rules remain optional", ProfileRuleSelectionIsOptional),
        ("SessionInfo parser selects the player driver", SessionInfoParserSelectsPlayer),
        ("SessionInfo parser tolerates partial YAML", SessionInfoParserToleratesPartialYaml),
        ("iRacing SessionInfo header offsets match the SDK", SessionInfoHeaderOffsetsMatchSdk),
        ("Signal filtering and jerk calculation", SignalProcessorCalculatesJerk),
        ("Normal acceleration is not a collision", NormalAccelerationIsIgnored),
        ("Hard braking is not a collision", HardBrakingIsIgnored),
        ("Light kerbs are ignored by default", LightKerbIsIgnored),
        ("Strong kerbs are detected", StrongKerbIsDetected),
        ("Side impacts are classified", SideImpactIsDetected),
        ("Front impacts are classified", FrontImpactIsDetected),
        ("Strong collisions have strong severity", StrongCollisionIsDetected),
        ("Rollover allows consecutive impacts", RolloverIsDetected),
        ("Landing is detected after airborne state", LandingIsDetected),
        ("Wheel drop uses suspension asymmetry", WheelDropIsDetected),
        ("Invalid telemetry resets warmup", InvalidTelemetryResetsPipeline),
        ("1x off-track incidents are detected exactly", OffTrackIncidentIsDetected),
        ("2x loss-of-control incidents are classified", LossOfControlIncidentIsDetected),
        ("4x contact incidents coexist with physical impacts", ContactIncidentIsDetected),
        ("Incident counter decreases do not create events", IncidentCounterDecreaseIsIgnored),
        ("Incident point and type switches both gate output", IncidentSwitchesGateOutput),
        ("Duplicate incident rumble is suppressed by default", DuplicateIncidentIsSuppressed),
        ("Effect mapping distinguishes impact and landing", EffectMappingDiffers),
        ("Incident mapper can use exact point patterns", IncidentMapperUsesPointPattern),
        ("Incident mapper can use inferred-type patterns", IncidentMapperUsesTypePattern),
        ("Simulated scenarios use balanced effects", SimulatedScenariosUseBalancedEffects),
        ("Default patterns respect conservative limits", DefaultEffectsRespectSafetyLimits),
        ("Per-event switches control haptic output", EventSwitchesControlOutput),
        ("Disabled effects remain detectable", DisabledEffectsRemainDetectable),
        ("Strong effect is not interrupted by a kerb", StrongEffectRejectsKerb),
        ("Strong effect replaces a kerb", StrongEffectPreemptsKerb),
        ("Controller sends zero after an effect", ControllerAlwaysSendsZero),
        ("Cancellation sends zero", CancellationSendsZero),
        ("Emergency stop sends zero", EmergencyStopSendsZero),
        ("Unavailable device rejects an effect", UnavailableDeviceRejectsEffect),
        ("JSONL recording preserves frames and markers", RecorderWritesReplayableJsonl),
        ("Calibration matches markers to detections", CalibrationMatchesDetection),
        ("Calibration proposes a bounded missed-event adjustment", CalibrationRecommendsForMissedEvent),
        ("Incident markers do not alter physical thresholds", CalibrationLeavesIncidentThresholdsAlone),
        ("Missing Toolkit DLL does not crash", MissingToolkitDoesNotCrash),
        ("Missing iRacing does not crash", MissingIRacingDoesNotCrash)
    ];

    private static async Task<int> Main()
    {
        var failures = new List<string>();
        Console.WriteLine($"Running {Tests.Count} tests...");
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
                Console.WriteLine($"[FAILED] {name}");
                Console.WriteLine($"         {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            failures.Count == 0
                ? $"Result: {Tests.Count}/{Tests.Count} tests passed."
                : $"Result: {Tests.Count - failures.Count}/{Tests.Count} passed.");
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
            Equal("Default", settings.ActiveProfile);
            True(settings.Impacts.Enabled);
            True(settings.Impacts.LightEnabled);
            True(settings.Impacts.MediumEnabled);
            True(settings.Impacts.StrongEnabled);
            True(settings.Impacts.RolloverEnabled);
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
                ActiveProfile = "Custom",
                UseSimulatedRumbleDevice = true
            };
            settings.Impacts.LightThreshold = 2.2;
            settings.Impacts.LightEnabled = false;
            settings.Effects.StrongImpact.FrequencyHz = 99;
            await service.SaveAsync(settings);
            var loaded = await service.LoadAsync();
            Equal("Custom", loaded.ActiveProfile);
            True(loaded.UseSimulatedRumbleDevice);
            False(loaded.Impacts.LightEnabled);
            Near(2.2, loaded.Impacts.LightThreshold, 0.001);
            Equal((byte)25, loaded.Effects.StrongImpact.FrequencyHz);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task SettingsSeedFactoryProfiles()
    {
        var directory = TempDirectory();
        try
        {
            var settings = await new SettingsService(
                Path.Combine(directory, "settings.json")).LoadAsync();
            Equal(4, settings.Profiles.Count(profile => profile.IsBuiltIn));
            True(settings.Profiles.Any(profile =>
                profile.Id == ProfileCatalog.DefaultProfileId));
            True(settings.Profiles.Any(profile =>
                profile.Id == ProfileCatalog.GentleProfileId));
            True(settings.Profiles.Any(profile =>
                profile.Id == ProfileCatalog.StrongProfileId));
            True(settings.Profiles.Any(profile =>
                profile.Id == ProfileCatalog.CustomProfileId));
            False(settings.Incidents.Enabled);
            Equal(IncidentPatternBasis.PointValue, settings.Incidents.PatternBasis);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task ProfileCrudWorks()
    {
        var settings = ProfileCatalog.Create("Default");
        var created = ProfileCatalog.AddProfile(settings, "GT sprint");
        False(created.IsBuiltIn);
        var duplicate = ProfileCatalog.DuplicateProfile(
            settings,
            created.Id,
            "GT sprint wet");
        ProfileCatalog.RenameProfile(settings, duplicate.Id, "GT sprint rain");
        Equal(
            "GT sprint rain",
            ProfileCatalog.FindProfile(settings, duplicate.Id)!.Name);

        settings.ProfileRules.Add(new ProfileAssignmentRule
        {
            ProfileId = duplicate.Id,
            TrackNamePattern = "okayama"
        });
        ProfileCatalog.ApplyProfile(settings, duplicate.Id);
        ProfileCatalog.DeleteProfile(settings, duplicate.Id);
        Equal(ProfileCatalog.DefaultProfileId, settings.ActiveProfileId);
        False(settings.ProfileRules.Any(rule => rule.ProfileId == duplicate.Id));
        Equal(4, settings.Profiles.Count(profile => profile.IsBuiltIn));

        Throws<InvalidOperationException>(() =>
            ProfileCatalog.DeleteProfile(settings, ProfileCatalog.DefaultProfileId));
        Throws<InvalidOperationException>(() =>
            ProfileCatalog.RenameProfile(
                settings,
                ProfileCatalog.DefaultProfileId,
                "Renamed"));
        return Task.CompletedTask;
    }

    private static Task ProfilesPreserveIndependentSettings()
    {
        var settings = ProfileCatalog.Create("Default");
        var defaultThreshold = settings.Impacts.LightThreshold;
        var profile = ProfileCatalog.AddProfile(settings, "Formula setup");
        ProfileCatalog.ApplyProfile(settings, profile.Id);
        settings.Impacts.LightThreshold = 3.45;
        settings.Incidents.Enabled = true;
        settings.Incidents.PatternBasis = IncidentPatternBasis.InferredType;
        ProfileCatalog.CaptureActiveProfile(settings);

        ProfileCatalog.ApplyProfile(settings, ProfileCatalog.DefaultProfileId);
        Near(defaultThreshold, settings.Impacts.LightThreshold, 0.001);
        False(settings.Incidents.Enabled);

        ProfileCatalog.ApplyProfile(settings, profile.Id);
        Near(3.45, settings.Impacts.LightThreshold, 0.001);
        True(settings.Incidents.Enabled);
        Equal(IncidentPatternBasis.InferredType, settings.Incidents.PatternBasis);
        return Task.CompletedTask;
    }

    private static async Task ProfileRulesPersist()
    {
        var directory = TempDirectory();
        try
        {
            var service = new SettingsService(Path.Combine(directory, "settings.json"));
            var settings = await service.LoadAsync();
            var profile = ProfileCatalog.AddProfile(settings, "Porsche at Spa");
            settings.ProfileRules.Add(new ProfileAssignmentRule
            {
                Name = "Porsche Spa",
                ProfileId = profile.Id,
                Priority = 50,
                CarPathPattern = "porsche911rgt3",
                TrackNamePattern = "spa"
            });
            await service.SaveAsync(settings);

            var loaded = await service.LoadAsync();
            var rule = loaded.ProfileRules.Single();
            Equal("Porsche Spa", rule.Name);
            Equal(50, rule.Priority);
            True(ProfileCatalog.FindProfile(loaded, rule.ProfileId) is not null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task SettingsMigratesLegacyEffects()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var legacy = LegacySettings();
            await File.WriteAllTextAsync(
                path,
                System.Text.Json.JsonSerializer.Serialize(legacy));

            var loaded = await new SettingsService(path).LoadAsync();

            Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
            Equal(120, loaded.Effects.LightImpact.DurationMs);
            Equal(160, loaded.Effects.MediumImpact.DurationMs);
            Equal(200, loaded.Effects.StrongImpact.DurationMs);
            Equal(100, loaded.Effects.StrongImpact.TailDurationMs);
            Equal(110, loaded.Effects.StrongKerb.DurationMs);
            Equal((byte)14, loaded.Effects.StrongKerb.FrequencyHz);
            Equal(140, loaded.Effects.Landing.DurationMs);
            Equal(110, loaded.Effects.Landing.TailDurationMs);
            Equal((byte)15, loaded.Effects.Landing.TailFrequencyHz);
            Equal(250, loaded.Safety.MaximumContinuousRumbleMs);
            Equal(550, loaded.Safety.MaximumEffectDurationMs);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task SettingsMigrationPreservesCustomDuration()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var legacy = LegacySettings();
            legacy.ActiveProfile = "Custom";
            legacy.Effects.StrongKerb.DurationMs = 175;
            await File.WriteAllTextAsync(
                path,
                System.Text.Json.JsonSerializer.Serialize(legacy));

            var loaded = await new SettingsService(path).LoadAsync();

            Equal(175, loaded.Effects.StrongKerb.DurationMs);
            Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task LegacyProfileNameMigrates()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var settings = new AppSettings
            {
                SchemaVersion = 2,
                ActiveProfile = "Padrão"
            };
            await File.WriteAllTextAsync(
                path,
                System.Text.Json.JsonSerializer.Serialize(settings));

            var loaded = await new SettingsService(path).LoadAsync();

            Equal("Default", loaded.ActiveProfile);
            Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task ProfileRuleExactMatch()
    {
        var settings = ProfileCatalog.Create("Default");
        var profile = ProfileCatalog.AddProfile(settings, "Porsche Spa");
        settings.AutoProfileSelectionEnabled = true;
        settings.ProfileRules.Add(new ProfileAssignmentRule
        {
            Name = "Exact assignment",
            ProfileId = profile.Id,
            CarPathPattern = "porsche911rgt3",
            TrackNamePattern = "spa",
            TrackConfigPattern = "grand prix pits"
        });

        var match = ProfileRuleMatcher.Select(settings, PlayerContext());
        True(match is not null);
        Equal(profile.Id, match!.Profile.Id);
        True(match.Description.Contains("Porsche", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    private static Task ProfileRuleWildcardMatch()
    {
        var settings = ProfileCatalog.Create("Default");
        var profile = ProfileCatalog.AddProfile(settings, "GT3");
        settings.AutoProfileSelectionEnabled = true;
        settings.ProfileRules.Add(new ProfileAssignmentRule
        {
            ProfileId = profile.Id,
            CarPathPattern = "PORSCHE*GT3",
            TrackNamePattern = "s?a"
        });

        var match = ProfileRuleMatcher.Select(settings, PlayerContext());
        Equal(profile.Id, match!.Profile.Id);
        return Task.CompletedTask;
    }

    private static Task ProfileRuleOrdering()
    {
        var settings = ProfileCatalog.Create("Default");
        var broad = ProfileCatalog.AddProfile(settings, "Broad");
        var exact = ProfileCatalog.AddProfile(settings, "Exact");
        var priority = ProfileCatalog.AddProfile(settings, "Priority");
        settings.AutoProfileSelectionEnabled = true;
        settings.ProfileRules.AddRange(
        [
            new ProfileAssignmentRule
            {
                Name = "Broad",
                ProfileId = broad.Id,
                Priority = 10,
                CarPathPattern = "porsche*"
            },
            new ProfileAssignmentRule
            {
                Name = "Exact",
                ProfileId = exact.Id,
                Priority = 10,
                CarPathPattern = "porsche911rgt3",
                TrackNamePattern = "spa"
            },
            new ProfileAssignmentRule
            {
                Name = "Priority",
                ProfileId = priority.Id,
                Priority = 11,
                TrackNamePattern = "*"
            }
        ]);

        Equal(
            priority.Id,
            ProfileRuleMatcher.Select(settings, PlayerContext())!.Profile.Id);
        settings.ProfileRules.Single(rule => rule.Name == "Priority").Enabled = false;
        Equal(
            exact.Id,
            ProfileRuleMatcher.Select(settings, PlayerContext())!.Profile.Id);
        return Task.CompletedTask;
    }

    private static Task ProfileRuleSelectionIsOptional()
    {
        var settings = ProfileCatalog.Create("Default");
        var profile = ProfileCatalog.AddProfile(settings, "Optional");
        settings.ProfileRules.Add(new ProfileAssignmentRule
        {
            ProfileId = profile.Id,
            TrackNamePattern = "*"
        });

        False(settings.AutoProfileSelectionEnabled);
        True(ProfileRuleMatcher.Select(settings, PlayerContext()) is null);
        settings.AutoProfileSelectionEnabled = true;
        True(ProfileRuleMatcher.Select(settings, new TelemetryContext()) is null);
        return Task.CompletedTask;
    }

    private static Task SessionInfoParserSelectsPlayer()
    {
        const string yaml = """
            WeekendInfo:
              TrackName: spa
              TrackID: 100
              TrackDisplayName: "Circuit de Spa-Francorchamps"
              TrackDisplayShortName: Spa
              TrackConfigName: Grand Prix Pits
            DriverInfo:
              DriverCarIdx: 2
              Drivers:
              - CarIdx: 0
                CarID: 1
                CarPath: other_car
              - CarIdx: 2
                CarID: 105
                CarClassID: 42
                CarPath: !!str porsche911rgt3
                CarScreenName: "Porsche 911 GT3 R"
                CarScreenNameShort: Porsche
                CarClassShortName: GT3
            """;

        var context = IRacingSessionInfoParser.Parse(yaml, 17);
        Equal(17, context.SessionInfoUpdate);
        Equal(2, context.DriverCarIdx!.Value);
        Equal(105, context.CarId!.Value);
        Equal(42, context.CarClassId!.Value);
        Equal("porsche911rgt3", context.CarPath);
        Equal("Porsche 911 GT3 R", context.CarName);
        Equal("GT3", context.CarClass);
        Equal(100, context.TrackId!.Value);
        Equal("spa", context.TrackName);
        Equal("Circuit de Spa-Francorchamps", context.TrackDisplayName);
        Equal("Grand Prix Pits", context.TrackConfigName);
        return Task.CompletedTask;
    }

    private static Task SessionInfoParserToleratesPartialYaml()
    {
        const string yaml = """
            Unrelated:
              Broken: [value
            WeekendInfo:
              TrackName: okayama
              TrackDisplayShortName: Okayama
            DriverInfo:
              DriverCarIdx: 99
              Drivers:
              - CarIdx: 0
                CarPath: ignored
            """;
        var context = IRacingSessionInfoParser.Parse(yaml);
        Equal("okayama", context.TrackName);
        Equal("Okayama", context.TrackDisplayName);
        Equal(string.Empty, context.CarPath);
        True(context.HasIdentity);
        Equal(-1, IRacingSessionInfoParser.Parse(null).SessionInfoUpdate);
        return Task.CompletedTask;
    }

    private static Task SessionInfoHeaderOffsetsMatchSdk()
    {
        Equal(12, IRacingSharedMemoryClient.SessionInfoUpdateOffset);
        Equal(16, IRacingSharedMemoryClient.SessionInfoLengthOffset);
        Equal(20, IRacingSharedMemoryClient.SessionInfoDataOffsetOffset);
        return Task.CompletedTask;
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
        True(processed.LatJerk > 100, $"Unexpected jerk: {processed.LatJerk:F2}");
        True(processed.HorizontalImpulseG > 0.5);
        return Task.CompletedTask;
    }

    private static Task NormalAccelerationIsIgnored()
    {
        var events = Detect(TelemetryScenario.NormalAcceleration);
        False(events.Any(IsCollision), "Normal acceleration generated a collision.");
        return Task.CompletedTask;
    }

    private static Task HardBrakingIsIgnored()
    {
        var events = Detect(TelemetryScenario.HardBraking);
        False(events.Any(IsCollision), "Hard braking/wheel lock generated a collision. "
            + Describe(events));
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
        True(result.SelectedEvent is null, "An event was accepted before warmup completed.");
        False(result.Diagnostics.IsWarm);
        return Task.CompletedTask;
    }

    private static Task OffTrackIncidentIsDetected()
    {
        var incidents = DetectCandidates(TelemetryScenario.OffTrackIncident1x)
            .Where(IsIncident)
            .ToArray();
        Equal(1, incidents.Length);
        Equal(HapticEventKind.Incident1x, incidents[0].Kind);
        Equal(1, incidents[0].IncidentPoints);
        Equal(IncidentType.OffTrack, incidents[0].IncidentType);
        return Task.CompletedTask;
    }

    private static Task LossOfControlIncidentIsDetected()
    {
        var incident = DetectCandidates(TelemetryScenario.LossOfControlIncident2x)
            .Single(IsIncident);
        Equal(HapticEventKind.Incident2x, incident.Kind);
        Equal(2, incident.IncidentPoints);
        Equal(IncidentType.LossOfControl, incident.IncidentType);
        return Task.CompletedTask;
    }

    private static Task ContactIncidentIsDetected()
    {
        var candidates = DetectCandidates(TelemetryScenario.ContactIncident4x);
        var incident = candidates.Single(candidate =>
            candidate.Kind == HapticEventKind.Incident4x);
        Equal(4, incident.IncidentPoints);
        Equal(IncidentType.Contact, incident.IncidentType);
        True(incident.HasRelatedPhysicalEvent);
        True(candidates.Any(IsCollision));
        return Task.CompletedTask;
    }

    private static Task IncidentCounterDecreaseIsIgnored()
    {
        var pipeline = new HapticDetectionPipeline();
        var settings = new AppSettings();
        var start = DateTimeOffset.UtcNow;
        for (var index = 0; index < 70; index++)
        {
            pipeline.Process(ValidFrame(start.AddSeconds(index / 60.0), index) with
            {
                IncidentCount = 4
            }, settings);
        }

        var result = pipeline.Process(ValidFrame(start.AddSeconds(1.3), 100) with
        {
            IncidentCount = 2
        }, settings);
        False(result.Candidates.Any(IsIncident));
        Equal(0, result.Diagnostics.IncidentPointDelta);
        return Task.CompletedTask;
    }

    private static Task IncidentSwitchesGateOutput()
    {
        var settings = new AppSettings();
        settings.Incidents.Enabled = true;
        var incident = IncidentEvent(
            HapticEventKind.Incident2x,
            2,
            IncidentType.LossOfControl);
        True(HapticEventPolicy.IsEnabled(incident, settings));

        settings.Incidents.TwoPointEnabled = false;
        False(HapticEventPolicy.IsEnabled(incident, settings));
        settings.Incidents.TwoPointEnabled = true;
        settings.Incidents.LossOfControlEnabled = false;
        False(HapticEventPolicy.IsEnabled(incident, settings));
        settings.Incidents.LossOfControlEnabled = true;
        True(HapticEventPolicy.IsEnabled(incident, settings));
        return Task.CompletedTask;
    }

    private static Task DuplicateIncidentIsSuppressed()
    {
        var settings = new AppSettings();
        settings.Incidents.Enabled = true;
        var incident = IncidentEvent(
            HapticEventKind.Incident4x,
            4,
            IncidentType.Contact,
            relatedPhysicalEvent: true);
        False(HapticEventPolicy.IsEnabled(incident, settings));
        settings.Incidents.SuppressWhenPhysicalImpactDetected = false;
        True(HapticEventPolicy.IsEnabled(incident, settings));
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
            "test",
            diag), settings.Effects);
        var landing = mapper.Map(new DetectedHapticEvent(
            DateTimeOffset.UtcNow,
            HapticEventKind.Landing,
            EventSeverity.Medium,
            3,
            70,
            ImpactDirection.NotApplicable,
            "test",
            diag), settings.Effects);
        True(impact.Pulses[0].FrequencyHz != landing.Pulses[0].FrequencyHz);
        True(landing.Pulses.Count == 2);
        return Task.CompletedTask;
    }

    private static Task IncidentMapperUsesPointPattern()
    {
        var settings = new AppSettings();
        settings.Incidents.PatternBasis = IncidentPatternBasis.PointValue;
        settings.Effects.Incident2x.FrequencyHz = 17;
        settings.Effects.IncidentOffTrack.FrequencyHz = 10;
        var effect = new RumbleEffectMapper().Map(
            IncidentEvent(
                HapticEventKind.Incident2x,
                2,
                IncidentType.OffTrack),
            settings.Effects,
            settings.Incidents);
        Equal((byte)17, effect.Pulses[0].FrequencyHz);
        True(effect.Name.Contains("2x", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    private static Task IncidentMapperUsesTypePattern()
    {
        var settings = new AppSettings();
        settings.Incidents.PatternBasis = IncidentPatternBasis.InferredType;
        settings.Effects.Incident4x.FrequencyHz = 24;
        settings.Effects.IncidentOffTrack.FrequencyHz = 11;
        var effect = new RumbleEffectMapper().Map(
            IncidentEvent(
                HapticEventKind.Incident4x,
                4,
                IncidentType.OffTrack),
            settings.Effects,
            settings.Incidents);
        Equal((byte)11, effect.Pulses[0].FrequencyHz);
        True(effect.Name.Contains("off track", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    private static Task DefaultEffectsRespectSafetyLimits()
    {
        var settings = new AppSettings();
        var mapper = new RumbleEffectMapper();
        var diagnostics = new ProcessedTelemetry();

        foreach (var kind in Enum.GetValues<HapticEventKind>())
        {
            if (kind == HapticEventKind.None)
            {
                continue;
            }

            var effect = mapper.Map(new DetectedHapticEvent(
                DateTimeOffset.UtcNow,
                kind,
                EventSeverity.Medium,
                3,
                50,
                ImpactDirection.NotApplicable,
                "test",
                diagnostics), settings.Effects);

            True(
                effect.TotalDurationMs <= settings.Safety.MaximumEffectDurationMs,
                $"{kind} exceeded the total limit: {effect.TotalDurationMs} ms.");
            True(
                effect.Pulses.All(
                    pulse => pulse.DurationMs <= settings.Safety.MaximumContinuousRumbleMs),
                $"{kind} exceeded the continuous limit.");
        }

        var strong = settings.Effects.StrongImpact;
        var landing = settings.Effects.Landing;
        True(strong.DurationMs >= settings.Effects.MediumImpact.DurationMs);
        True(landing.TailDurationMs > 0);
        True(settings.Effects.StrongKerb.DurationMs < strong.DurationMs);
        return Task.CompletedTask;
    }

    private static Task SimulatedScenariosUseBalancedEffects()
    {
        var settings = new AppSettings();
        var mapper = new RumbleEffectMapper();

        var kerb = Detect(TelemetryScenario.StrongKerb)
            .First(x => x.Kind == HapticEventKind.StrongKerb);
        var kerbEffect = mapper.Map(kerb, settings.Effects);
        Equal((byte)14, kerbEffect.Pulses[0].FrequencyHz);
        Equal(110, kerbEffect.Pulses[0].DurationMs);
        Equal(1, kerbEffect.Pulses.Count);

        var landing = Detect(TelemetryScenario.Landing)
            .First(x => x.Kind == HapticEventKind.Landing);
        var landingEffect = mapper.Map(landing, settings.Effects);
        Equal(2, landingEffect.Pulses.Count);
        Equal((byte)19, landingEffect.Pulses[0].FrequencyHz);
        Equal(140, landingEffect.Pulses[0].DurationMs);
        Equal(60, landingEffect.Pulses[0].PauseAfterMs);
        Equal((byte)15, landingEffect.Pulses[1].FrequencyHz);
        Equal(110, landingEffect.Pulses[1].DurationMs);
        return Task.CompletedTask;
    }

    private static Task EventSwitchesControlOutput()
    {
        var settings = new AppSettings();
        foreach (var kind in new[]
        {
            HapticEventKind.LightImpact,
            HapticEventKind.MediumImpact,
            HapticEventKind.StrongImpact,
            HapticEventKind.RolloverImpact,
            HapticEventKind.StrongKerb,
            HapticEventKind.WheelDrop,
            HapticEventKind.Landing,
            HapticEventKind.SevereVerticalCompression
        })
        {
            True(HapticEventPolicy.IsEnabled(kind, settings), kind.ToString());
        }

        settings.Impacts.LightEnabled = false;
        settings.Impacts.RolloverEnabled = false;
        settings.Vertical.StrongKerbsEnabled = false;
        settings.Vertical.LightKerbsEnabled = false;
        settings.Vertical.LandingsEnabled = false;

        False(HapticEventPolicy.IsEnabled(HapticEventKind.LightImpact, settings));
        True(HapticEventPolicy.IsEnabled(HapticEventKind.MediumImpact, settings));
        False(HapticEventPolicy.IsEnabled(HapticEventKind.RolloverImpact, settings));
        False(HapticEventPolicy.IsEnabled(HapticEventKind.StrongKerb, settings));
        False(HapticEventPolicy.IsEnabled(HapticEventKind.Landing, settings));
        True(HapticEventPolicy.IsEnabled(HapticEventKind.WheelDrop, settings));
        return Task.CompletedTask;
    }

    private static Task DisabledEffectsRemainDetectable()
    {
        var collisionSettings = new AppSettings();
        collisionSettings.Impacts.Enabled = false;
        var collisionEvents = Detect(
            TelemetryScenario.StrongCollision,
            collisionSettings);
        True(
            collisionEvents.Any(x => x.Kind == HapticEventKind.StrongImpact),
            Describe(collisionEvents));
        False(HapticEventPolicy.IsEnabled(
            HapticEventKind.StrongImpact,
            collisionSettings));

        var landingSettings = new AppSettings();
        landingSettings.Vertical.LandingsEnabled = false;
        var landingEvents = Detect(TelemetryScenario.Landing, landingSettings);
        True(
            landingEvents.Any(x => x.Kind == HapticEventKind.Landing),
            Describe(landingEvents));
        False(HapticEventPolicy.IsEnabled(
            HapticEventKind.Landing,
            landingSettings));
        return Task.CompletedTask;
    }

    private static async Task StrongEffectRejectsKerb()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        var strong = new RumbleEffect("strong", 100, [new RumblePulse(24, 80)]);
        var kerb = new RumbleEffect("kerb", 40, [new RumblePulse(13, 20)]);
        True(await controller.TryPlayAsync(strong));
        await Task.Delay(10);
        False(await controller.TryPlayAsync(kerb));
        await Task.Delay(120);
    }

    private static async Task StrongEffectPreemptsKerb()
    {
        var device = new SimulatedRumbleDevice();
        await using var controller = Controller(device);
        var kerb = new RumbleEffect("kerb", 40, [new RumblePulse(13, 100)]);
        var strong = new RumbleEffect("strong", 100, [new RumblePulse(24, 30)]);
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
            new RumbleEffect("test", 50, [new RumblePulse(18, 20)])));
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
            new RumbleEffect("cancel", 50, [new RumblePulse(20, 200)]),
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
            new RumbleEffect("stop", 50, [new RumblePulse(21, 200)])));
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
            new RumbleEffect("unavailable", 50, [new RumblePulse(20, 20)])));
    }

    private static async Task RecorderWritesReplayableJsonl()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "recording.jsonl");
            await using var recorder = new TelemetryRecorder();
            await recorder.StartAsync(path);
            var frame = ValidFrame(DateTimeOffset.UtcNow, 1) with
            {
                Context = PlayerContext()
            };
            await recorder.RecordFrameAsync(frame);
            await recorder.MarkAsync("Impact");
            await recorder.StopAsync();
            var entries = new List<TelemetryLogEntry>();
            await foreach (var entry in TelemetryReplayClient.ReadEntriesAsync(path))
            {
                entries.Add(entry);
            }
            Equal(2, entries.Count);
            Equal("frame", entries[0].EntryType);
            Equal("marker", entries[1].EntryType);
            Equal("porsche911rgt3", entries[0].Frame!.Context.CarPath);
            Equal("spa", entries[1].Frame!.Context.TrackName);
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
                    await recorder.MarkAsync("Impact");
                }
            }
            await recorder.StopAsync();
            var report = await CalibrationAnalyzer.AnalyzeAsync(path, new AppSettings());
            Equal(1, report.MarkerCount);
            True(report.MatchedCount == 1, $"Report: {report}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task CalibrationRecommendsForMissedEvent()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "missed-event.jsonl");
            await using var recorder = new TelemetryRecorder();
            await recorder.StartAsync(path);
            foreach (var frame in TelemetryScenarioFactory.Create(
                         TelemetryScenario.StrongCollision))
            {
                await recorder.RecordFrameAsync(frame);
                if (Math.Abs(frame.LatAccelMps2) > 50)
                {
                    await recorder.MarkAsync("Impact");
                }
            }
            await recorder.StopAsync();

            var settings = new AppSettings();
            settings.Impacts.LightThreshold = 100;
            settings.Impacts.MediumThreshold = 101;
            settings.Impacts.StrongThreshold = 102;
            var report = await CalibrationAnalyzer.AnalyzeAsync(path, settings);
            Equal(1, report.MissedCount);
            var recommendation = report.Recommendations.Single();
            Equal("Impacts.LightThreshold", recommendation.SettingPath);
            True(recommendation.CanApply);
            True(recommendation.SuggestedValue < recommendation.CurrentValue);
            True(recommendation.SuggestedValue >= 0.2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task CalibrationLeavesIncidentThresholdsAlone()
    {
        var directory = TempDirectory();
        try
        {
            var path = Path.Combine(directory, "incident-marker.jsonl");
            await using var recorder = new TelemetryRecorder();
            await recorder.StartAsync(path);
            var previous = 0;
            foreach (var frame in TelemetryScenarioFactory.Create(
                         TelemetryScenario.OffTrackIncident1x))
            {
                await recorder.RecordFrameAsync(frame);
                var current = frame.IncidentCount ?? previous;
                if (current > previous)
                {
                    await recorder.MarkAsync("1x incident");
                }
                previous = current;
            }
            await recorder.StopAsync();

            var report = await CalibrationAnalyzer.AnalyzeAsync(path, new AppSettings());
            Equal(1, report.MarkerCount);
            Equal(1, report.MatchedCount);
            Equal(0, report.Recommendations.Count);
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

    private static List<DetectedHapticEvent> Detect(
        TelemetryScenario scenario,
        AppSettings? settings = null)
    {
        var pipeline = new HapticDetectionPipeline();
        settings ??= new AppSettings();
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

    private static List<DetectedHapticEvent> DetectCandidates(
        TelemetryScenario scenario,
        AppSettings? settings = null)
    {
        var pipeline = new HapticDetectionPipeline();
        settings ??= new AppSettings();
        var events = new List<DetectedHapticEvent>();
        foreach (var frame in TelemetryScenarioFactory.Create(scenario))
        {
            events.AddRange(pipeline.Process(frame, settings).Candidates);
        }
        return events;
    }

    private static DetectedHapticEvent IncidentEvent(
        HapticEventKind kind,
        int points,
        IncidentType type,
        bool relatedPhysicalEvent = false) =>
        new(
            DateTimeOffset.UtcNow,
            kind,
            points >= 4 ? EventSeverity.Strong : EventSeverity.Medium,
            points,
            50,
            ImpactDirection.NotApplicable,
            "test incident",
            new ProcessedTelemetry())
        {
            IncidentPoints = points,
            IncidentType = type,
            HasRelatedPhysicalEvent = relatedPhysicalEvent
        };

    private static RumbleController Controller(SimulatedRumbleDevice device) =>
        new(
            device,
            new SafetySettings
            {
                MaximumContinuousRumbleMs = 250,
                MaximumEffectDurationMs = 500,
                MaximumCallsPerSecond = 40
            });

    private static AppSettings LegacySettings()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 1,
            ActiveProfile = "Padrão"
        };
        settings.Safety.MaximumContinuousRumbleMs = 300;
        settings.Safety.MaximumEffectDurationMs = 700;
        settings.Effects.LightImpact =
            new EffectPatternSettings { FrequencyHz = 12, DurationMs = 75 };
        settings.Effects.MediumImpact =
            new EffectPatternSettings { FrequencyHz = 18, DurationMs = 125 };
        settings.Effects.StrongImpact = new EffectPatternSettings
        {
            FrequencyHz = 24,
            DurationMs = 145,
            PulseCount = 1,
            GapMs = 40,
            TailFrequencyHz = 21,
            TailDurationMs = 80
        };
        settings.Effects.Rollover = new EffectPatternSettings
        {
            FrequencyHz = 22,
            DurationMs = 90,
            PulseCount = 2,
            GapMs = 45
        };
        settings.Effects.StrongKerb =
            new EffectPatternSettings { FrequencyHz = 13, DurationMs = 60 };
        settings.Effects.WheelDrop =
            new EffectPatternSettings { FrequencyHz = 15, DurationMs = 80 };
        settings.Effects.Landing = new EffectPatternSettings
        {
            FrequencyHz = 18,
            DurationMs = 60,
            GapMs = 30,
            TailFrequencyHz = 14,
            TailDurationMs = 50
        };
        settings.Effects.SevereCompression =
            new EffectPatternSettings { FrequencyHz = 20, DurationMs = 105 };
        return settings;
    }

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

    private static TelemetryContext PlayerContext() =>
        new()
        {
            SessionInfoUpdate = 1,
            DriverCarIdx = 2,
            CarId = 105,
            CarClassId = 42,
            CarPath = "porsche911rgt3",
            CarName = "Porsche 911 GT3 R",
            CarClass = "GT3",
            TrackId = 100,
            TrackName = "spa",
            TrackDisplayName = "Circuit de Spa-Francorchamps",
            TrackConfigName = "Grand Prix Pits"
        };

    private static bool IsCollision(DetectedHapticEvent detected) =>
        detected.Kind is HapticEventKind.LightImpact
            or HapticEventKind.MediumImpact
            or HapticEventKind.StrongImpact
            or HapticEventKind.RolloverImpact;

    private static bool IsIncident(DetectedHapticEvent detected) =>
        detected.Kind is HapticEventKind.Incident1x
            or HapticEventKind.Incident2x
            or HapticEventKind.Incident4x
            or HapticEventKind.IncidentOther;

    private static string Describe(IEnumerable<DetectedHapticEvent> events) =>
        "Events: " + string.Join(
            ", ",
            events.Select(x => $"{x.Kind}/{x.Direction}/{x.Score:F2}"));

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "psvr2-haptics-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void True(bool condition, string message = "Expected true.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message = "Expected false.") =>
        True(!condition, message);

    private static void Equal<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}; received {actual}.");
        }
    }

    private static void Near(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"Expected {expected} ± {tolerance}; received {actual}.");
        }
    }

    private static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} to be thrown.");
    }
}
