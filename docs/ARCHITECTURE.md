# Architecture

## Layers

### Core

- `TelemetrySignalProcessor`: fast/slow EMA, deltas, jerk and context;
- `ImpactDetector`: collisions, direction, severity and rollover;
- `VerticalImpactDetector`: kerbs, wheel drops, landings and compression;
- `IncidentDetector`: exact point deltas and best-effort type classification;
- `TelemetryTriggerEngine`: stateful profile rules over raw/derived telemetry;
- `HapticDetectionPipeline`: runs detectors, applies custom rules and selects
  by priority;
- `HapticEventPolicy`: decides whether a detected category may produce rumble;
- `RumbleEffectMapper`: converts events into pulse patterns;
- `RumbleController`: serialization, preemption, cancellation, limits and OFF;
- `SettingsService`, `ProfileCatalog`, `ProfileRuleMatcher`,
  `RotatingFileLogger`;
- `TelemetryRecorder`, `TelemetryCircularBuffer`, `TelemetryReplayClient`,
  `CalibrationAnalyzer`, `PhysicalCalibrationSession`;
- `ProfilePackageService`, `DiagnosticBundleService`;
- `TelemetrySimulator` and `SimulatedRumbleDevice`.

### Infrastructure

- `Psvr2ToolkitClient`: discovery, DLL loading, exports, state and native calls;
- `IRacingSharedMemoryClient`: shared memory, reconnection, `SessionInfo` and
  normalization;
- `IRacingSessionInfoParser`: dependency-free extraction of player-car and
  circuit identity.

### App

- `AppCoordinator`: application lifecycle and integration, with no detector
  logic in the UI;
- `GlobalInputService`: registered keyboard shortcuts and joystick-button edge
  polling;
- `ApplicationIntegrationService`: opt-in update check and current-user
  startup entry;
- `MainForm` plus focused controls: English WinForms interface.

## Data flow

```mermaid
flowchart TD
    A["iRacing or simulator"] --> B["Normalized snapshot"]
    B --> C["Filtering and jerk"]
    C --> D["Built-in detectors"]
    D --> E["Custom trigger engine"]
    E --> F["Candidate priority"]
    F --> G{"Output policy enabled?"}
    G -->|"Yes"| H["Effect mapping"]
    G -->|"No"| I["Diagnostics and recording only"]
    H --> J["Safe controller"]
    J --> K["Real C API or fake device"]
```

Category switches are deliberately applied after detection. This allows the
Diagnostics tab and JSONL calibration to observe an event without sending
rumble to the headset.

The trigger engine runs after the built-in detectors so `GateBuiltIn` can
require a same-frame built-in candidate and `ReplaceBuiltIn` can suppress only
its target kind. Additive rules retain the candidate. All rule state is reset
on invalid telemetry, source/profile changes and pipeline reset. Live iRacing
replay is output-ineligible; the analyzer explicitly opts recorded replay
frames into offline evaluation.

## Profile flow

```mermaid
flowchart TD
    A["iRacing SessionInfo"] --> B["Car and track identity"]
    B --> C{"Automatic selection enabled?"}
    C -->|"No"| D["Keep active profile"]
    C -->|"Yes"| E["Match enabled rules"]
    E --> D
    E --> F["Activate matched profile"]
```

Rules are deterministic: priority, specificity, then rule name. A profile owns
detector settings, category switches, incident policy, custom trigger rules and
effect patterns. The real/simulated device choice, global haptics switch,
recording/input/application behavior and safety limits remain outside profiles.
A profile change resets detector/trigger history and stops any active effect so
samples from two calibrations cannot be mixed.

## Calibration and capture flow

```mermaid
flowchart TD
    A["Live SDK or iRacing replay"] --> B["JSONL or circular buffer"]
    B --> C["Offline pipeline dry run"]
    C --> D["Marker comparison"]
    C --> E["Per-condition statistics"]
    D --> F["Reviewed built-in adjustment"]
    E --> G["Manual trigger adjustment"]
```

The circular buffer owns a bounded in-memory queue and writes only on explicit
capture. `CalibrationAnalyzer` reconstructs the current pipeline and never
touches a rumble device. Playing JSONL through `AppCoordinator` is a distinct,
explicit operation that can exercise the selected rumble device.

`SessionInfo` changes slowly and is parsed only when its SDK update counter
changes. Fast telemetry rows continue to use the dynamic variable-header index.

## Incident flow

The signal processor compares successive non-null
`PlayerCarMyIncidentCount` values. It never generates a delta on initial
connection, driver entry, reset or a counter decrease. A positive delta becomes
1x, 2x, 4x or other. `IncidentDetector` retains a bounded evidence queue and
classifies the likely type from:

- `PlayerTrackSurface`;
- recent physical detector candidates;
- collision score;
- angular rate, roll and pitch.

The point value is SDK data; the type is an inference. Policy then applies the
point gate, type gate and duplicate protection. Mapping selects either the
point waveform or the type waveform according to the active profile.

## Failure and OFF flow

```mermaid
flowchart TD
    A["Active effect"] --> B{"Event"}
    B -->|"Normal completion"| C["Send 0 Hz"]
    B -->|"Cancellation or higher priority"| C
    B -->|"Driver exits car or iRacing is lost"| C
    B -->|"Inactive Toolkit driver or exception"| C
    B -->|"Shutdown or emergency stop"| C
    C --> D["Log result"]
```

A native timeout is special: managed code cannot safely terminate a C function
that is stuck. After a timeout, the client blocks new commands and keeps the
DLL loaded until process exit instead of unloading it beneath a potentially
active native function.

## Extension points

New rumble devices implement `IHmdRumbleDevice`; new telemetry sources
implement `ITelemetryClient`. A new detector should emit
`DetectedHapticEvent`, register a post-detection policy switch, add a mapper
pattern and include simulator plus safety tests. See `CONTRIBUTING.md` for the
required checklist.

A new custom-rule signal is added to `TelemetrySignal`, documented in
`TelemetrySignalCatalog` and mapped in `TelemetryTriggerEngine.ReadSignal`.
Preserve enum values by appending new members, because serialized profile rules
store those names and may also be read from older numeric JSON.
