using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Configuration;

/// <summary>
/// Controls how a user-defined trigger interacts with the built-in detector
/// for the same event.
/// </summary>
public enum TriggerSourceMode
{
    /// <summary>
    /// Keep the built-in detector and also allow this rule to emit the event.
    /// </summary>
    Additive = 0,

    /// <summary>
    /// Suppress the built-in event of the same kind. The custom rule becomes
    /// the complete source of that event.
    /// </summary>
    ReplaceBuiltIn = 1,

    /// <summary>
    /// Suppress the unfiltered built-in event and emit it only when both the
    /// built-in detector and this rule match on the same frame.
    /// </summary>
    GateBuiltIn = 2
}

public enum TriggerMatchMode
{
    AllConditions = 0,
    AnyCondition = 1
}

public enum TriggerComparison
{
    GreaterThan = 0,
    GreaterThanOrEqual = 1,
    LessThan = 2,
    LessThanOrEqual = 3,
    BetweenInclusive = 4,
    OutsideInclusive = 5,
    Equal = 6,
    NotEqual = 7
}

public enum MissingSignalBehavior
{
    FailCondition = 0,
    PassCondition = 1
}

/// <summary>
/// Raw and derived values available to custom trigger conditions. Units are
/// deliberately part of the enum name or supplied by
/// <see cref="TelemetrySignalCatalog"/> so saved profiles remain unambiguous.
/// </summary>
public enum TelemetrySignal
{
    SpeedMps = 0,
    LatAccelMps2,
    LongAccelMps2,
    VertAccelMps2,
    VelocityXMps,
    VelocityYMps,
    VelocityZMps,
    YawRad,
    PitchRad,
    RollRad,
    YawRateRadPerSec,
    PitchRateRadPerSec,
    RollRateRadPerSec,
    Brake,
    Throttle,
    Gear,
    Rpm,
    IncidentCount,
    IncidentPointDelta,
    IncidentIncreased,
    PlayerTrackSurface,
    PlayerTrackSurfaceMaterial,
    LfWheelSpeedMps,
    RfWheelSpeedMps,
    LrWheelSpeedMps,
    RrWheelSpeedMps,
    LfShockDeflectionM,
    RfShockDeflectionM,
    LrShockDeflectionM,
    RrShockDeflectionM,
    LfShockVelocityMps,
    RfShockVelocityMps,
    LrShockVelocityMps,
    RrShockVelocityMps,
    TireLfRumblePitchHz,
    TireRfRumblePitchHz,
    TireLrRumblePitchHz,
    TireRrRumblePitchHz,
    SmoothedLatAccelMps2,
    SmoothedLongAccelMps2,
    SmoothedVertAccelMps2,
    BaselineLatAccelMps2,
    BaselineLongAccelMps2,
    BaselineVertAccelMps2,
    LatDeltaMps2,
    LongDeltaMps2,
    VertDeltaMps2,
    LatJerkMps3,
    LongJerkMps3,
    VertJerkMps3,
    SpeedDeltaMps,
    SpeedDecelerationG,
    AngularRateMagnitudeRadPerSec,
    HorizontalImpulseG,
    VerticalImpulseG,
    HorizontalJerkGPerSec,
    VerticalJerkGPerSec,
    SuspensionVelocityPeakMps,
    SuspensionVelocityAsymmetryMps,
    RumbleStripWheelCount,
    MaxRumblePitchHz,
    WheelLockLikely,
    BrakeRecentlyActive,
    ImpactScore,
    VerticalScore,
    TimeInCarMilliseconds,
    IsOnTrack,
    IsInGarage,
    IsReplayPlaying,
    SessionState,
    EnterExitReset,
    IsConnected,
    IsValid,
    IsOnTrackCar,
    IsDriverInCar
}

public sealed class TelemetryTriggerCondition
{
    public TelemetrySignal Signal { get; set; } = TelemetrySignal.HorizontalImpulseG;
    public TriggerComparison Comparison { get; set; } =
        TriggerComparison.GreaterThanOrEqual;
    public bool UseAbsoluteValue { get; set; }
    public double Value { get; set; } = 1.0;
    public double SecondValue { get; set; } = 2.0;
    public double EqualityTolerance { get; set; } = 0.001;
    public MissingSignalBehavior MissingSignalBehavior { get; set; } =
        MissingSignalBehavior.FailCondition;
}

