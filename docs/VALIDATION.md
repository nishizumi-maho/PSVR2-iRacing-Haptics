# Version 1.0.0 validation

Date: July 30, 2026.

## Reproducible environment

- GitHub-hosted Windows x64 runner;
- .NET SDK 8.0.423 selected by `global.json`;
- app target: `net8.0-windows`, runtime `win-x64`;
- Release, self-contained, untrimmed, non-single-file publication;
- the same `build.ps1` entry point used by contributors and the release
  workflow.

The authoritative current run is available from the repository's
[Validate and package workflow](https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics/actions/workflows/ci.yml).

## Build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Warnings are treated as errors for every project.

The packaging script also verifies:

- the app executable exists and has an x86-64 PE machine header;
- `portable.mode`, README and the executable are present in the ZIP;
- every ZIP entry can be opened and decompressed;
- neither `psvr2_toolkit_capi.dll` nor a PSVR2 Toolkit executable is bundled;
- a SHA-256 sidecar is written for the exact portable ZIP.

## Automated tests

```text
Result: 53/53 tests passed.
```

Coverage includes:

- settings defaults, validation, round-trip persistence and legacy migrations;
- factory/user profile creation, duplication, rename, delete and independent
  configuration;
- automatic rule exact/wildcard matching, opt-in behavior, priority,
  specificity and persistence;
- iRacing `SessionInfo` parsing and official header offsets;
- filtering, jerk, warmup and invalid/reset telemetry;
- rejection of normal acceleration, hard braking, wheel lock and light kerbs;
- lateral/front/strong impacts, rollover, strong kerb, wheel drop, landing and
  severe compression;
- exact 1x/2x/4x incident deltas, off-track/loss-of-control/contact
  classification, counter decreases and physical duplicate protection;
- point-based and inferred-type incident waveforms;
- independent event switches with detection retained while output is disabled;
- effect safety limits, priority, preemption and rejection;
- mandatory `0 Hz` after completion, cancellation and emergency stop;
- unavailable Toolkit/iRacing behavior;
- JSONL context/marker round trip, replay matching and bounded calibration
  recommendations.

## Validation limits

The hosted runner has no PSVR2, PSVR2 Toolkit driver or iRacing installation.
The following remain real-hardware responsibilities:

- confirm that `0` stops the motor with the installed headset firmware;
- assess perceived sensation and the physical meaning of values called Hz;
- validate Toolkit behavior if its driver disappears during a native call;
- tune thresholds across real car/track combinations;
- evaluate coexistence with any other C API rumble client.

The reproducible physical procedure and evidence checklist are in
[HARDWARE_TEST.md](HARDWARE_TEST.md). No physical validation is implied by the
automated test result.
