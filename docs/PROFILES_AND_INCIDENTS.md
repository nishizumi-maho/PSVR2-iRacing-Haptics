# Profiles, automatic assignments and incidents

## What a profile contains

A profile is a named snapshot of:

- collision detector sensitivity, thresholds, cooldown and minimum speed;
- vertical detector sensitivity, thresholds and cooldown;
- each collision/vertical/incident output switch;
- incident evidence, cooldown, duplicate and pattern-basis policy;
- the custom-trigger engine state and every telemetry trigger;
- every frequency, duration, pulse-count, gap and tail value.

The master haptics switch, device selection and safety limits are global. This
prevents a car/track rule from silently selecting hardware or weakening safety.

Factory profiles can be edited and reset. They cannot be renamed or deleted.
User profiles can be created from the active values, duplicated, renamed and
deleted. Deleting a profile also removes its assignment rules; deleting the
active profile returns to Default.

Profiles can be exported as `*.psvr2haptics.json`. Import validates the package,
previews its incident/trigger content, creates a new user identity and cannot
alter the receiving app's global device, safety, input or startup settings.

## Recommended profile workflow

1. Calibrate Default using a recorded solo session.
2. Choose **New from current** and name the result for its purpose, such as
   `GT3 baseline`.
3. Adjust only the values needed by that vehicle category.
4. Duplicate it for a circuit-specific exception, such as `GT3 Nordschleife`.
5. Replay the same JSONL after every detector change.
6. Create automatic rules only after both profiles behave correctly manually.

## Assignment fields

| Field | Typical value | Guidance |
| --- | --- | --- |
| Car path | `porsche911rgt3` | Most stable car field |
| Car display name | `Porsche 911 GT3 R` | Readable but may change |
| Car class | `GT3` | Useful for category-wide profiles |
| Track name | `spa` | Most stable circuit field |
| Track configuration | `Grand Prix Pits` | Use only for layout-specific setup |

Every non-empty field must match. Leave a field blank to accept any value.
Wildcards are case-insensitive:

- `porsche*` matches every car path beginning with `porsche`;
- `*gt3*` matches a display name or class containing `gt3`;
- `s?a` matches `spa`;
- `*` matches any non-empty value.

An all-empty rule is rejected because it would unintentionally match every
session.

## Rule precedence

Rules are sorted by:

1. higher numeric priority;
2. greater specificity (more populated/less-wildcarded text);
3. rule name, case-insensitively.

This makes the result deterministic. For example:

| Rule | Priority | Criteria | Result |
| --- | ---: | --- | --- |
| GT3 baseline | 10 | class=`GT3` | broad category default |
| Porsche Spa | 20 | path=`porsche911rgt3`; track=`spa` | overrides baseline |
| Porsche Spa wet | 30 | same plus config=`Wet` | overrides both |

When no rule matches, the app keeps the current profile. Disabling automatic
selection also keeps the current profile.

## Exact incident points

The app compares consecutive valid values of
`PlayerCarMyIncidentCount`. A change from 3 to 7 produces one 4x event. It does
not infer four separate 1x events. Supported point categories are:

- 1x;
- 2x;
- 4x;
- other positive delta.

The first sample initializes the baseline. Missing values, resets, counter
decreases and out-of-car data do not create events.

## Inferred incident types

The SDK counter does not include the stewarding cause. The app uses a recent,
bounded evidence window:

- **Off track**: the player occupied `irsdk_OffTrack`;
- **Loss of control**: significant angular motion without stronger contact or
  off-track evidence;
- **Contact**: a physical collision candidate or compatible collision score;
- **Rollover**: extreme orientation/rotation;
- **Unknown**: the counter changed but evidence was inconclusive.

These labels are diagnostic aids, not authoritative iRacing decisions. Keep the
Unknown switch enabled while validating a new car so an unexplained delta is
visible.

## Two pattern modes

**Point-value mode** chooses the waveform from 1x/2x/4x/other. Use it when the
penalty size should determine the sensation.

**Inferred-type mode** chooses the waveform from off track/loss of
control/contact/rollover/unknown. Use it when cause differentiation matters
more than point count.

The point and type enable switches remain gates in both modes. For example,
disabling 4x blocks a 4x contact even in type mode; disabling Contact blocks it
even in point mode.

## Duplicate protection

A contact often produces both:

1. an immediate physical collision event; and
2. a later incident-counter change.

With duplicate protection on (default), the incident remains in diagnostics and
logs but does not produce a second waveform. With it off, the incident waveform
is scheduled after the physical effect; it never interrupts the stronger
physical event.

## Troubleshooting

- **No detected identity**: use live iRacing telemetry, enter the car and wait
  for a valid `SessionInfo` update. Replays/simulators use the identity stored
  in their frames.
- **Rule does not match**: compare the exact values shown under Profiles; remove
  optional fields one at a time.
- **Wrong profile wins**: inspect priority first, then specificity.
- **Incident is logged but silent**: check the global master, incident master,
  point gate, type gate and duplicate protection in that order.
- **Type is Unknown**: increase the evidence window moderately and preserve a
  JSONL/log. Do not assume the SDK supplied a missing label.
- **Too many sensations**: keep duplicate protection on, lengthen incident
  cooldown, and disable incident rumble while retaining diagnostics.
