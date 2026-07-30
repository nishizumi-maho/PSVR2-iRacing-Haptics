# Version 0.2.0 validation

Date: July 29, 2026.

## Environment

- SDK: .NET 8.0.423;
- app target: `net8.0-windows`, `win-x64`;
- publication: self-contained, untrimmed and not single-file;
- build environment: Linux container;
- unavailable physical hardware: PSVR2, Toolkit driver and iRacing.

## Build

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The published executable is a Windows x86-64 GUI PE. The package is inspected
to confirm that it does not contain `psvr2_toolkit_capi.dll` or a PSVR2 Toolkit
executable.

## Automated tests

```text
Result: 32/32 tests passed.
```

Coverage includes settings persistence, validation and migrations; filtering
and jerk; rejection of normal acceleration and hard braking; light/strong
kerbs; lateral, front and strong collisions; rollover; landing; wheel drop;
invalid telemetry; effect mapping; priority and preemption; mandatory `0 Hz`;
cancellation; emergency stop; unavailable devices; recording/replay/calibration;
safe absence of the Toolkit and iRacing; per-event output policy; detection
while an output category is disabled; and Portuguese profile-name migration.

## Validation limits

The WinForms UI cannot be launched in this Linux environment, and no physical
rumble was commanded. The following still require Windows and real hardware:

- confirmation that `0` stops the motor with the installed headset firmware;
- perceived sensation and physical correspondence of values called Hz;
- separate detection of a connected headset;
- behavior during real driver loss;
- car/track-specific threshold tuning;
- coexistence with other C API clients.

The reproducible procedure is in `docs/HARDWARE_TEST.md`.
