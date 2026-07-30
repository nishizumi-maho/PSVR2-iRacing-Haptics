# Changelog

## 1.0.0 — 2026-07-30

- added persistent, user-created profiles with create, duplicate, rename, delete
  and factory-reset operations;
- made detector thresholds, event switches, incident policy and rumble patterns
  independent for every profile;
- added optional automatic profile activation using car and track identity from
  the iRacing `SessionInfo` SDK block;
- added prioritized wildcard assignment rules for `CarPath`, display name,
  class, `TrackName` and track configuration;
- added exact incident-point events from changes in
  `PlayerCarMyIncidentCount`, including separately configurable 1x, 2x, 4x and
  other-point patterns;
- added best-effort off-track, loss-of-control, contact, rollover and unknown
  incident classifications, each with independent output switches and optional
  type-based rumble patterns;
- added duplicate protection so a physical impact and its related incident
  notification do not rumble twice unless explicitly requested;
- expanded the calibration recorder with incident markers, human-reaction-time
  matching, per-marker details and bounded threshold recommendations;
- fixed the iRacing shared-memory offsets used to read the `SessionInfo` update,
  length and data location;
- expanded the simulator with 1x off-track, 2x loss-of-control and 4x contact
  incident scenarios;
- added Windows GitHub Actions validation, portable packaging, SHA-256 output
  and automatic GitHub release publishing;
- expanded architecture, telemetry, calibration, hardware-test and contribution
  documentation;
- expanded automated coverage to profile CRUD/migration, rule matching, SDK
  identity parsing, incident detection/policy/mapping and calibration advice.

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
