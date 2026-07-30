using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed class VerticalImpactDetector
{
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;
    private double _airborneConfidence;
    private bool _armed = true;

    public DetectionResult Evaluate(
        ProcessedTelemetry telemetry,
        VerticalImpactSettings settings)
    {
        var priorAirborneConfidence = _airborneConfidence;
        UpdateAirborneConfidence(telemetry);

        var score = settings.Sensitivity * (
            telemetry.VerticalImpulseG * 1.18
            + Math.Min(telemetry.VerticalJerkGPerSec, 55) * 0.052
            + Math.Min(telemetry.SuspensionVelocityPeakMps, 6) * 0.42
            + Math.Min(telemetry.AngularRateMagnitude, 6) * 0.10);
        var diagnostics = telemetry with { VerticalScore = score };

        if (!telemetry.Frame.IsDriverInCar
            || telemetry.TimeInCarMilliseconds < settings.WarmupMs
            || telemetry.Frame.SpeedMps < settings.MinimumSpeedMps)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        var sinceLast = (telemetry.Frame.Timestamp - _lastEventAt).TotalMilliseconds;
        if (sinceLast < settings.CooldownMs)
        {
            return new DetectionResult(null, diagnostics);
        }

        var rumbleMaterial = telemetry.Frame.PlayerTrackSurfaceMaterial is >= 11 and <= 14;
        var rumbleEvidence = telemetry.RumbleStripWheelCount > 0 || rumbleMaterial;
        var landingEvidence = priorAirborneConfidence >= 0.62
            && telemetry.VerticalImpulseG >= 0.65
            && telemetry.VerticalJerkGPerSec >= 3.0;
        var horizontalCollisionDominates = telemetry.HorizontalImpulseG
            > Math.Max(1.25, telemetry.VerticalImpulseG * 0.82)
            && !rumbleEvidence;

        if (horizontalCollisionDominates)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        if (!_armed)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        HapticEventKind kind;
        EventSeverity severity;
        int priority;
        double threshold;
        string evidence;

        if (landingEvidence && settings.LandingsEnabled)
        {
            kind = HapticEventKind.Landing;
            severity = score >= settings.SevereCompressionThreshold
                ? EventSeverity.Strong
                : EventSeverity.Medium;
            priority = 70;
            threshold = settings.LandingThreshold;
            evidence = $"confiança de voo={priorAirborneConfidence:F2}";
        }
        else if (rumbleEvidence && settings.StrongKerbsEnabled)
        {
            kind = HapticEventKind.StrongKerb;
            severity = EventSeverity.Light;
            priority = 40;
            threshold = settings.StrongKerbThreshold;
            evidence =
                $"rodas em rumble strip={telemetry.RumbleStripWheelCount}; "
                + $"pitch máximo={telemetry.MaxRumblePitchHz:F1} Hz";
        }
        else if (settings.WheelDropsEnabled
                 && telemetry.SuspensionVelocityAsymmetryMps >= 0.55)
        {
            kind = HapticEventKind.WheelDrop;
            severity = EventSeverity.Medium;
            priority = 55;
            threshold = settings.StrongKerbThreshold * 0.9;
            evidence =
                $"assimetria da suspensão={telemetry.SuspensionVelocityAsymmetryMps:F2} m/s";
        }
        else if (settings.SevereCompressionEnabled)
        {
            kind = HapticEventKind.SevereVerticalCompression;
            severity = EventSeverity.Strong;
            priority = 65;
            threshold = settings.SevereCompressionThreshold;
            evidence =
                $"pico da suspensão={telemetry.SuspensionVelocityPeakMps:F2} m/s";
        }
        else
        {
            return new DetectionResult(null, diagnostics);
        }

        if (kind == HapticEventKind.StrongKerb
            && settings.LightKerbsEnabled
            && score >= threshold * 0.62)
        {
            threshold *= 0.62;
        }

        if (score < threshold)
        {
            ResetArmingIfQuiet(score, settings);
            return new DetectionResult(null, diagnostics);
        }

        var reason =
            $"impulso vertical={telemetry.VerticalImpulseG:F2} g; "
            + $"jerk vertical={telemetry.VerticalJerkGPerSec:F1} g/s; "
            + $"{evidence}; horizontal={telemetry.HorizontalImpulseG:F2} g";

        _lastEventAt = telemetry.Frame.Timestamp;
        _armed = false;
        if (kind == HapticEventKind.Landing)
        {
            _airborneConfidence = 0;
        }

        return new DetectionResult(
            new DetectedHapticEvent(
                telemetry.Frame.Timestamp,
                kind,
                severity,
                score,
                priority,
                ImpactDirection.NotApplicable,
                reason,
                diagnostics),
            diagnostics);
    }

    public void Reset()
    {
        _lastEventAt = DateTimeOffset.MinValue;
        _airborneConfidence = 0;
        _armed = true;
    }

    private void UpdateAirborneConfidence(ProcessedTelemetry telemetry)
    {
        if (!telemetry.Frame.IsDriverInCar)
        {
            _airborneConfidence = 0;
            return;
        }

        var freeFallLike = telemetry.VertDelta < -TelemetrySignalProcessor.GravityMps2 * 0.35;
        var verticalMotion = Math.Abs(telemetry.Frame.VelocityZMps) > 0.65;
        var lowSuspensionActivity = telemetry.SuspensionVelocityPeakMps < 0.22;

        if (freeFallLike || (verticalMotion && lowSuspensionActivity))
        {
            _airborneConfidence = Math.Min(
                1.0,
                _airborneConfidence + telemetry.DeltaTimeSeconds * 4.5);
        }
        else
        {
            _airborneConfidence = Math.Max(
                0,
                _airborneConfidence - telemetry.DeltaTimeSeconds * 1.2);
        }
    }

    private void ResetArmingIfQuiet(double score, VerticalImpactSettings settings)
    {
        var quietThreshold = Math.Min(
            settings.StrongKerbThreshold,
            settings.LandingThreshold) * settings.HysteresisRatio;
        if (score < quietThreshold)
        {
            _armed = true;
        }
    }
}
