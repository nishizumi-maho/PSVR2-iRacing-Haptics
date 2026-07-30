# Real-hardware test procedure

## Preparation

1. Read the PSVR2 Toolkit warning and jailbreak guide.
2. Confirm that the official Toolkit works by itself.
3. Close every other application that can send HMD rumble.
4. Keep **STOP ALL RUMBLE NOW** visible.
5. If compatible with the upstream procedure, begin with the headset off your
   head.

## C API test

1. Start PSVR2 Toolkit and SteamVR.
2. Open this app.
3. Confirm that Status shows:
   - Toolkit path file found;
   - C API DLL loaded;
   - C API initialized;
   - Toolkit driver active.
4. On **Manual test**, use 12 Hz, 120 ms and one pulse.
5. Start the test.
6. Physically confirm that rumble starts and then stops.
7. Click **Stop immediately** and confirm that no rumble remains.
8. Test 14, 18, 21 and 25 Hz gradually, keeping 120 ms at first and never
   exceeding 25 Hz.
9. Only then compare 80, 120, 160 and 200 ms. Pause between tests and stop if
   the sensation becomes uncomfortable.

Repeat the sequence through **Comfort calibration**. If the test reports that
no clearly perceptible and comfortable range was found, keep haptics disabled;
do not substitute an untested default.

If any pulse does not stop:

1. press the emergency stop;
2. close this app;
3. stop PSVR2 Toolkit and SteamVR;
4. disconnect/restart the equipment as described by the upstream project;
5. preserve the log and do not continue until the failure is understood.

## Event-switch test

1. Open **Effects**.
2. Disable light impacts and save.
3. Run the Side impact simulator scenario with real hardware selected.
4. Confirm that the event appears under Diagnostics but produces no rumble.
5. Re-enable light impacts and save.
6. Run the same scenario and confirm that rumble is now produced.
7. Repeat for landing, strong kerb and rollover.
8. Save this setup as a user profile, switch away and back, and confirm that
   the profile restores its own disabled categories.

## Profile assignment test

1. Enter an iRacing test session and open **Profiles**.
2. Confirm that detected car/track values are populated.
3. Create a user profile and add a rule with **Use detected car and track**.
4. Enable automatic selection and save.
5. Switch manually to another profile, leave the session and re-enter it.
6. Confirm that the rule selects the expected profile and that the result is
   explained under Status, Profiles, Diagnostics and logs.
7. Change only the track configuration and confirm that a configuration-specific
   rule does not match the wrong layout.
8. Disable automatic selection and confirm that the current profile remains
   unchanged when car/track identity changes.

## Incident test

1. Keep incident haptics off and record a controlled 1x off track.
2. Confirm that Diagnostics/logs show the counter delta without rumble.
3. Enable incident haptics, the 1x gate and the off-track gate.
4. Select point-based patterns and reproduce a safe 1x event.
5. Repeat with type-based patterns and confirm that the off-track waveform is
   used.
6. For a contact test, keep duplicate protection on. Confirm that the physical
   impact rumbles once while the related incident remains logged.
7. Disable duplicate protection only for a controlled test and confirm that the
   incident pattern follows the physical effect instead of interrupting it.

The point delta should match the SDK counter exactly. Off-track/contact/loss-of-
control/rollover labels are heuristic and must be reported as such.

## Failure tests

- run the app without the Toolkit; it must remain open and responsive;
- start the Toolkit later; discovery should recover;
- disable all haptics during a pulse; the log should show `Rumble: OFF`;
- disconnect or stop the driver while idle;
- close iRacing while idle;
- exit the car; no new effect should be sent;
- close the app after a test; the log should record OFF.

Avoid deliberately losing the driver during active rumble until basic behavior
is confirmed. The current native function may block in that race condition.

## iRacing calibration test

1. Use an offline test-drive session.
2. Apply the Default profile.
3. Record two or three clean laps with ordinary braking and kerbs.
4. Confirm that normal driving does not create unwanted effects.
5. Start a new JSONL recording.
6. Reproduce one controlled event at a time and add the matching marker
   immediately.
7. Stop recording and compare markers.
8. Lower a missed event's matching threshold by 0.10–0.20.
9. Raise a threshold by 0.10–0.20 when normal driving causes false positives.
10. Change one value at a time and replay the same JSONL after each change.
11. Tune frequency and duration only after event detection is reliable.

## Custom-trigger and replay safety test

1. Select the simulated rumble device and create an Additive rule using
   absolute `LatAccelMps2`, a conservative threshold and a minimum-speed
   condition.
2. Record clean laps and several controlled events; dry-run the file with
   **Analyze JSONL**.
3. Confirm that clean-lap p99 remains below the chosen value and that the marked
   examples cross it.
4. Disable the custom-trigger engine and verify that built-in events still
   appear while the custom evaluation disappears.
5. Play an iRacing replay while recording. Confirm that no physical command is
   produced by the live replay.
6. Analyze that recording and confirm that the recorded replay frames are
   included in the dry-run statistics.
7. Select **Replay JSONL** with the real device configured. Confirm that the app
   warns before any replay-driven physical output.
8. Return to the simulated device before testing Replace or Gate mode.

## Global-control and circular-buffer test

1. Assign an unused keyboard shortcut and wheel button to emergency stop.
2. Trigger a manual effect and verify that each binding immediately sends OFF.
3. Verify toggle-haptics, recording and marker bindings while iRacing has focus.
4. Keep a 30–60 second circular buffer, reproduce an event and save it from the
   binding.
5. Confirm that the saved JSONL contains the preceding frames plus the final
   marker and can be analyzed.

Do not calibrate in an official race. Suspension and rumble-pitch channels
vary by car, so repeat the test when changing to a substantially different
vehicle category.

## Evidence to retain

- installed Toolkit version;
- car and track;
- JSONL recording;
- rotating log;
- profile/settings values;
- whether zero stopped the motor;
- relative sensation at 10/14/18/21/25 Hz;
- shortest clearly perceptible duration in each tested range;
- false positives and missed markers.
