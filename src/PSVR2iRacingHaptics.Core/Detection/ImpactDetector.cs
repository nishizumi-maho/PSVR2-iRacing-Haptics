using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed class ImpactDetector
{
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;
    private bool _armed = true;

    public DetectionResult Evaluate(
        ProcessedTelemetry telemetry,
        ImpactSettings settings)
    {
        var score = settings.Sensitivity * (
            telemetry.HorizontalImpulseG * 1.15
            + Math.Min(telemetry.HorizontalJerkGPerSec, 45) * 0.042
            + Math.Min(telemetry.SpeedDecelerationG, 7) * 0.34
            + Math.Min(telemetry.AngularRateMagnitude, 8) * 0.16
            + (telemetry.IncidentIncreased ? 0.65 : 0));

        var diagnostics = telemetry with { ImpactScore = score };
        if (!telemetry.Frame.IsDriverInCar
            || telemetry.TimeInCarMilliseconds < settings.WarmupMs
            || telemetry.Frame.SpeedMps < settings.MinimumSpeedMps)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        var rollover = IsRollover(telemetry);
        var minimumCooldown = rollover
            ? settings.RolloverCooldownMs
            : settings.CooldownMs;
        var sinceLast = (telemetry.Frame.Timestamp - _lastEventAt).TotalMilliseconds;
        if (sinceLast < minimumCooldown)
        {
            return new DetectionResult(null, diagnostics);
        }

        var hasImpactShape =
            (telemetry.HorizontalImpulseG >= 0.38
                && telemetry.HorizontalJerkGPerSec >= 2.5)
            || (telemetry.IncidentIncreased && telemetry.HorizontalImpulseG >= 0.22)
            || (telemetry.SpeedDecelerationG >= 1.7
                && telemetry.HorizontalJerkGPerSec >= 1.8)
            || rollover;

        var verticallyDominated = telemetry.VerticalImpulseG
            > telemetry.HorizontalImpulseG * 1.65
            && telemetry.HorizontalImpulseG < 1.35
            && !telemetry.IncidentIncreased;

        var hardBrakingOnly = telemetry.BrakeRecentlyActive
            && !telemetry.IncidentIncreased
            && !rollover
            && Math.Abs(telemetry.LongDelta) > Math.Abs(telemetry.LatDelta) * 1.8
            && telemetry.HorizontalImpulseG < 1.85
            && telemetry.AngularRateMagnitude < 0.85;

        if (!hasImpactShape || verticallyDominated || hardBrakingOnly || telemetry.WheelLockLikely)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        if (!_armed && !rollover)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        var severity = score >= settings.StrongThreshold
            ? EventSeverity.Strong
            : score >= settings.MediumThreshold
                ? EventSeverity.Medium
                : score >= settings.LightThreshold
                    ? EventSeverity.Light
                    : EventSeverity.None;

        if (severity == EventSeverity.None && !rollover)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        var direction = ClassifyDirection(telemetry, rollover);
        var kind = rollover
            ? HapticEventKind.RolloverImpact
            : severity switch
            {
                EventSeverity.Strong => HapticEventKind.StrongImpact,
                EventSeverity.Medium => HapticEventKind.MediumImpact,
                _ => HapticEventKind.LightImpact
            };
        var priority = rollover
            ? 95
            : severity switch
            {
                EventSeverity.Strong => 100,
                EventSeverity.Medium => 80,
                _ => 60
            };

        var reason =
            $"horizontal impulse={telemetry.HorizontalImpulseG:F2} g; "
            + $"jerk={telemetry.HorizontalJerkGPerSec:F1} g/s; "
            + $"deceleration={telemetry.SpeedDecelerationG:F2} g; "
            + $"angular rate={telemetry.AngularRateMagnitude:F2} rad/s; "
            + $"incident={(telemetry.IncidentIncreased ? "increased" : "stable")}; "
            + $"direction={DirectionText(direction)}";

        _lastEventAt = telemetry.Frame.Timestamp;
        _armed = rollover;
        return new DetectionResult(
            new DetectedHapticEvent(
                telemetry.Frame.Timestamp,
                kind,
                rollover ? EventSeverity.Strong : severity,
                score,
                priority,
                direction,
                reason,
                diagnostics),
            diagnostics);
    }

    public void Reset()
    {
        _lastEventAt = DateTimeOffset.MinValue;
        _armed = true;
    }

    private void ResetArmingIfQuiet(double score, ImpactSettings settings)
    {
        if (score < settings.LightThreshold * settings.HysteresisRatio)
        {
            _armed = true;
        }
    }

    private static bool IsRollover(ProcessedTelemetry telemetry)
    {
        var extremeOrientation = Math.Abs(telemetry.Frame.RollRad) > 1.05
            || Math.Abs(telemetry.Frame.PitchRad) > 1.05;
        return extremeOrientation
            && (telemetry.AngularRateMagnitude > 1.35
                || telemetry.VerticalImpulseG > 0.9
                || telemetry.HorizontalImpulseG > 0.9);
    }

    private static ImpactDirection ClassifyDirection(
        ProcessedTelemetry telemetry,
        bool rollover)
    {
        if (rollover)
        {
            return ImpactDirection.Rollover;
        }

        if (Math.Abs(telemetry.LatDelta) > Math.Abs(telemetry.LongDelta) * 1.15)
        {
            return ImpactDirection.Lateral;
        }

        if (telemetry.SpeedDeltaMps < -0.15 || telemetry.SpeedDecelerationG > 0.35)
        {
            return ImpactDirection.Front;
        }

        if (telemetry.SpeedDeltaMps > 0.12)
        {
            return ImpactDirection.Rear;
        }

        return ImpactDirection.Unknown;
    }

    private static string DirectionText(ImpactDirection direction) => direction switch
    {
        ImpactDirection.Lateral => "lateral",
        ImpactDirection.Front => "front",
        ImpactDirection.Rear => "rear",
        ImpactDirection.Rollover => "rollover",
        _ => "undetermined"
    };
}
