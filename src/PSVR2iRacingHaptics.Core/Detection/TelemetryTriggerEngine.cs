using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed record TriggerConditionEvaluation(
    TelemetrySignal Signal,
    double? ObservedValue,
    bool Matched,
    string Explanation);

public sealed record TriggerEvaluation(
    string TriggerId,
    string TriggerName,
    HapticEventKind TargetEvent,
    bool ConditionsMatched,
    bool BuiltInMatched,
    bool Fired,
    bool SuppressesBuiltIn,
    string Explanation,
    IReadOnlyList<TriggerConditionEvaluation> Conditions);

public sealed record TriggerEngineResult(
    IReadOnlyList<DetectedHapticEvent> Candidates,
    IReadOnlyList<TriggerEvaluation> Evaluations);

/// <summary>
/// Evaluates profile-owned telemetry rules after the built-in detectors have
/// produced their candidates. The engine supports additive rules, complete
/// replacement of a built-in event and gating a built-in event with arbitrary
/// telemetry conditions.
/// </summary>
public sealed class TelemetryTriggerEngine
{
    private readonly Dictionary<string, RuntimeState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public TriggerEngineResult Evaluate(
        ProcessedTelemetry telemetry,
        TelemetryTriggerSettings settings,
        IReadOnlyList<DetectedHapticEvent> builtInCandidates)
    {
        if (!telemetry.Frame.IsDriverInCar)
        {
            Reset();
            return new TriggerEngineResult(
                Array.Empty<DetectedHapticEvent>(),
                Array.Empty<TriggerEvaluation>());
        }

        if (!settings.Enabled || settings.CustomTriggers.Count == 0)
        {
            return new TriggerEngineResult(
                builtInCandidates.ToArray(),
                Array.Empty<TriggerEvaluation>());
        }

        var enabled = settings.CustomTriggers
            .Where(trigger => trigger.Enabled && trigger.Conditions.Count > 0)
            .ToArray();
        var suppressingTriggers = enabled
            .Where(trigger => trigger.SourceMode is
                TriggerSourceMode.ReplaceBuiltIn or TriggerSourceMode.GateBuiltIn)
            .ToArray();
        var candidates = builtInCandidates
            .Where(candidate => !suppressingTriggers.Any(trigger =>
                IsCompatibleBuiltIn(trigger.TargetEvent, candidate)))
            .ToList();
        var evaluations = new List<TriggerEvaluation>(enabled.Length);
        var activeIds = enabled
            .Select(trigger => trigger.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleId in _states.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _states.Remove(staleId);
        }

        foreach (var trigger in enabled)
        {
            var builtIn = builtInCandidates
                .Where(candidate =>
                    IsCompatibleBuiltIn(trigger.TargetEvent, candidate))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => candidate.Score)
                .FirstOrDefault();
            var conditionEvaluations = trigger.Conditions
                .Select(condition => EvaluateCondition(telemetry, condition))
                .ToArray();
            var conditionsMatched = conditionEvaluations.Length > 0
                && (trigger.MatchMode == TriggerMatchMode.AllConditions
                    ? conditionEvaluations.All(condition => condition.Matched)
                    : conditionEvaluations.Any(condition => condition.Matched));
            var effectiveMatch = conditionsMatched
                && (trigger.SourceMode != TriggerSourceMode.GateBuiltIn
                    || builtIn is not null);
            var state = StateFor(trigger.Id);
            var fired = AdvanceState(
                state,
                effectiveMatch,
                telemetry.Frame.Timestamp,
                trigger);

            if (fired)
            {
                candidates.Add(CreateEvent(
                    telemetry,
                    trigger,
                    builtIn,
                    conditionEvaluations));
            }

            var explanation = BuildEvaluationExplanation(
                trigger,
                conditionsMatched,
                builtIn is not null,
                fired,
                state,
                telemetry.Frame.Timestamp);
            evaluations.Add(new TriggerEvaluation(
                trigger.Id,
                trigger.Name,
                trigger.TargetEvent,
                conditionsMatched,
                builtIn is not null,
                fired,
                trigger.SourceMode != TriggerSourceMode.Additive,
                explanation,
                conditionEvaluations));
        }

