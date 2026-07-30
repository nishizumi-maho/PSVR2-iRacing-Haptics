# Custom telemetry triggers

Custom telemetry triggers let a profile decide exactly when a haptic event
exists. They complement the built-in collision, vertical and incident
detectors; they do not bypass the master haptics switch, per-event switches,
priority controller or safety-duration limits.

## Mental model

A trigger has four independent parts:

1. **Input conditions** select raw or derived telemetry and compare it with
   user-owned values.
2. **State controls** decide how long a match must persist and when the trigger
   may fire again.
3. **Detector interaction** adds to, replaces or gates the built-in detector for
   the selected output event.
4. **Rumble mapping** uses the normal event pattern or an optional pattern
   stored on that trigger.

The custom-trigger engine can be disabled for a profile without deleting its
rules. Every rule also has its own Enabled switch. Detection output is still
subject to the event switches on **Effects**.

## First LatAccel rule

This example emits a side-impact event when the magnitude of raw lateral
acceleration reaches 18 m/s² while the car is moving at least 5 m/s:

| Setting | Value |
| --- | --- |
| Output event | `SideImpact` |
| Interaction | `Additive` |
| Condition logic | `AllConditions` |
| Condition 1 | `LatAccelMps2`, absolute, `>= 18` |
| Condition 2 | `SpeedMps`, signed, `>= 5` |
| Hold | `0 ms` |
| Cooldown | `300 ms` |
| Require release | enabled |
| Release | `80 ms` |

`LatAccelMps2` is signed. Positive and negative directions depend on the SDK
axis convention; enabling **Absolute** makes the same threshold apply to both
sides. Do not convert the displayed value to g before entering it: this raw
channel is in m/s². Use `HorizontalImpulseG` when a baseline-relative value in g
is preferred.

Start in Additive mode. Replace mode should be used only after the custom rule
has been dry-run against clean laps and marked events.

## Condition logic

Every condition supports:

- signed or absolute value;
- `>`, `>=`, `<`, `<=`, inclusive between, inclusive outside, equal and
  not-equal;
- an explicit tolerance for equal/not-equal;
- `FailCondition` or `PassCondition` when an optional signal is unavailable.

`AllConditions` is logical AND. `AnyCondition` is logical OR. Multiple enabled
triggers that target the same event are additional OR branches. This makes
nested logic possible without a scripting language:

```text
(large lateral impulse AND enough speed)
OR
(large yaw rate AND incident points increased)
```

Create those as two triggers with `AllConditions`.

Missing suspension, individual-wheel and rumble-pitch channels are normal for
some cars. `FailCondition` is the conservative default. Use `PassCondition`
only when that signal is an optional refinement and the remaining conditions
are safe on their own.

## Interaction modes

| Mode | Built-in event | Custom condition | Result |
| --- | --- | --- | --- |
| Additive | kept | may match independently | either source may emit the event |
| Replace built-in | suppressed for that event kind | complete source | only the custom rule may emit it |
| Gate built-in | required on the same frame | also required | both sources must agree |

Replacement and gate suppression is scoped to the target event kind. A rule
that replaces `LightImpact` does not suppress `StrongImpact`, `Landing` or an
incident event.

Directional outputs are matched deliberately: `SideImpact`, `FrontImpact` and
`RearImpact` treat a built-in light/medium/strong collision with the
corresponding classified direction as their compatible built-in event. This
makes Gate and Replace useful for directional rules even though the built-in
detector normally represents severity in `Kind` and direction in `Direction`.

When several candidates of one event kind occur on the same frame, the engine
keeps one deterministic candidate. A custom match takes precedence for that
kind, then priority and score break ties. The complete candidate set is then
ordered by the normal application priority rules.

## Hold, cooldown and rearm

- **Hold** requires the complete condition expression to remain true for the
  specified time. Use zero for one-frame impact spikes. A nonzero hold is useful
  for states such as sustained rotation or surface contact.
- **Cooldown** is the minimum time between firings. It does not change the
  rumble strength.
- **Require release** prevents a continuously true expression from firing
  repeatedly.
- **Release time** requires the expression to remain false for that long before
  rearming.
- **Priority** controls competition with other event families; it does not make
  the physical motor stronger.

A conservative transient-event starting point is hold `0 ms`, cooldown
`300 ms`, require release enabled and release `80 ms`.

## Available signals

The in-app signal list is authoritative for the running version. Units follow
the normalized `TelemetryFrame` model and the iRacing SDK.

