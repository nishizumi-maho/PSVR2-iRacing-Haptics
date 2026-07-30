# PSVR2 iRacing Haptics

[![Validate and package](https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics/actions/workflows/ci.yml/badge.svg)](https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/nishizumi-maho/PSVR2-iRacing-Haptics)](https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics/releases/latest)

An independent Windows companion app that converts iRacing telemetry into
PlayStation VR2 headset rumble patterns through the
[PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit) C API.

The app does **not bundle, redistribute, modify or replace** PSVR2 Toolkit. It
does not patch the driver, access USB directly or perform a jailbreak. The C
API DLL remains part of the Toolkit installation and is loaded from the path
published by the Toolkit itself.

## Important warning

In the Toolkit version reviewed for this project, headset rumble is marked as a
feature that requires a jailbroken headset. The upstream project warns that the
procedure can damage or even permanently disable the headset. This app does not
perform that procedure or make it safer. Read the upstream documentation and
continue at your own risk.

Do not run another headset-rumble client at the same time. The C API provides
eight client slots, but HMD rumble is global and has no arbitration or priority
between applications.

## Requirements

- Windows 10 or Windows 11 x64
- PlayStation VR2 connected to the PC
- PSVR2 Toolkit installed, configured and running
- SteamVR running with the Toolkit driver active
- the jailbreak/configuration required by the Toolkit for headset rumble
- iRacing, only for live telemetry
- no administrator privileges
- no Python installation

The portable ZIP is self-contained and does not require a separate .NET
installation. Because it includes `portable.mode`, settings, logs and
recordings are stored in the `data` folder beside the executable. If that file
is removed, data is stored under `%LOCALAPPDATA%\PSVR2iRacingHaptics`.

## Quick start

1. Start PSVR2 Toolkit and SteamVR normally.
2. Confirm that the Toolkit driver is active.
3. Extract the entire portable ZIP to a normal user folder.
4. Run `PSVR2iRacingHaptics.exe`.
5. Open **Manual test**, select `PSVR2 Toolkit (real hardware)` and begin with
   12 Hz for 120 ms.
6. Use **STOP ALL RUMBLE NOW** if anything behaves unexpectedly.
7. Open **Effects** and enable only the event categories you want to feel.
8. Start iRacing and enter the car. Status should show `iRacing connected` and
   `Driver in car`.
9. Keep the **Default** profile until you have a clean calibration recording.

The manual test works without iRacing. The telemetry simulator can drive either
the real headset or the fake rumble device:

- select `PSVR2 Toolkit (real hardware)` on **Manual test** to feel scenarios;
- select `Simulated rumble device` to validate commands and logs without a
  headset;
- enable `Use simulated telemetry` on **Calibration & simulator** in either
  case.

## Choosing enabled effects

The **Effects** tab contains a master haptics switch and independent switches
for:

- all collision haptics;
- light, medium and strong impacts;
- rollover impacts;
- strong kerbs;
- light kerbs, disabled by default;
- landings;
- wheel drops;
- severe vertical compression;
- incident-point notifications as a separate master category;
- 1x, 2x, 4x and other incident-point changes;
- inferred off-track, loss-of-control, contact, rollover and unknown incident
  types.

Disabling an event prevents its rumble pattern from being sent. Detection,
diagnostics and recording remain active, so a disabled effect can still be
calibrated safely. Event choices are stored in the active profile, so different
cars can intentionally enable different effects. The global master haptics
switch, selected device and safety limits are not changed by a profile.

## Confirmed PSVR2 Toolkit behavior