public sealed class CustomTelemetryTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New telemetry trigger";
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public HapticEventKind TargetEvent { get; set; } = HapticEventKind.LightImpact;
    public TriggerSourceMode SourceMode { get; set; } = TriggerSourceMode.Additive;
    public TriggerMatchMode MatchMode { get; set; } = TriggerMatchMode.AllConditions;
    public List<TelemetryTriggerCondition> Conditions { get; set; } = [];

    /// <summary>
    /// Conditions must remain true for this long before the rule may fire.
    /// Zero is appropriate for one-frame impact spikes.
    /// </summary>
    public int HoldMilliseconds { get; set; }

    /// <summary>
    /// Minimum time between firings even when release is not required.
    /// </summary>
    public int CooldownMilliseconds { get; set; } = 300;

    /// <summary>
    /// When enabled, the rule must become false for ReleaseMilliseconds before
    /// it can fire again. This prevents continuous rumble from a sustained
    /// threshold crossing.
    /// </summary>
    public bool RequireReleaseBeforeRetrigger { get; set; } = true;
    public int ReleaseMilliseconds { get; set; } = 80;
    public int Priority { get; set; } = 60;

    /// <summary>
    /// Use a pattern stored directly on this trigger instead of the profile's
    /// normal pattern for TargetEvent.
    /// </summary>
    public bool UseCustomEffect { get; set; }
    public EffectPatternSettings CustomEffect { get; set; } =
        new() { FrequencyHz = 14, DurationMs = 120 };
}

public sealed class TelemetryTriggerSettings
{
    public bool Enabled { get; set; } = true;
    public List<CustomTelemetryTrigger> CustomTriggers { get; set; } = [];
}

public sealed record TelemetrySignalDescriptor(
    TelemetrySignal Signal,
    string DisplayName,
    string Unit,
    string Description);

public static class TelemetrySignalCatalog
{
    public static IReadOnlyList<TelemetrySignalDescriptor> All { get; } =
        Enum.GetValues<TelemetrySignal>()
            .Select(signal => new TelemetrySignalDescriptor(
                signal,
                SplitName(signal.ToString()),
                Unit(signal),
                Description(signal)))
            .ToArray();

    public static TelemetrySignalDescriptor Describe(TelemetrySignal signal) =>
        All.First(descriptor => descriptor.Signal == signal);

    private static string Unit(TelemetrySignal signal) => signal switch
    {
        TelemetrySignal.SpeedMps
            or TelemetrySignal.VelocityXMps
            or TelemetrySignal.VelocityYMps
            or TelemetrySignal.VelocityZMps
            or TelemetrySignal.SpeedDeltaMps
            or TelemetrySignal.LfWheelSpeedMps
            or TelemetrySignal.RfWheelSpeedMps
            or TelemetrySignal.LrWheelSpeedMps
            or TelemetrySignal.RrWheelSpeedMps
            or TelemetrySignal.LfShockVelocityMps
            or TelemetrySignal.RfShockVelocityMps
            or TelemetrySignal.LrShockVelocityMps
            or TelemetrySignal.RrShockVelocityMps
            or TelemetrySignal.SuspensionVelocityPeakMps
            or TelemetrySignal.SuspensionVelocityAsymmetryMps => "m/s",
        TelemetrySignal.LatAccelMps2
            or TelemetrySignal.LongAccelMps2
            or TelemetrySignal.VertAccelMps2
            or TelemetrySignal.SmoothedLatAccelMps2
            or TelemetrySignal.SmoothedLongAccelMps2
            or TelemetrySignal.SmoothedVertAccelMps2
            or TelemetrySignal.BaselineLatAccelMps2
            or TelemetrySignal.BaselineLongAccelMps2
            or TelemetrySignal.BaselineVertAccelMps2
            or TelemetrySignal.LatDeltaMps2
            or TelemetrySignal.LongDeltaMps2
            or TelemetrySignal.VertDeltaMps2 => "m/s²",
        TelemetrySignal.LatJerkMps3
            or TelemetrySignal.LongJerkMps3
            or TelemetrySignal.VertJerkMps3 => "m/s³",
        TelemetrySignal.YawRad
            or TelemetrySignal.PitchRad
            or TelemetrySignal.RollRad => "rad",
        TelemetrySignal.YawRateRadPerSec
            or TelemetrySignal.PitchRateRadPerSec
            or TelemetrySignal.RollRateRadPerSec
            or TelemetrySignal.AngularRateMagnitudeRadPerSec => "rad/s",
        TelemetrySignal.SpeedDecelerationG
            or TelemetrySignal.HorizontalImpulseG
            or TelemetrySignal.VerticalImpulseG => "g",
        TelemetrySignal.HorizontalJerkGPerSec
            or TelemetrySignal.VerticalJerkGPerSec => "g/s",
        TelemetrySignal.LfShockDeflectionM
            or TelemetrySignal.RfShockDeflectionM
            or TelemetrySignal.LrShockDeflectionM
            or TelemetrySignal.RrShockDeflectionM => "m",
        TelemetrySignal.TireLfRumblePitchHz
            or TelemetrySignal.TireRfRumblePitchHz
            or TelemetrySignal.TireLrRumblePitchHz
            or TelemetrySignal.TireRrRumblePitchHz
            or TelemetrySignal.MaxRumblePitchHz => "Hz",
        TelemetrySignal.TimeInCarMilliseconds => "ms",
        _ => string.Empty
    };