| Group | Signals | Unit or representation |
| --- | --- | --- |
| Motion | `SpeedMps`, `VelocityXMps`, `VelocityYMps`, `VelocityZMps` | m/s |
| Raw acceleration | `LatAccelMps2`, `LongAccelMps2`, `VertAccelMps2` | m/s² |
| Orientation | `YawRad`, `PitchRad`, `RollRad` | rad |
| Angular rate | `YawRateRadPerSec`, `PitchRateRadPerSec`, `RollRateRadPerSec` | rad/s |
| Driver/powertrain | `Brake`, `Throttle`, `Gear`, `Rpm` | SDK value; brake/throttle are 0–1 |
| Incident/surface | `IncidentCount`, `IncidentPointDelta`, `IncidentIncreased`, `PlayerTrackSurface`, `PlayerTrackSurfaceMaterial` | count, point delta, 0/1 or SDK enum |
| Wheel speed | `LfWheelSpeedMps`, `RfWheelSpeedMps`, `LrWheelSpeedMps`, `RrWheelSpeedMps` | m/s; optional |
| Shock deflection | `LfShockDeflectionM`, `RfShockDeflectionM`, `LrShockDeflectionM`, `RrShockDeflectionM` | m; optional |
| Shock velocity | `LfShockVelocityMps`, `RfShockVelocityMps`, `LrShockVelocityMps`, `RrShockVelocityMps` | m/s; optional |
| Tire rumble | `TireLfRumblePitchHz`, `TireRfRumblePitchHz`, `TireLrRumblePitchHz`, `TireRrRumblePitchHz` | Hz; optional |
| Filtered acceleration | `SmoothedLatAccelMps2`, `SmoothedLongAccelMps2`, `SmoothedVertAccelMps2` | m/s² |
| Slow baseline | `BaselineLatAccelMps2`, `BaselineLongAccelMps2`, `BaselineVertAccelMps2` | m/s² |
| Baseline delta | `LatDeltaMps2`, `LongDeltaMps2`, `VertDeltaMps2` | m/s² |
| Axis jerk | `LatJerkMps3`, `LongJerkMps3`, `VertJerkMps3` | m/s³ |
| Derived impact | `HorizontalImpulseG`, `VerticalImpulseG`, `SpeedDecelerationG` | g |
| Derived change | `HorizontalJerkGPerSec`, `VerticalJerkGPerSec` | g/s |
| Derived motion | `SpeedDeltaMps`, `AngularRateMagnitudeRadPerSec` | m/s or rad/s |
| Suspension summary | `SuspensionVelocityPeakMps`, `SuspensionVelocityAsymmetryMps` | m/s |
| Rumble summary | `RumbleStripWheelCount`, `MaxRumblePitchHz` | count or Hz |
| Detector evidence | `WheelLockLikely`, `BrakeRecentlyActive`, `ImpactScore`, `VerticalScore` | 0/1 or unitless score |
| Timing/state | `TimeInCarMilliseconds`, `SessionState`, `EnterExitReset` | ms or SDK integer |
| Validity | `IsConnected`, `IsValid`, `IsOnTrack`, `IsOnTrackCar`, `IsInGarage`, `IsReplayPlaying`, `IsDriverInCar` | 0 or 1 |

The rule engine never evaluates output-eligible triggers when
`IsDriverInCar` is false. Consequently, validity conditions are usually useful
for replay analysis and diagnostics rather than as a substitute for the
application's safety gate.

## Raw versus derived values

Raw acceleration contains sustained cornering, acceleration, braking, banking
and gravity. A simple raw threshold can therefore be car/track dependent.
Derived values remove or summarize some of that context:

- `LatDeltaMps2` and `LongDeltaMps2` compare the smoothed signal with a slow
  baseline;
- `HorizontalImpulseG` combines the lateral and longitudinal baseline deltas;
- jerk emphasizes rapid change;
- `ImpactScore` and `VerticalScore` expose the built-in detector's score before
  its severity threshold;
- braking and wheel-lock evidence can be added as explicit conditions when a
  raw longitudinal rule needs false-positive protection.

There is no universal best signal. Calibrate with telemetry from the intended
car and track, and keep a clean-lap recording as negative evidence.

## Calibrating from live driving or an iRacing replay

1. Select the **Simulated rumble device** or disable all haptics while
   collecting data.
2. Start a JSONL recording, or enable the circular buffer.
3. Drive a clean baseline. To use an iRacing replay, start recording first and
   then play the replay in iRacing.
4. Add markers with the app, a global keyboard shortcut or a wheel binding.
   If the event was unpredictable, save the circular buffer immediately after
   it occurs.
5. Stop the recording.
6. Create or select a trigger and choose **Analyze JSONL**.
7. Compare normal-driving p95/p99 with the maximum around compatible markers.
8. Change one condition, save it, and analyze the identical file again.
9. Repeat with several positive examples and at least two clean laps.
10. Enable physical output only after the rule separates those samples
    reliably.

**Analyze JSONL is always a dry run** and never sends a headset command. Live
iRacing replay is also output-ineligible. The separate **Replay JSONL** action
runs recorded frames through the normal app pipeline and can send physical
rumble when the real device is selected; the app displays a warning first.

## Reading the dry-run statistics

For each trigger the report includes matching frames, actual firings and
matches suppressed by hold/cooldown/release state. Every condition reports:

- minimum and maximum;
- median;
- p95 and p99;
- missing-sample count;
- minimum and maximum inside marker reaction windows.

For a short spike, compare the clean-lap p99 with the marker-window maximum. A
candidate threshold needs margin from both. A threshold chosen from only one
event is a hypothesis, not a finished calibration. For sustained conditions,
inspect matching-frame count together with hold time rather than looking only
at the maximum.

Statistics are descriptive; the app does not silently rewrite custom rules.

## Rumble patterns and safety

By default, a custom trigger uses the active profile's pattern for its output
event. Enabling the trigger-specific pattern exposes frequency, pulse duration,
pulse count, gap, tail frequency and tail duration.

Those values still pass through:

- the master and category switches;
- the 0–25 Hz conservative range;
- maximum continuous and total-effect duration;
- serialized output, rate limiting and priority/preemption;
- mandatory `0 Hz` shutdown behavior.

Frequency is not treated as intensity. Complete **Comfort calibration** and
choose a recognizable value inside the measured range.

## Profiles, sharing and recovery

Triggers are profile-owned and follow manual or automatic profile activation.
Exporting a profile includes its trigger rules and optional patterns. Import
shows a preview, creates a new user profile and cannot change global device,
safety, startup or input settings.

If a rule behaves badly:

1. use the emergency stop;
2. disable the custom-trigger engine for that profile;
3. select the simulated rumble device;
4. inspect Diagnostics and dry-run the saved JSONL;
5. switch back to Additive mode or remove the rule;
6. re-enable physical output only after the negative recording stays quiet.

Deleting all custom triggers restores built-in detection; it does not delete
the profile's effects or built-in thresholds.
