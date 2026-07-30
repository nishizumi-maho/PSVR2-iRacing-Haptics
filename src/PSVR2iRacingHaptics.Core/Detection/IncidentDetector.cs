using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

/// <summary>
/// Turns a change in PlayerCarMyIncidentCount into a separately configurable
/// haptic event. iRacing does not expose the incident cause directly, so the
/// type is inferred from a short window of physical telemetry.
/// </summary>
public sealed class IncidentDetector
{
    private readonly Queue<EvidenceSample> _evidence = new();
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;

    public DetectionResult Evaluate(
        ProcessedTelemetry telemetry,
        IncidentSettings settings,
        IReadOnlyList<DetectedHapticEvent> physicalCandidates)
    {
        if (!telemetry.Frame.IsDriverInCar)
        {
            Reset();
            return new DetectionResult(null, telemetry);
        }

        AddEvidence(telemetry, physicalCandidates, settings.EvidenceWindowMs);
        if (!telemetry.IncidentIncreased || telemetry.IncidentPointDelta <= 0)
        {
            return new DetectionResult(null, telemetry);
        }

        var now = telemetry.Frame.Timestamp;
        if ((now - _lastEventAt).TotalMilliseconds < settings.CooldownMs)
        {
            return new DetectionResult(null, telemetry);
        }

        var relevant = _evidence.ToArray();
        var physical = relevant
            .Where(sample => sample.PhysicalEvent is not null)
            .OrderByDescending(sample => sample.PhysicalEvent!.Priority)
            .Select(sample => sample.PhysicalEvent)
            .FirstOrDefault();
        var maximumImpact = relevant.Length == 0
            ? telemetry.ImpactScore
            : relevant.Max(sample => sample.ImpactScore);
        var maximumAngularRate = relevant.Length == 0
            ? telemetry.AngularRateMagnitude
            : relevant.Max(sample => sample.AngularRate);
        var sawOffTrack = relevant.Any(sample => sample.TrackLocation == 0);
        var sawRollover = relevant.Any(sample => sample.Rollover);

        var incidentType = sawRollover
            ? IncidentType.Rollover
            : physical is not null && IsCollision(physical.Kind)
                ? IncidentType.Contact
                : sawOffTrack
                    ? IncidentType.OffTrack
                    : maximumAngularRate >= 1.25 && maximumImpact < 2.4
                        ? IncidentType.LossOfControl
                        : maximumImpact >= 1.05
                            ? IncidentType.Contact
                            : IncidentType.Unknown;

        var points = telemetry.IncidentPointDelta;
        var kind = points switch
        {
            1 => HapticEventKind.Incident1x,
            2 => HapticEventKind.Incident2x,
            4 => HapticEventKind.Incident4x,
            _ => HapticEventKind.IncidentOther
        };
        var severity = points switch
        {
            <= 1 => EventSeverity.Light,
            2 => EventSeverity.Medium,
            _ => EventSeverity.Strong
        };
        var priority = points switch
        {
            <= 1 => 35,
            2 => 50,
            4 => 75,
            _ => 55
        };
        var relatedPhysicalEvent = physical is not null
            || incidentType is IncidentType.Contact or IncidentType.Rollover;
        var reason =
            $"incident counter increased by {points}x; "
            + $"inferred type={TypeText(incidentType)}; "
            + $"evidence window={settings.EvidenceWindowMs} ms; "
            + $"peak collision score={maximumImpact:F2}; "
            + $"peak angular rate={maximumAngularRate:F2} rad/s; "
            + $"off-track evidence={(sawOffTrack ? "yes" : "no")}; "
            + $"related physical event={(relatedPhysicalEvent ? "yes" : "no")}";

        _lastEventAt = now;
        return new DetectionResult(
            new DetectedHapticEvent(
                now,
                kind,
                severity,
                points,
                priority,
                ImpactDirection.NotApplicable,
                reason,
                telemetry)
            {
                IncidentPoints = points,
                IncidentType = incidentType,
                HasRelatedPhysicalEvent = relatedPhysicalEvent
            },
            telemetry);
    }

    public void Reset()
    {
        _evidence.Clear();
        _lastEventAt = DateTimeOffset.MinValue;
    }

    private void AddEvidence(
        ProcessedTelemetry telemetry,
        IReadOnlyList<DetectedHapticEvent> physicalCandidates,
        int windowMs)
    {
        var physical = physicalCandidates
            .OrderByDescending(candidate => candidate.Priority)
            .FirstOrDefault();
        _evidence.Enqueue(new EvidenceSample(
            telemetry.Frame.Timestamp,
            telemetry.ImpactScore,
            telemetry.AngularRateMagnitude,
            telemetry.Frame.PlayerTrackSurface,
            IsRollover(telemetry),
            physical));

        var cutoff = telemetry.Frame.Timestamp
            - TimeSpan.FromMilliseconds(Math.Clamp(windowMs, 250, 5000));
        while (_evidence.Count > 0 && _evidence.Peek().Timestamp < cutoff)
        {
            _evidence.Dequeue();
        }
    }

    private static bool IsRollover(ProcessedTelemetry telemetry) =>
        Math.Abs(telemetry.Frame.RollRad) > 1.05
        || Math.Abs(telemetry.Frame.PitchRad) > 1.05
        || telemetry.AngularRateMagnitude > 3.5;

    private static bool IsCollision(HapticEventKind kind) =>
        kind is HapticEventKind.LightImpact
            or HapticEventKind.MediumImpact
            or HapticEventKind.StrongImpact
            or HapticEventKind.RolloverImpact;

    private static string TypeText(IncidentType type) => type switch
    {
        IncidentType.OffTrack => "off track",
        IncidentType.LossOfControl => "loss of control",
        IncidentType.Contact => "contact",
        IncidentType.Rollover => "rollover",
        _ => "unknown"
    };

    private sealed record EvidenceSample(
        DateTimeOffset Timestamp,
        double ImpactScore,
        double AngularRate,
        int? TrackLocation,
        bool Rollover,
        DetectedHapticEvent? PhysicalEvent);
}
