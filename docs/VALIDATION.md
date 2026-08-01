# Version 1.1.2 validation

Date: August 1, 2026.

## Reproducible environment

- GitHub-hosted Windows x64 runner;
- .NET SDK 8.0.423 selected by `global.json`;
- app target: `net8.0-windows`, runtime `win-x64`;
- Release, self-contained, untrimmed, non-single-file publication;
- Inno Setup 6.7.0 for the setup executable;
- the same `build.ps1` and `build-installer.ps1` entry points used by
  contributors and the release workflow.

The v1.1.2 pull-request checkpoint is
[workflow run 30713267519](https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics/actions/runs/30713267519).
The authoritative current run is always available from the repository's
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

The v1.1.2 checkpoint contained 480 portable entries. The portable ZIP was
72,425,789 bytes and its SHA-256 was:

```text
2cee087f9ec9de6a883a296df7e473aa7cb5b3281758d7dd36ba3be245ecc899
```

## Installer validation

The Inno Setup build produced a 51,363,302-byte setup executable. The workflow
then:

- installed it silently into a unique current-user temporary directory;
- confirmed that `PSVR2iRacingHaptics.exe`, README and the uninstaller exist;
- confirmed that installed mode does not contain `portable.mode`;
- confirmed that the setup is configured for current-user, non-admin mode;
- uninstalled it silently and received a successful exit code;
- wrote and independently verified its SHA-256 sidecar.

The setup SHA-256 for the pull-request checkpoint was:

```text
d1a1bf3f76fc304052ad5edb2d04cb38732d0c44cc059508bb4d7ab8ad1e8104
```

The setup bootstrap produced by Inno 6 is PE32, while its packaged application
is x86-64 and the script is gated to `x64compatible` systems with 64-bit install
mode. The installer AppId is fixed so later versions update the same installed
product.

The workflow artifact contains the setup, portable ZIP and both SHA-256
sidecars. Its GitHub artifact digest was
`sha256:67445a2d3731b5c3c6657b8b8b2b08bd289a2717029d61bea8c0e1bde94bbee3`.
The release workflow repeats the complete build after merge rather than
promoting this pull-request checkpoint, so the final release hashes may differ
and its attached sidecars are authoritative.

## Automated tests

```text
Result: 74/74 tests passed.
```

Windows application checks:

```text
Result: 3/3 Windows application checks passed.
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
- persistence of profile-owned custom telemetry triggers;
- raw and derived telemetry-signal lookup, including lateral acceleration;
- AND/OR condition groups, absolute/signed comparisons, missing-signal policy,
  hold time, cooldown and release/rearm behavior;
- additive, built-in replacement and built-in gate trigger modes;
- directional impact gates and per-trigger rumble-pattern overrides;
- prevention of custom haptics during live iRacing replay;
- offline replay statistics for custom triggers, including when live custom
  output is disabled;
- circular-buffer capture of the telemetry preceding a marker;
- validated profile-package round trips without machine-global settings;
- bounded physical comfort calibration and explicit no-range results;
- redaction and reviewed contents of diagnostic support bundles;
- complete, unique default keyboard/steering-wheel input actions;
- default-enabled release checks for new settings, semantic version comparison
  and rejection of non-official release URLs;
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
- verify keyboard and steering-wheel bindings with the user's actual devices;
- run the physical calibration assistant and confirm that its suggested range
  remains clear and comfortable for that headset and wearer;
- evaluate coexistence with any other C API rumble client.

The executable is not Authenticode-signed. The release provides a SHA-256
sidecar for integrity verification, not publisher authentication.

The reproducible physical procedure and evidence checklist are in
[HARDWARE_TEST.md](HARDWARE_TEST.md). No physical validation is implied by the
automated test result.
