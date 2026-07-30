# PSVR2 iRacing Haptics

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
- severe vertical compression.

Disabling an event prevents its rumble pattern from being sent. Detection,
diagnostics and recording remain active, so a disabled effect can still be
calibrated safely. Applying the Default, Gentle or Strong profile preserves the
user's enabled/disabled choices.

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
evidence.

## Default rumble patterns

- light impact: 12 Hz for 120 ms;
- medium impact: 18 Hz for 160 ms;
- strong impact: 24 Hz for 200 ms, 55 ms pause, then 21 Hz for 100 ms;
- rollover: two 22 Hz pulses for 120 ms, separated by 65 ms;
- strong kerb: 14 Hz for 110 ms;
- wheel drop: 16 Hz for 130 ms;
- landing: 19 Hz for 140 ms, 60 ms pause, then 15 Hz for 110 ms;
- severe compression: 20 Hz for 150 ms.

Frequency is not treated as physical intensity. Effects are distinguished by
frequency, duration, pulse count, spacing and optional tail.

The defaults favor recognizable events without prolonged vibration. Ordinary
events use one pulse; tails are reserved for strong impacts and landings, while
rollover uses a double pulse. The default safety limits are 250 ms of continuous
rumble and 550 ms for a complete effect. Light kerbs remain disabled and strong
kerbs respect cooldown.

Priority order: strong impact, rollover, medium impact, landing, severe
compression, light impact, wheel drop and kerb. A stronger effect can replace a
weaker one; a weaker effect cannot interrupt a stronger one.

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
   `Mark strong kerb` or `Mark landing` immediately after it happens.
7. Stop recording and click **Compare markers**.
8. For a missed collision, lower only its matching collision threshold by
   0.10–0.20. For a missed kerb or landing, lower the matching vertical
   threshold by 0.10–0.20.
9. If normal driving produces an event, raise the corresponding threshold by
   the same amount.
10. Change one value at a time, save, and replay the same JSONL. Watch
    `Collision score` and `Vertical score` under **Diagnostics**.
11. Once detection is reliable, tune frequency and duration for comfort.
    Cooldown only controls how soon the same event family can repeat.

Calibration results mean:

- **Matched** — the current detector found the expected category within 500 ms
  of a marker;
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
collision, rollover and connection loss.

## Profiles

- **Default** — balanced starting point;
- **Gentle** — higher thresholds and milder rumble;
- **Strong** — lower thresholds and stronger rumble;
- **Custom** — values edited in the UI.

Existing Portuguese profile names from versions 0.1.x are migrated
automatically.

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

The .NET 8 x64 SDK is required.

```powershell
.\build.ps1
```

The script restores, builds, runs the test executable, publishes a
self-contained `win-x64` app and creates:

```text
build\PSVR2-iRacing-Haptics-v0.2.0-win-x64-portable.zip
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

## License and trademarks

This project is licensed under the MIT License. PSVR2 Toolkit is an external
project with its own terms. PlayStation, PlayStation VR2, Sony, iRacing and
SteamVR are trademarks of their respective owners. This project is unofficial
and is not affiliated with them.
