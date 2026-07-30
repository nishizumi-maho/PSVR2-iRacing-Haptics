# Changelog

## 0.2.0 — 2026-07-29

- translated the complete app UI, runtime messages, logs, tests and
  documentation to English;
- added a dedicated Effects tab with a master haptics switch and independent
  output switches for light, medium and strong impacts, rollover, strong/light
  kerbs, landings, wheel drops and severe compression;
- kept detection, diagnostics and recordings active when a haptic category is
  disabled;
- preserved enabled/disabled event choices when applying a profile;
- added automatic migration from the Portuguese profile names used by 0.1.x;
- expanded the in-app calibration guide with a safe tuning order, small
  threshold increments and explanations of matched, missed and unmarked events;
- added readable display names for telemetry simulator scenarios;
- added event-policy, disabled-output and profile-name migration tests;
- 32 automated tests.

## 0.1.1 — 2026-07-29

- rebalanced effects so each event is easier to recognize without unnecessarily
  prolonging rumble;
- kept strong kerb and wheel drop as single-pulse effects;
- reserved tails for strong impacts and landings, and double pulses for
  rollover impacts;
- reduced default limits to 250 ms continuous and 550 ms per effect;
- migrated 0.1.0 default durations automatically while preserving customized
  values;
- made the landing's second pulse configurable in the UI;
- clarified the difference between simulated telemetry and the simulated
  rumble device;
- 29 automated tests.

## 0.1.0 — 2026-07-29

- first independent Windows x64 / .NET 8 release;
- dynamic PSVR2 Toolkit C API loading;
- manual test and simulated rumble device;
- direct iRacing shared-memory reader;
- collision, strong kerb, wheel drop, landing, compression and rollover
  detectors;
- filtering, jerk, hysteresis, cooldown and braking/wheel-lock protection;
- prioritized rumble queue with cancellation, limits and mandatory `0 Hz`;
- Brazilian Portuguese interface;
- Default-equivalent, Gentle-equivalent, Strong-equivalent and Custom-equivalent
  profiles;
- rotating logs, JSONL recording, replay and marker analysis;
- eleven telemetry scenarios;
- 25 automated tests;
- self-contained portable package.
