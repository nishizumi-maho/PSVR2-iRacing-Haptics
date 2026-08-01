# Contributing

Thank you for improving PSVR2 iRacing Haptics. This project sits between
real-time simulator telemetry and physical hardware, so correctness and safe
shutdown behavior take priority over feature count.

## Development setup

- Windows 10/11 x64;
- .NET 8 SDK;
- PowerShell 7 or Windows PowerShell;
- Inno Setup 6.3 or later, when building the installer locally;
- Git;
- PSVR2 Toolkit, SteamVR, iRacing and a headset only for optional hardware
  validation.

Clone the repository and run:

```powershell
.\build.ps1
```

That command restores dependencies, builds the complete solution, runs the
test executables, publishes a self-contained `win-x64` application, creates the
portable ZIP and writes its SHA-256 file. To compile and smoke-test the
installer from that publication, run:

```powershell
.\build-installer.ps1
```

The installer build performs a silent install and uninstall under a unique
temporary path, checks the deployed executable and confirms that installed
mode does not contain `portable.mode`.

`global.json` selects the tested .NET 8.0.423 SDK feature band. Install that
SDK (or a later 8.0.4xx patch) even if a newer major SDK is already present.

For a faster source run:

```powershell
.\run.ps1 -Simulator
```

The simulator path must remain usable without iRacing, PSVR2 Toolkit or a
headset.

## Understand the boundaries

- `PSVR2iRacingHaptics.Core` owns normalized models, signal processing,
  detectors, event policy, profiles, effects, controller safety, recording and
  replay, custom triggers and calibration services. It must not depend on
  WinForms or native DLL loading.
- `PSVR2iRacingHaptics.Infrastructure` owns the iRacing shared-memory reader and
  PSVR2 Toolkit C API client.
- `PSVR2iRacingHaptics.App` owns lifecycle coordination and the English
  WinForms UI. Detector logic does not belong in `MainForm`.
- `PSVR2iRacingHaptics.Tests` is a dependency-free executable test harness so
  the portable build has no test-framework package dependency.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing cross-layer
behavior.

## Non-negotiable safety invariants

Every contribution must preserve:

1. `0 Hz` after normal effect completion;
2. `0 Hz` after cancellation, emergency stop, disable, source/driver loss,
   exception and application shutdown;
3. no blocking wait on the UI thread;
4. serialized native calls;
5. maximum continuous and total-effect durations;
6. call-rate limiting;
7. no DLL unload after a native call times out;
8. no automatic jailbreak, driver patching or direct USB access;
9. no bundled `psvr2_toolkit_capi.dll`;
10. normal startup when iRacing and the Toolkit are absent.

A change that can affect any item above needs a focused automated test and an
explicit note in the pull request.

## iRacing telemetry rules

Do not invent telemetry names. Verify every variable against the official
iRacing SDK headers or a documented SDK reference, then state its real type and
unit. Fast telemetry variables must be discovered through the variable-header
table; never hard-code a car-dependent row offset.

Car and circuit identity comes from the slowly changing `SessionInfo` YAML
block. Keep parsing tolerant and dependency-free unless a new dependency has a
clear distribution and security benefit. The fixed `irsdk_header` offsets are
covered by tests.

When the SDK does not expose the desired fact, name the result as an inference
and document its evidence. Incident **points** are exact counter deltas;
incident **types** are heuristic.

## Adding or changing an event

A complete event change normally includes:

1. normalized input fields in `TelemetryFrame`;
2. processing/diagnostics in `TelemetrySignalProcessor`;
3. a detector candidate and documented classification reason;
4. priority relative to existing candidates;
5. an independent output policy switch;
6. a default effect pattern and profile migration behavior;
7. UI controls and in-app explanation;
8. simulator coverage, including a nearby negative scenario;
9. detector, policy, mapper, priority and mandatory-OFF tests;
10. README, calibration and changelog updates.

Detection and haptic output must remain separate. A disabled effect should
still appear in diagnostics and recordings.

## Profiles and settings

Increase `AppSettings.CurrentSchemaVersion` when stored meaning changes or
migration logic is required. Loading old settings must preserve customized
values whenever their intent is unambiguous.

Profile-owned data:

- collision and vertical detector settings;
- event-category switches;
- incident policy;
- custom telemetry triggers;
- all rumble patterns.

Global data:

- master haptics switch;
- real/simulated device choice;
- safety limits;
- profile catalog, assignment rules and automatic-selection switch.

Recording/circular-buffer, physical-calibration, input and desktop-integration
settings are also global.

Rule matching must remain deterministic: priority, specificity, then rule name.

## Adding a custom-trigger signal

1. Confirm the source variable's real SDK type and unit.
2. Add or normalize the value in `TelemetryFrame`/the signal processor.
3. Append a `TelemetrySignal` member; do not reorder existing members.
4. Add its unit and plain-language meaning to `TelemetrySignalCatalog`.
5. Map it in `TelemetryTriggerEngine.ReadSignal`.
6. Ensure JSONL recording/replay preserves the source value.
7. Add positive, signed/absolute, missing-channel and replay tests as relevant.
8. Update `docs/CUSTOM_TRIGGERS.md`.

Rule evaluation must remain deterministic and bounded. Do not add arbitrary
code execution, reflection over settings, expressions that allocate per frame,
or a way to bypass `IsDriverInCar`, output policy or controller safety.

Changes to Additive/Replace/Gate semantics need focused tests for both candidate
emission and suppression. Replacement must remain scoped to one event kind.

## Style

- all code, UI, logs, tests and documentation are written in English;
- nullable reference types stay enabled;
- keep public behavior documented in plain language;
- prefer small, testable components over adding logic to `MainForm` or
  `AppCoordinator`;
- do not add warnings to a Release build;
- do not commit `bin`, `obj`, portable output, ZIPs, logs or user data.

## Testing

Run:

```powershell
dotnet build .\PSVR2iRacingHaptics.sln -c Release
dotnet run --project .\tests\PSVR2iRacingHaptics.Tests -c Release
```

Then run `.\build.ps1` before opening a pull request. GitHub Actions repeats the
full Windows x64 release path. Hardware changes also require the relevant part
of [docs/HARDWARE_TEST.md](docs/HARDWARE_TEST.md); state clearly what could and
could not be tested physically.

## Pull-request checklist

- [ ] Scope is explained and unrelated changes are excluded.
- [ ] Release build has zero errors and zero warnings.
- [ ] All automated tests pass.
- [ ] New positive behavior and likely false positives have tests.
- [ ] Mandatory OFF and priority behavior remain covered.
- [ ] Old settings load without losing intentional customization.
- [ ] UI, logs and documentation are English and explain limitations.
- [ ] No Toolkit binary, secret, log, recording or user settings are included.
- [ ] Physical validation status is stated honestly.

Keep pull requests focused enough to review the safety path end to end.