    private static string Description(TelemetrySignal signal) => signal switch
    {
        TelemetrySignal.SpeedMps =>
            "Raw iRacing vehicle speed.",
        TelemetrySignal.LatAccelMps2 =>
            "Raw iRacing lateral acceleration. Use absolute value to match either side.",
        TelemetrySignal.LongAccelMps2 =>
            "Raw iRacing longitudinal acceleration. The sign distinguishes acceleration and braking.",
        TelemetrySignal.VertAccelMps2 =>
            "Raw iRacing vertical acceleration.",
        TelemetrySignal.VelocityXMps
            or TelemetrySignal.VelocityYMps
            or TelemetrySignal.VelocityZMps =>
            "A raw iRacing world/local velocity-axis value.",
        TelemetrySignal.YawRad
            or TelemetrySignal.PitchRad
            or TelemetrySignal.RollRad =>
            "A raw iRacing orientation axis.",
        TelemetrySignal.YawRateRadPerSec
            or TelemetrySignal.PitchRateRadPerSec
            or TelemetrySignal.RollRateRadPerSec =>
            "A raw iRacing angular-velocity axis.",
        TelemetrySignal.Brake or TelemetrySignal.Throttle =>
            "Raw driver input from 0 to 1.",
        TelemetrySignal.Gear or TelemetrySignal.Rpm =>
            "Raw iRacing powertrain telemetry.",
        TelemetrySignal.IncidentCount =>
            "Current cumulative PlayerCarMyIncidentCount value.",
        TelemetrySignal.HorizontalImpulseG =>
            "Magnitude of lateral and longitudinal acceleration above the slow baseline.",
        TelemetrySignal.VerticalImpulseG =>
            "Absolute vertical acceleration above the slow baseline.",
        TelemetrySignal.HorizontalJerkGPerSec =>
            "Rate of change of the smoothed horizontal acceleration.",
        TelemetrySignal.VerticalJerkGPerSec =>
            "Rate of change of the smoothed vertical acceleration.",
        TelemetrySignal.IncidentPointDelta =>
            "Exact increase in PlayerCarMyIncidentCount on this frame.",
        TelemetrySignal.IncidentIncreased =>
            "1 when the incident counter increased on this frame; otherwise 0.",
        TelemetrySignal.PlayerTrackSurface
            or TelemetrySignal.PlayerTrackSurfaceMaterial =>
            "Raw iRacing surface enum/material value.",
        TelemetrySignal.LfWheelSpeedMps
            or TelemetrySignal.RfWheelSpeedMps
            or TelemetrySignal.LrWheelSpeedMps
            or TelemetrySignal.RrWheelSpeedMps =>
            "Raw optional individual-wheel speed.",
        TelemetrySignal.LfShockDeflectionM
            or TelemetrySignal.RfShockDeflectionM
            or TelemetrySignal.LrShockDeflectionM
            or TelemetrySignal.RrShockDeflectionM =>
            "Raw optional shock deflection for one wheel.",
        TelemetrySignal.LfShockVelocityMps
            or TelemetrySignal.RfShockVelocityMps
            or TelemetrySignal.LrShockVelocityMps
            or TelemetrySignal.RrShockVelocityMps =>
            "Raw optional shock velocity for one wheel.",
        TelemetrySignal.TireLfRumblePitchHz
            or TelemetrySignal.TireRfRumblePitchHz
            or TelemetrySignal.TireLrRumblePitchHz
            or TelemetrySignal.TireRrRumblePitchHz =>
            "Raw optional tire rumble-strip pitch for one wheel.",
        TelemetrySignal.SmoothedLatAccelMps2
            or TelemetrySignal.SmoothedLongAccelMps2
            or TelemetrySignal.SmoothedVertAccelMps2 =>
            "Fast-filtered acceleration used by the built-in detectors.",
        TelemetrySignal.BaselineLatAccelMps2
            or TelemetrySignal.BaselineLongAccelMps2
            or TelemetrySignal.BaselineVertAccelMps2 =>
            "Slow acceleration baseline used to separate sustained motion from impulses.",
        TelemetrySignal.LatDeltaMps2
            or TelemetrySignal.LongDeltaMps2
            or TelemetrySignal.VertDeltaMps2 =>
            "Fast-filtered acceleration minus the slow baseline.",
        TelemetrySignal.LatJerkMps3
            or TelemetrySignal.LongJerkMps3
            or TelemetrySignal.VertJerkMps3 =>
            "Rate of change of one fast-filtered acceleration axis.",
        TelemetrySignal.SpeedDeltaMps =>
            "Change in speed since the preceding processed frame.",
        TelemetrySignal.SpeedDecelerationG =>
            "Positive deceleration magnitude inferred from the speed change.",
        TelemetrySignal.AngularRateMagnitudeRadPerSec =>
            "Magnitude of the three angular-rate axes.",
        TelemetrySignal.ImpactScore =>
            "Built-in collision score before severity thresholds are applied.",
        TelemetrySignal.VerticalScore =>
            "Built-in vertical-event score before event thresholds are applied.",
        TelemetrySignal.SuspensionVelocityPeakMps =>
            "Largest absolute available shock velocity.",
        TelemetrySignal.SuspensionVelocityAsymmetryMps =>
            "Spread between available shock velocities.",
        TelemetrySignal.RumbleStripWheelCount =>
            "Number of wheels whose optional rumble-pitch channel indicates a strip.",
        TelemetrySignal.MaxRumblePitchHz =>
            "Largest available tire rumble-pitch value.",
        TelemetrySignal.WheelLockLikely =>
            "1 when wheel-speed and braking evidence suggest lockup; otherwise 0.",
        TelemetrySignal.BrakeRecentlyActive =>
            "1 during braking and its short transition window; otherwise 0.",
        TelemetrySignal.TimeInCarMilliseconds =>
            "Time accumulated in the current valid in-car run.",
        TelemetrySignal.IsReplayPlaying =>
            "1 while the iRacing SDK reports replay playback; otherwise 0.",
        TelemetrySignal.SessionState or TelemetrySignal.EnterExitReset =>
            "Raw iRacing session/reset state integer.",
        TelemetrySignal.IsConnected
            or TelemetrySignal.IsValid
            or TelemetrySignal.IsOnTrack
            or TelemetrySignal.IsOnTrackCar
            or TelemetrySignal.IsInGarage
            or TelemetrySignal.IsDriverInCar =>
            "Runtime state represented as 1 for true and 0 for false.",
        _ => "A raw or derived telemetry value recorded in calibration JSONL files."
    };

    private static string SplitName(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            if (index > 0
                && char.IsUpper(value[index])
                && !char.IsUpper(value[index - 1]))
            {
                result.Append(' ');
            }
            result.Append(value[index]);
        }
        return result.ToString();
    }
}
