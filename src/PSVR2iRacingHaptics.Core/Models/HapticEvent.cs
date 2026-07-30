namespace PSVR2iRacingHaptics.Core.Models;

public enum HapticEventKind
{
    None = 0,
    LightImpact,
    MediumImpact,
    StrongImpact,
    SideImpact,
    FrontImpact,
    RearImpact,
    RolloverImpact,
    StrongKerb,
    WheelDrop,
    Landing,
    SevereVerticalCompression,
    Incident1x,
    Incident2x,
    Incident4x,
    IncidentOther
}

public enum EventSeverity
{
    None = 0,
    Light = 1,
    Medium = 2,
    Strong = 3
}

public enum ImpactDirection
{
    NotApplicable = 0,
    Unknown,
    Lateral,
    Front,
    Rear,
    Rollover
}

/// <summary>
/// Best-effort incident classification derived from telemetry around an
/// incident-count change. iRacing exposes the point count, not a direct cause.
/// </summary>
public enum IncidentType
{
    NotApplicable = 0,
    Unknown,
    OffTrack,
    LossOfControl,
    Contact,
    Rollover
}

public sealed record DetectedHapticEvent(
    DateTimeOffset Timestamp,
    HapticEventKind Kind,
    EventSeverity Severity,
    double Score,
    int Priority,
    ImpactDirection Direction,
    string Reason,
    ProcessedTelemetry Diagnostics)
{
    public int IncidentPoints { get; init; }
    public IncidentType IncidentType { get; init; } = IncidentType.NotApplicable;
    public bool HasRelatedPhysicalEvent { get; init; }
    public bool IsCustomTrigger { get; init; }
    public string? TriggerId { get; init; }
    public string? TriggerName { get; init; }
}

public sealed record DetectionResult(
    DetectedHapticEvent? Event,
    ProcessedTelemetry Diagnostics);

public sealed record ProcessedTelemetry
{
    public TelemetryFrame Frame { get; init; } = new();
    public double DeltaTimeSeconds { get; init; }
    public bool IsWarm { get; init; }
    public double TimeInCarMilliseconds { get; init; }
    public double SmoothedLatAccel { get; init; }
    public double SmoothedLongAccel { get; init; }
    public double SmoothedVertAccel { get; init; }
    public double BaselineLatAccel { get; init; }
    public double BaselineLongAccel { get; init; }
    public double BaselineVertAccel { get; init; }
    public double LatDelta { get; init; }
    public double LongDelta { get; init; }
    public double VertDelta { get; init; }
    public double LatJerk { get; init; }
    public double LongJerk { get; init; }
    public double VertJerk { get; init; }
    public double SpeedDeltaMps { get; init; }
    public double SpeedDecelerationG { get; init; }
    public double AngularRateMagnitude { get; init; }
    public double HorizontalImpulseG { get; init; }
    public double VerticalImpulseG { get; init; }
    public double HorizontalJerkGPerSec { get; init; }
    public double VerticalJerkGPerSec { get; init; }
    public double SuspensionVelocityPeakMps { get; init; }
    public double SuspensionVelocityAsymmetryMps { get; init; }
    public int RumbleStripWheelCount { get; init; }
    public double MaxRumblePitchHz { get; init; }
    public bool WheelLockLikely { get; init; }
    public bool BrakeRecentlyActive { get; init; }
    public bool IncidentIncreased { get; init; }
    public int IncidentPointDelta { get; init; }
    public double ImpactScore { get; init; }
    public double VerticalScore { get; init; }
}
