# Telemetry and calibration

## IRSDK layout

The reader follows the dynamic IRSDK layout:

- header version 2;
- up to four `varBuf` entries;
- 144-byte `varHeader`;
- char, bool, int, bitfield, float and double types;
- selection of the buffer with the greatest `tickCount`;
- complete row copy followed by verification that the tick did not change.

There are no fixed offsets for `LatAccel` or any other variable. The variable
index is rebuilt whenever the client connects.

The fixed header fields required for the slowly changing YAML block follow the
official `irsdk_header` layout:

| Offset | Field |
| ---: | --- |
| 12 | `sessionInfoUpdate` |
| 16 | `sessionInfoLen` |
| 20 | `sessionInfoOffset` |

The reader copies at most 8 MiB, verifies that the update counter did not change
during the copy, and parses only the car/track fields used by the app.

## Variables and types

| Variable | Type | Use |
| --- | --- | --- |
| `IsOnTrack` | bool | driver in the car with active physics |
| `IsOnTrackCar` | bool | player's car has active physics |
| `IsInGarage` | bool | reject loading/garage data |
| `IsReplayPlaying` | bool | prevent unintended effects during iRacing replay |
| `Speed` | float, m/s | minimum speed and speed loss |
| `LatAccel` | float, m/s² | lateral impulse |
| `LongAccel` | float, m/s² | longitudinal impact/braking |
| `VertAccel` | float, m/s² | vertical impact; includes gravity |
| `VelocityX/Y/Z` | float, m/s | movement and airborne evidence |
| `Yaw/Pitch/Roll` | float, rad | orientation/rollover |
| `YawRate/PitchRate/RollRate` | float, rad/s | rapid rotation |
| `Brake`, `Throttle` | float, 0–1 | reject normal braking |
| `PlayerCarMyIncidentCount` | int | exact cumulative point count and impact evidence |
| `PlayerTrackSurface` | enum/int | off-track evidence |
| `PlayerTrackSurfaceMaterial` | int | rumble material when available |
| `LF/RF/LR/RRspeed` | float, m/s | wheel-lock evidence |
| `LF/RF/LR/RRshockVel` | float, m/s | compression and asymmetry |
| `LF/RF/LR/RRshockDefl` | float, m | extension/compression when available |
| `TireLF/RF/LR/RR_RumblePitch` | float, Hz | rumble-strip presence |

Per-wheel ride height is dependent telemetry and has historically not been
guaranteed live. Binary wheel contact is not available. Neither signal is
required.

## Scores

The collision score combines:

- magnitude of lateral/longitudinal baseline deviation in g;
- horizontal jerk;
- deceleration;
- angular velocity;
- a small bonus when incident count increases.

Braking with pedal/wheel-lock evidence and its immediate transition are
suppressed unless compatible incident or rotation evidence exists.

## Incident points and inferred types

The incident detector observes only positive changes between consecutive valid
`PlayerCarMyIncidentCount` samples. The point delta is exact SDK data. The app
does not generate an event for:

- the first sample after connecting or entering the car;
- a missing count;
- a reset or counter decrease;
- invalid or out-of-car telemetry.

The SDK does not include a stewarding-cause variable alongside the count. The
following labels are inferred over a configurable evidence window:

| Label | Primary evidence |
| --- | --- |
| Off track | `PlayerTrackSurface == irsdk_OffTrack` |
| Loss of control | sustained high angular rate without contact evidence |
| Contact | physical collision candidate or a large collision score |
| Rollover | extreme roll/pitch or rollover detector evidence |
| Unknown | counter changed without sufficient evidence |

Point and type switches are both policy gates. The waveform may independently
be chosen by point value or inferred type. Related contact/rollover
notifications are suppressed by default when a physical impact already exists.

The vertical score combines:

- vertical deviation in g;
- vertical jerk;
- peak suspension velocity;
- angular velocity.

Classification also uses:

- rumble pitch/material for kerbs;
- suspension asymmetry for wheel drops;
- a preceding low-acceleration/vertical-motion period for landings;
- a high symmetric suspension peak for severe compression.

## Event output policy

Detectors always continue to produce diagnostics. `HapticEventPolicy` applies
the user's per-event switches only before effect mapping. Consequently:

- a disabled category cannot send rumble;
- the event remains visible under Diagnostics;
- recording and marker comparison remain useful;
- switching profiles loads that profile's deliberate category choices.

Light kerbs are a special case: enabling them also lowers the kerb detector's
threshold. They remain off by default to prevent continuous track-surface
rumble.

## JSONL

Entry types:

- `frame`: `TelemetryFrame` plus the original detection result;
- `marker`: current frame plus marker text.

Replay runs the current detectors again. Marker comparison looks for a
compatible category from 2,000 ms before the click through 250 ms after it,
reports per-marker timing and peak score, and includes every detector candidate
rather than only the highest-priority event.

## Recommended calibration process

1. Enable only the desired categories under **Effects**.
2. Use **Manual test** to calibrate physical comfort before telemetry.
3. Apply the **Default** profile.
4. Record two or three clean laps, including ordinary kerbs.
5. Confirm that normal driving remains quiet.
6. Record controlled events in a test session and mark each immediately.
7. Compare markers with current settings.
8. Review a missed-event suggestion 8% below its observed peak.
9. Review a false-positive suggestion with an 8% margin above its score.
10. Change one value at a time and replay the same recording.
11. Adjust frequency/duration only after detection is reliable.

Interpretation:

- `Matched`: compatible detection in the reaction-time window;
- `Missed`: marker without compatible detection;
- `Unmarked detection`: detection without a marker.

Collision threshold tuning should be guided by `Collision score`; vertical
threshold tuning should be guided by `Vertical score`. Sensitivity multiplies
an entire detector and should be changed only when all thresholds for that
detector need to move together. Cooldown changes repetition timing, not event
strength.

Initial values are functional hypotheses verified by simulation, not universal
calibration for every car.

Automatic recommendations are bounded, grouped by setting and never applied
without confirmation. If the same recording asks to both raise and lower one
threshold, the app reports a conflict and disables automatic application.
Incident markers validate point/type detection but never modify physical
thresholds.

## Automatic profile identity

`IRacingSessionInfoParser` extracts:

- `DriverInfo.DriverCarIdx`;
- the matching `Drivers` entry's `CarID`, `CarClassID`, `CarPath`,
  `CarScreenName`, `CarScreenNameShort` and `CarClassShortName`;
- `WeekendInfo.TrackID`, `TrackName`, `TrackDisplayName`,
  `TrackDisplayShortName` and `TrackConfigName`.

Rules accept case-insensitive `*`/`?` wildcards. All populated fields are AND
conditions. Higher priority wins; equal priority uses greater specificity and
then rule name. Automatic selection is opt-in and retains the current profile
when no rule matches.

The telemetry simulator passes through the same pipeline, event switches and
effect mapping as iRacing. Select the real Toolkit device for a physical
scenario test; the simulated rumble device only records expected commands.