The analysis targets commit
[`9e24e6ef475660481e8b46366aaa3cb24d0b4fde`](https://github.com/BnuuySolutions/PSVR2Toolkit/commit/9e24e6ef475660481e8b46366aaa3cb24d0b4fde),
the state of `main` reviewed on July 29, 2026.

| Question | Behavior confirmed in source |
| --- | --- |
| DLL | `psvr2_toolkit_capi.dll` |
| Discovery | `%TEMP%\psvr2tk_capi_path.txt`, written by the driver with the C API directory |
| Official loader | Reads the first line, appends the DLL name and calls `LoadLibraryExA(..., LOAD_WITH_ALTERED_SEARCH_PATH)` |
| Initialization | `psvr2_toolkit_init()` returns `0` (OK), `-1` (driver inactive) or `-2` (no free slot) |
| Clients | Eight slots; `deinit()` only releases the slot |
| Driver state | `psvr2_toolkit_get_driver_active()` returns `bool` |
| Rumble | `void psvr2_toolkit_set_hmd_rumble(uint8_t rumbleHz)` |
| Range | The function accepts any `uint8_t` and does not clamp it. The official test UI limits input to `0–25`; this app uses the same conservative range |
| Driver command | `HeadsetRumbleSet` forwards one byte through control command `0x08` |
| Separate intensity | Not available |
| Built-in duration | Not available; there is no visible auto-off timer |
| Return value | The send function is `void` and does not confirm delivery |
| Inactive driver | The manager discards the command when it detects an inactive driver |
| Headset presence | Not exposed separately by the C API |
| C API version | No version export exists |

The parameter and the official UI call the byte “Hz,” and the driver forwards
it without conversion. The public source does not measure or guarantee that
the perceived physical frequency exactly matches the requested number.

The official test can send `0`, and no duration or auto-off behavior is visible
in the driver path. This is consistent with `0 = off`, but the final meaning of
USB command `0x08` is implemented in headset firmware and is not demonstrated
in the repository. For safety, this app sends `0` after every pulse and
requires this behavior to be confirmed during the first hardware test.

The driver command thread runs at roughly 10 ms intervals, but the Toolkit does
not document a safe maximum call rate. This app defaults to **20 non-zero calls
per second**. Emergency `0` commands bypass that limiter.

See [docs/PSVR2_TOOLKIT_ANALYSIS.md](docs/PSVR2_TOOLKIT_ANALYSIS.md) for file
references and the complete analysis.

## iRacing telemetry

The integration reads `Local\IRSDKMemMapFileName` directly and waits on
`Local\IRSDKDataValidEvent`. Variable headers are discovered at runtime; no
car-variable offset is hard-coded.

Signals used include:

- `IsOnTrack`, `IsOnTrackCar`, `IsInGarage`, `IsReplayPlaying`;
- `Speed`, `LatAccel`, `LongAccel`, `VertAccel`;
- `VelocityX`, `VelocityY`, `VelocityZ`;
- `Yaw`, `Pitch`, `Roll`, `YawRate`, `PitchRate`, `RollRate`;
- `Brake`, `Throttle`, `Gear`, `RPM`;
- `PlayerCarMyIncidentCount`;
- `PlayerTrackSurface`, `PlayerTrackSurfaceMaterial`;
- `LF/RF/LR/RRspeed`;
- `LF/RF/LR/RRshockDefl`, `LF/RF/LR/RRshockVel`;
- `TireLF/RF/LR/RR_RumblePitch`.

Suspension, individual wheel speed and rumble-pitch channels depend on the car
and session. The app detects missing channels and uses fallback signals. The
iRacing SDK does not provide a single reliable collision event, individual
wheel-contact bit or damage impulse, so classification is heuristic.

The detector calculates a slow baseline, smoothed acceleration, deviation from
the baseline, jerk on all three axes, deceleration, angular motion, suspension
activity/asymmetry and temporal context. Incident count is only supporting
evidence for physical-impact classification; a separate incident detector
handles exact counter changes.

The app also reads the slowly changing `SessionInfo` YAML block from the same
shared-memory mapping. It extracts the current driver's `CarPath`, display
name, class and IDs plus the circuit's `TrackName`, display name, ID and
configuration. These values are used only for display and optional profile
assignment rules.

## Incident haptics

`PlayerCarMyIncidentCount` is a cumulative integer. A positive counter delta is
therefore an exact point change, and the app exposes separate events for 1x,
2x, 4x and any other delta.

The SDK does **not** expose an official incident cause beside that counter. The
app labels an incident as off track, loss of control, contact, rollover or
unknown by examining the preceding track-location, acceleration, rotation and
physical-event evidence. These type labels are explicitly best-effort.

Each profile controls:

- the incident master switch;
- point-value gates and inferred-type gates;
- cooldown and evidence-window length;
- duplicate suppression for related physical impacts;
- whether the waveform is selected by exact point value or inferred type;
- frequency, duration, pulse count and gap for every point/type waveform.

Both the point gate and inferred-type gate must allow the event. By default,
incident rumble is off and duplicate suppression is on; diagnostics and
recording still receive every detected counter increase.

## Default rumble patterns

- light impact: 12 Hz for 120 ms;
- medium impact: 18 Hz for 160 ms;
- strong impact: 24 Hz for 200 ms, 55 ms pause, then 21 Hz for 100 ms;
- rollover: two 22 Hz pulses for 120 ms, separated by 65 ms;
- strong kerb: 14 Hz for 110 ms;
- wheel drop: 16 Hz for 130 ms;
- landing: 19 Hz for 140 ms, 60 ms pause, then 15 Hz for 110 ms;
- severe compression: 20 Hz for 150 ms;
- 1x incident: 12 Hz for 105 ms;
- 2x incident: two 16 Hz pulses for 115 ms, separated by 65 ms;
- 4x incident: 20 Hz for 150 ms, then a 16 Hz tail for 90 ms;
- other incident delta: 14 Hz for 120 ms.

Frequency is not treated as physical intensity. Effects are distinguished by
frequency, duration, pulse count, spacing and optional tail.

The defaults favor recognizable events without prolonged vibration. Ordinary
events use one pulse; tails are reserved for strong impacts and landings, while
rollover uses a double pulse. The default safety limits are 250 ms of continuous
rumble and 550 ms for a complete effect. Light kerbs remain disabled and strong
kerbs respect cooldown.

Priority order favors strong physical impacts and rollover, followed by medium
impacts, 4x incidents, landing/compression, light impacts and lower-point
incidents/vertical events. A stronger effect can replace a weaker one; a weaker
effect cannot interrupt a stronger one. When duplicate protection is disabled,
a related incident notification is queued after its physical effect instead of
interrupting it.

## Calibration

Calibration has two separate stages:

- **Detection controls** — sensitivity and thresholds decide whether an event
  exists.
- **Feel controls** — frequency, duration, pulse count and gap decide how a
  detected event feels.

Do not compensate for missed detections by making rumble longer. Calibrate
detection first, then tune the physical sensation.

Recommended workflow:

1. On **Effects**, enable only the categories you want.
2. On **Manual test**, find a comfortable frequency and duration before using
   live telemetry.
3. Apply the **Default** profile.
4. In an iRacing solo test session, drive two or three clean laps using normal
   braking and ordinary kerbs. The app should remain quiet.
5. Open **Calibration & simulator** and start a JSONL recording.
6. Reproduce one clear event at a time. Click `Mark impact`,
   `Mark strong kerb`, `Mark landing`, `Mark 1x`, `Mark 2x` or `Mark 4x`
   immediately after it happens.
7. Stop recording and click **Compare markers**.
8. Review the bounded recommendation. A controlled miss proposes a threshold
   8% below the observed peak; a marked false positive proposes an 8% margin
   above its score. Conflicting evidence is never auto-applied.
9. Incident markers validate counter handling and classification but do not
   change physical thresholds automatically.
10. Change one value at a time, save, and replay the same JSONL. Watch
    `Collision score` and `Vertical score` under **Diagnostics**.
11. Once detection is reliable, tune frequency and duration for comfort.
    Cooldown only controls how soon the same event family can repeat.

Calibration results mean:

- **Matched** — the current detector found the expected category from 2,000 ms
  before the marker through 250 ms after it, allowing for reaction time;
- **Missed** — a marker had no compatible detection, often because its
  threshold is too high;
- **Unmarked detections** — the detector found events that were not marked,
  usually false positives or events omitted during marking.

Each JSONL line stores the complete telemetry snapshot required by the current
algorithm, the detection made at recording time and an optional marker. A
recording can be replayed after changing settings; entering the simulator again
is not required.

Built-in scenarios cover a parked car, normal acceleration, hard braking, light
and strong kerbs, wheel drop, landing, side impact, front impact, strong
collision, rollover, 1x off track, 2x loss of control, 4x contact and connection
loss.

## Profiles and automatic activation

The four resettable factory profiles are:

- **Default** — balanced starting point;
- **Gentle** — higher thresholds and milder rumble;
- **Strong** — lower thresholds and more pronounced rumble;
- **Custom** — editable general-purpose starting point.

Use **Profiles** to create a profile from the current setup, duplicate any
profile, rename/delete user profiles, or reset a factory profile. Each profile
stores collision and vertical thresholds, individual event switches, incident
policy and every event waveform. Factory profiles cannot be renamed or deleted.

Automatic activation is optional. A rule targets one profile and may contain
any combination of:

- `CarPath` (the most stable car identifier);
- car display name;
- car class;
- `TrackName` (the most stable track identifier);
- track configuration.

Every populated field is an AND condition. `*` matches any sequence and `?`
matches one character, both case-insensitively. Higher priority wins; an equal
priority prefers the more specific rule and then the rule name for deterministic
results. If no rule matches, the current profile stays active.

Existing 0.1.x/0.2.0 settings and Portuguese factory-profile names are migrated
without discarding customized detector/effect values.

See [docs/PROFILES_AND_INCIDENTS.md](docs/PROFILES_AND_INCIDENTS.md) for rule
examples, incident pattern modes, precedence and troubleshooting.

## Safety behavior

- permanent emergency-stop button;
- `0 Hz` after every pulse, cancellation, exception, disable and shutdown;
- `0 Hz` when telemetry is lost, the driver exits the car or the Toolkit driver
  becomes inactive;
- continuous-duration and total-effect limits;
- serialized device calls;
- call-rate limit;
- native-call timeout;
- new native calls blocked after a stuck call;
- no native operation on the UI thread;
- light kerbs disabled by default;
- normal startup without iRacing or PSVR2 Toolkit.

## Logs

Portable mode: `data\logs\psvr2-iracing-haptics.log`.

Logs include app version, available DLL path/version, initialization result,
driver state, iRacing connection and in-car changes, detection values and
reasons, suppressed event categories, rumble patterns, cancellations, errors
and `Rumble: OFF`. Logs rotate at 5 MiB with four retained files.

## Build

The .NET 8 x64 SDK is required. `global.json` pins the tested 8.0.423 feature
band and permits later patches in that band.

```powershell
.\build.ps1
```

The script restores, builds, runs the test executable, publishes a
self-contained `win-x64` app and creates:

```text
build\PSVR2-iRacing-Haptics-v1.0.0-win-x64-portable.zip
```

Run from source on Windows:

```powershell
.\run.ps1
```

Start with the fake rumble device:

```powershell
.\run.ps1 -Simulator
```

Run tests:

```powershell
dotnet run --project .\tests\PSVR2iRacingHaptics.Tests -c Release
```

## Hardware validation

Follow [docs/HARDWARE_TEST.md](docs/HARDWARE_TEST.md). The package is compiled
and validated with simulators, but the build environment does not contain a
PSVR2 or iRacing installation. Physical rumble, real headset presence and
car-specific thresholds still require Windows hardware validation. Exact build
and test results are recorded in [docs/VALIDATION.md](docs/VALIDATION.md).

GitHub Actions runs the same release script on Windows for every pull request.
A successful merge to `main` creates the versioned GitHub release only if that
version does not already exist.

## Project structure

```text
src/PSVR2iRacingHaptics.Core
src/PSVR2iRacingHaptics.Infrastructure
src/PSVR2iRacingHaptics.App
tests/PSVR2iRacingHaptics.Tests
docs
build
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for component boundaries and
failure flow.

## Contributing

Start with [CONTRIBUTING.md](CONTRIBUTING.md). It explains the development
environment, safety invariants, code boundaries, how to add verified iRacing
signals/events, required tests and the pull-request checklist. Please do not
invent telemetry variable names or weaken any mandatory `0 Hz` shutdown path.

## License and trademarks

This project is licensed under the MIT License. PSVR2 Toolkit is an external
project with its own terms. PlayStation, PlayStation VR2, Sony, iRacing and
SteamVR are trademarks of their respective owners. This project is unofficial
and is not affiliated with them.