        var deduplicated = candidates
            .GroupBy(candidate => candidate.Kind)
            .Select(group => group
                .OrderByDescending(candidate => candidate.IsCustomTrigger)
                .ThenByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => candidate.Score)
                .First())
            .OrderByDescending(candidate => candidate.Priority)
            .ThenByDescending(candidate => candidate.Score)
            .ToArray();
        return new TriggerEngineResult(deduplicated, evaluations);
    }

    public void Reset() => _states.Clear();

    public static double? ReadSignal(
        ProcessedTelemetry telemetry,
        TelemetrySignal signal)
    {
        var frame = telemetry.Frame;
        return signal switch
        {
            TelemetrySignal.SpeedMps => frame.SpeedMps,
            TelemetrySignal.LatAccelMps2 => frame.LatAccelMps2,
            TelemetrySignal.LongAccelMps2 => frame.LongAccelMps2,
            TelemetrySignal.VertAccelMps2 => frame.VertAccelMps2,
            TelemetrySignal.VelocityXMps => frame.VelocityXMps,
            TelemetrySignal.VelocityYMps => frame.VelocityYMps,
            TelemetrySignal.VelocityZMps => frame.VelocityZMps,
            TelemetrySignal.YawRad => frame.YawRad,
            TelemetrySignal.PitchRad => frame.PitchRad,
            TelemetrySignal.RollRad => frame.RollRad,
            TelemetrySignal.YawRateRadPerSec => frame.YawRateRadPerSec,
            TelemetrySignal.PitchRateRadPerSec => frame.PitchRateRadPerSec,
            TelemetrySignal.RollRateRadPerSec => frame.RollRateRadPerSec,
            TelemetrySignal.Brake => frame.Brake,
            TelemetrySignal.Throttle => frame.Throttle,
            TelemetrySignal.Gear => frame.Gear,
            TelemetrySignal.Rpm => frame.Rpm,
            TelemetrySignal.IncidentCount => frame.IncidentCount,
            TelemetrySignal.IncidentPointDelta => telemetry.IncidentPointDelta,
            TelemetrySignal.IncidentIncreased => Bool(telemetry.IncidentIncreased),
            TelemetrySignal.PlayerTrackSurface => frame.PlayerTrackSurface,
            TelemetrySignal.PlayerTrackSurfaceMaterial =>
                frame.PlayerTrackSurfaceMaterial,
            TelemetrySignal.LfWheelSpeedMps => frame.LfWheelSpeedMps,
            TelemetrySignal.RfWheelSpeedMps => frame.RfWheelSpeedMps,
            TelemetrySignal.LrWheelSpeedMps => frame.LrWheelSpeedMps,
            TelemetrySignal.RrWheelSpeedMps => frame.RrWheelSpeedMps,
            TelemetrySignal.LfShockDeflectionM => frame.LfShockDeflectionM,
            TelemetrySignal.RfShockDeflectionM => frame.RfShockDeflectionM,
            TelemetrySignal.LrShockDeflectionM => frame.LrShockDeflectionM,
            TelemetrySignal.RrShockDeflectionM => frame.RrShockDeflectionM,
            TelemetrySignal.LfShockVelocityMps => frame.LfShockVelocityMps,
            TelemetrySignal.RfShockVelocityMps => frame.RfShockVelocityMps,
            TelemetrySignal.LrShockVelocityMps => frame.LrShockVelocityMps,
            TelemetrySignal.RrShockVelocityMps => frame.RrShockVelocityMps,
            TelemetrySignal.TireLfRumblePitchHz => frame.TireLfRumblePitchHz,
            TelemetrySignal.TireRfRumblePitchHz => frame.TireRfRumblePitchHz,
            TelemetrySignal.TireLrRumblePitchHz => frame.TireLrRumblePitchHz,
            TelemetrySignal.TireRrRumblePitchHz => frame.TireRrRumblePitchHz,
            TelemetrySignal.SmoothedLatAccelMps2 => telemetry.SmoothedLatAccel,
            TelemetrySignal.SmoothedLongAccelMps2 => telemetry.SmoothedLongAccel,
            TelemetrySignal.SmoothedVertAccelMps2 => telemetry.SmoothedVertAccel,
            TelemetrySignal.BaselineLatAccelMps2 => telemetry.BaselineLatAccel,
            TelemetrySignal.BaselineLongAccelMps2 => telemetry.BaselineLongAccel,
            TelemetrySignal.BaselineVertAccelMps2 => telemetry.BaselineVertAccel,
            TelemetrySignal.LatDeltaMps2 => telemetry.LatDelta,
            TelemetrySignal.LongDeltaMps2 => telemetry.LongDelta,
            TelemetrySignal.VertDeltaMps2 => telemetry.VertDelta,
            TelemetrySignal.LatJerkMps3 => telemetry.LatJerk,
            TelemetrySignal.LongJerkMps3 => telemetry.LongJerk,
            TelemetrySignal.VertJerkMps3 => telemetry.VertJerk,
            TelemetrySignal.SpeedDeltaMps => telemetry.SpeedDeltaMps,
            TelemetrySignal.SpeedDecelerationG => telemetry.SpeedDecelerationG,
            TelemetrySignal.AngularRateMagnitudeRadPerSec =>
                telemetry.AngularRateMagnitude,
            TelemetrySignal.HorizontalImpulseG => telemetry.HorizontalImpulseG,
            TelemetrySignal.VerticalImpulseG => telemetry.VerticalImpulseG,
            TelemetrySignal.HorizontalJerkGPerSec =>
                telemetry.HorizontalJerkGPerSec,
            TelemetrySignal.VerticalJerkGPerSec => telemetry.VerticalJerkGPerSec,
            TelemetrySignal.SuspensionVelocityPeakMps =>
                telemetry.SuspensionVelocityPeakMps,
            TelemetrySignal.SuspensionVelocityAsymmetryMps =>
                telemetry.SuspensionVelocityAsymmetryMps,
            TelemetrySignal.RumbleStripWheelCount =>
                telemetry.RumbleStripWheelCount,
            TelemetrySignal.MaxRumblePitchHz => telemetry.MaxRumblePitchHz,
            TelemetrySignal.WheelLockLikely => Bool(telemetry.WheelLockLikely),
            TelemetrySignal.BrakeRecentlyActive =>
                Bool(telemetry.BrakeRecentlyActive),
            TelemetrySignal.ImpactScore => telemetry.ImpactScore,
            TelemetrySignal.VerticalScore => telemetry.VerticalScore,
            TelemetrySignal.TimeInCarMilliseconds =>
                telemetry.TimeInCarMilliseconds,
            TelemetrySignal.IsOnTrack => Bool(frame.IsOnTrack),
            TelemetrySignal.IsInGarage => Bool(frame.IsInGarage),
            TelemetrySignal.IsReplayPlaying => Bool(frame.IsReplayPlaying),
            TelemetrySignal.SessionState => frame.SessionState,
            TelemetrySignal.EnterExitReset => frame.EnterExitReset,
            TelemetrySignal.IsConnected => Bool(frame.IsConnected),
            TelemetrySignal.IsValid => Bool(frame.IsValid),
            TelemetrySignal.IsOnTrackCar => Bool(frame.IsOnTrackCar),
            TelemetrySignal.IsDriverInCar => Bool(frame.IsDriverInCar),
            _ => null
        };
    }

    private static TriggerConditionEvaluation EvaluateCondition(
        ProcessedTelemetry telemetry,
        TelemetryTriggerCondition condition)
    {
        var raw = ReadSignal(telemetry, condition.Signal);
        if (!raw.HasValue || !double.IsFinite(raw.Value))
        {
            var matched =
                condition.MissingSignalBehavior == MissingSignalBehavior.PassCondition;
            return new TriggerConditionEvaluation(
                condition.Signal,
                null,
                matched,
                matched ? "signal missing; configured to pass" : "signal missing");
        }

        var observed = condition.UseAbsoluteValue
            ? Math.Abs(raw.Value)
            : raw.Value;
        var tolerance = Math.Max(0, condition.EqualityTolerance);
        var lower = Math.Min(condition.Value, condition.SecondValue);
        var upper = Math.Max(condition.Value, condition.SecondValue);
        var matched = condition.Comparison switch
        {
            TriggerComparison.GreaterThan => observed > condition.Value,
            TriggerComparison.GreaterThanOrEqual => observed >= condition.Value,
            TriggerComparison.LessThan => observed < condition.Value,
            TriggerComparison.LessThanOrEqual => observed <= condition.Value,
            TriggerComparison.BetweenInclusive => observed >= lower && observed <= upper,
            TriggerComparison.OutsideInclusive => observed <= lower || observed >= upper,
            TriggerComparison.Equal =>
                Math.Abs(observed - condition.Value) <= tolerance,
            TriggerComparison.NotEqual =>
                Math.Abs(observed - condition.Value) > tolerance,
            _ => false
        };
        var descriptor = TelemetrySignalCatalog.Describe(condition.Signal);
        return new TriggerConditionEvaluation(
            condition.Signal,
            observed,
            matched,
            $"{observed:F3}{UnitSuffix(descriptor.Unit)} "
            + $"{ComparisonText(condition.Comparison)} "
            + ConditionTarget(condition, lower, upper, descriptor.Unit));
    }

    private RuntimeState StateFor(string triggerId)
    {
        if (!_states.TryGetValue(triggerId, out var state))
        {
            state = new RuntimeState();
            _states.Add(triggerId, state);
        }
        return state;
    }

    private static bool AdvanceState(
        RuntimeState state,
        bool matched,
        DateTimeOffset timestamp,
        CustomTelemetryTrigger trigger)
    {
        if (!matched)
        {
            state.MatchedSince = null;
            if (!state.Armed)
            {
                state.ReleasedSince ??= timestamp;
                if ((timestamp - state.ReleasedSince.Value).TotalMilliseconds
                    >= trigger.ReleaseMilliseconds)
                {
                    state.Armed = true;
                    state.ReleasedSince = null;
                }
            }
            return false;
        }

        state.ReleasedSince = null;
        state.MatchedSince ??= timestamp;
        var heldLongEnough = (timestamp - state.MatchedSince.Value).TotalMilliseconds
            >= trigger.HoldMilliseconds;
        var cooldownElapsed = (timestamp - state.LastFiredAt).TotalMilliseconds
            >= trigger.CooldownMilliseconds;
        if (!heldLongEnough || !cooldownElapsed || !state.Armed)
        {
            return false;
        }

        state.LastFiredAt = timestamp;
        if (trigger.RequireReleaseBeforeRetrigger)
        {
            state.Armed = false;
        }
        else
        {
            state.MatchedSince = timestamp;
        }
        return true;
    }

    private static DetectedHapticEvent CreateEvent(
        ProcessedTelemetry telemetry,
        CustomTelemetryTrigger trigger,
        DetectedHapticEvent? builtIn,
        IReadOnlyList<TriggerConditionEvaluation> conditions)
    {
        var score = conditions
            .Where(condition => condition.ObservedValue.HasValue)
            .Select(condition => Math.Abs(condition.ObservedValue!.Value))
            .DefaultIfEmpty(1)
            .Max();
        var reason = $"custom trigger '{trigger.Name}' matched "
            + $"{trigger.MatchMode}; "
            + string.Join(
                "; ",
                conditions.Select(condition =>
                    $"{condition.Signal}: {condition.Explanation} "
                    + $"({(condition.Matched ? "pass" : "fail")})"));
        var incidentPoints = trigger.TargetEvent switch
        {
            HapticEventKind.Incident1x => 1,
            HapticEventKind.Incident2x => 2,
            HapticEventKind.Incident4x => 4,
            HapticEventKind.IncidentOther =>
                Math.Max(1, telemetry.IncidentPointDelta),
            _ => 0
        };

        return new DetectedHapticEvent(
            telemetry.Frame.Timestamp,
            trigger.TargetEvent,
            builtIn?.Severity ?? SeverityFor(trigger.TargetEvent),
            score,
            trigger.Priority,
            builtIn?.Direction ?? DirectionFor(trigger.TargetEvent),
            reason,
            telemetry)
        {
            IncidentPoints = builtIn?.IncidentPoints ?? incidentPoints,
            IncidentType = builtIn?.IncidentType ?? IncidentType.NotApplicable,
            HasRelatedPhysicalEvent = builtIn?.HasRelatedPhysicalEvent ?? false,
            TriggerId = trigger.Id,
            TriggerName = trigger.Name,
            IsCustomTrigger = true
        };
    }

    private static string BuildEvaluationExplanation(
        CustomTelemetryTrigger trigger,
        bool conditionsMatched,
        bool builtInMatched,
        bool fired,
        RuntimeState state,
        DateTimeOffset timestamp)
    {
        if (fired)
        {
            return "fired";
        }
        if (!conditionsMatched)
        {
            return "conditions did not match";
        }
        if (trigger.SourceMode == TriggerSourceMode.GateBuiltIn && !builtInMatched)
        {
            return "conditions matched, but the built-in detector did not";
        }
        if (!state.Armed)
        {
            return "waiting for the rule to release";
        }
        var held = state.MatchedSince.HasValue
            ? (timestamp - state.MatchedSince.Value).TotalMilliseconds
            : 0;
        if (held < trigger.HoldMilliseconds)
        {
            return $"waiting for hold time ({held:F0}/{trigger.HoldMilliseconds} ms)";
        }
        var cooldown = (timestamp - state.LastFiredAt).TotalMilliseconds;
        if (cooldown < trigger.CooldownMilliseconds)
        {
            return $"cooldown ({cooldown:F0}/{trigger.CooldownMilliseconds} ms)";
        }
        return "matched but did not fire";
    }

    private static EventSeverity SeverityFor(HapticEventKind kind) => kind switch
    {
        HapticEventKind.StrongImpact
            or HapticEventKind.RolloverImpact
            or HapticEventKind.SevereVerticalCompression
            or HapticEventKind.Incident4x => EventSeverity.Strong,
        HapticEventKind.MediumImpact
            or HapticEventKind.SideImpact
            or HapticEventKind.FrontImpact
            or HapticEventKind.RearImpact
            or HapticEventKind.WheelDrop
            or HapticEventKind.Landing
            or HapticEventKind.Incident2x
            or HapticEventKind.IncidentOther => EventSeverity.Medium,
        _ => EventSeverity.Light
    };

    private static ImpactDirection DirectionFor(HapticEventKind kind) => kind switch
    {
        HapticEventKind.SideImpact => ImpactDirection.Lateral,
        HapticEventKind.FrontImpact => ImpactDirection.Front,
        HapticEventKind.RearImpact => ImpactDirection.Rear,
        HapticEventKind.RolloverImpact => ImpactDirection.Rollover,
        _ => ImpactDirection.NotApplicable
    };

    private static bool IsCompatibleBuiltIn(
        HapticEventKind target,
        DetectedHapticEvent candidate) =>
        target switch
        {
            HapticEventKind.SideImpact =>
                IsCollision(candidate.Kind)
                && candidate.Direction == ImpactDirection.Lateral,
            HapticEventKind.FrontImpact =>
                IsCollision(candidate.Kind)
                && candidate.Direction == ImpactDirection.Front,
            HapticEventKind.RearImpact =>
                IsCollision(candidate.Kind)
                && candidate.Direction == ImpactDirection.Rear,
            _ => candidate.Kind == target
        };

    private static bool IsCollision(HapticEventKind kind) =>
        kind is HapticEventKind.LightImpact
            or HapticEventKind.MediumImpact
            or HapticEventKind.StrongImpact;

    private static double Bool(bool value) => value ? 1.0 : 0.0;

    private static string ComparisonText(TriggerComparison comparison) => comparison switch
    {
        TriggerComparison.GreaterThan => ">",
        TriggerComparison.GreaterThanOrEqual => ">=",
        TriggerComparison.LessThan => "<",
        TriggerComparison.LessThanOrEqual => "<=",
        TriggerComparison.BetweenInclusive => "between",
        TriggerComparison.OutsideInclusive => "outside",
        TriggerComparison.Equal => "equals",
        TriggerComparison.NotEqual => "does not equal",
        _ => "?"
    };

    private static string ConditionTarget(
        TelemetryTriggerCondition condition,
        double lower,
        double upper,
        string unit)
    {
        var suffix = UnitSuffix(unit);
        return condition.Comparison is TriggerComparison.BetweenInclusive
            or TriggerComparison.OutsideInclusive
                ? $"{lower:F3}{suffix} and {upper:F3}{suffix}"
                : $"{condition.Value:F3}{suffix}";
    }

    private static string UnitSuffix(string unit) =>
        string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";

    private sealed class RuntimeState
    {
        public DateTimeOffset? MatchedSince { get; set; }
        public DateTimeOffset? ReleasedSince { get; set; }
        public DateTimeOffset LastFiredAt { get; set; } = DateTimeOffset.MinValue;
        public bool Armed { get; set; } = true;
    }
}
