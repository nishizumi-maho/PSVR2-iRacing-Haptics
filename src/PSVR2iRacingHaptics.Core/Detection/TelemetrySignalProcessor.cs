using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed class TelemetrySignalProcessor
{
    public const double GravityMps2 = 9.80665;

    private TelemetryFrame? _lastFrame;
    private DateTimeOffset _enteredCarAt;
    private bool _initialized;
    private double _smoothLat;
    private double _smoothLong;
    private double _smoothVert;
    private double _baselineLat;
    private double _baselineLong;
    private double _baselineVert;
    private double _brakeHoldSeconds;
    private int? _lastIncidentCount;

    public ProcessedTelemetry Process(TelemetryFrame frame)
    {
        if (!IsFiniteFrame(frame) || !frame.IsDriverInCar)
        {
            Reset();
            return new ProcessedTelemetry
            {
                Frame = frame,
                DeltaTimeSeconds = 1.0 / 60.0,
                IsWarm = false
            };
        }

        if (!_initialized || _lastFrame is null ||
            frame.Timestamp <= _lastFrame.Timestamp ||
            frame.Sequence < _lastFrame.Sequence)
        {
            Initialize(frame);
            return BuildInitial(frame);
        }

        var dt = Math.Clamp(
            (frame.Timestamp - _lastFrame.Timestamp).TotalSeconds,
            1.0 / 360.0,
            0.12);
        var previousSmoothLat = _smoothLat;
        var previousSmoothLong = _smoothLong;
        var previousSmoothVert = _smoothVert;

        var fastAlpha = 1.0 - Math.Exp(-dt / 0.035);
        _smoothLat += fastAlpha * (frame.LatAccelMps2 - _smoothLat);
        _smoothLong += fastAlpha * (frame.LongAccelMps2 - _smoothLong);
        _smoothVert += fastAlpha * (frame.VertAccelMps2 - _smoothVert);

        var latDelta = _smoothLat - _baselineLat;
        var longDelta = _smoothLong - _baselineLong;
        var vertDelta = _smoothVert - _baselineVert;

        var latJerk = (_smoothLat - previousSmoothLat) / dt;
        var longJerk = (_smoothLong - previousSmoothLong) / dt;
        var vertJerk = (_smoothVert - previousSmoothVert) / dt;
        var speedDelta = frame.SpeedMps - _lastFrame.SpeedMps;
        var speedDecelerationG = Math.Max(0, -speedDelta / dt) / GravityMps2;

        var horizontalImpulseG =
            Math.Sqrt(latDelta * latDelta + longDelta * longDelta) / GravityMps2;
        var verticalImpulseG = Math.Abs(vertDelta) / GravityMps2;
        var horizontalJerkG = Math.Sqrt(latJerk * latJerk + longJerk * longJerk)
            / GravityMps2;
        var verticalJerkG = Math.Abs(vertJerk) / GravityMps2;
        var angularRate = Math.Sqrt(
            frame.YawRateRadPerSec * frame.YawRateRadPerSec
            + frame.PitchRateRadPerSec * frame.PitchRateRadPerSec
            + frame.RollRateRadPerSec * frame.RollRateRadPerSec);

        var shockVelocities = Values(
            frame.LfShockVelocityMps,
            frame.RfShockVelocityMps,
            frame.LrShockVelocityMps,
            frame.RrShockVelocityMps).Select(Math.Abs).ToArray();
        var shockPeak = shockVelocities.Length == 0 ? 0 : shockVelocities.Max();
        var shockAsymmetry = shockVelocities.Length < 2
            ? 0
            : shockVelocities.Max() - shockVelocities.Min();

        var rumblePitches = Values(
            frame.TireLfRumblePitchHz,
            frame.TireRfRumblePitchHz,
            frame.TireLrRumblePitchHz,
            frame.TireRrRumblePitchHz).ToArray();
        var rumbleWheelCount = rumblePitches.Count(x => x > 0.1);
        var maxRumblePitch = rumblePitches.Length == 0 ? 0 : rumblePitches.Max();

        var wheelSpeeds = Values(
            frame.LfWheelSpeedMps,
            frame.RfWheelSpeedMps,
            frame.LrWheelSpeedMps,
            frame.RrWheelSpeedMps).Select(Math.Abs).ToArray();
        var averageWheelSpeed = wheelSpeeds.Length == 0 ? frame.SpeedMps : wheelSpeeds.Average();
        var wheelLockLikely = frame.Brake > 0.55f
            && frame.SpeedMps > 8
            && averageWheelSpeed < frame.SpeedMps * 0.58;
        _brakeHoldSeconds = frame.Brake > 0.55f
            ? 0.35
            : Math.Max(0, _brakeHoldSeconds - dt);

        var incidentIncreased = frame.IncidentCount.HasValue
            && _lastIncidentCount.HasValue
            && frame.IncidentCount.Value > _lastIncidentCount.Value;

        var timeInCar = (frame.Timestamp - _enteredCarAt).TotalMilliseconds;
        var result = new ProcessedTelemetry
        {
            Frame = frame,
            DeltaTimeSeconds = dt,
            IsWarm = timeInCar >= 900,
            TimeInCarMilliseconds = Math.Max(0, timeInCar),
            SmoothedLatAccel = _smoothLat,
            SmoothedLongAccel = _smoothLong,
            SmoothedVertAccel = _smoothVert,
            BaselineLatAccel = _baselineLat,
            BaselineLongAccel = _baselineLong,
            BaselineVertAccel = _baselineVert,
            LatDelta = latDelta,
            LongDelta = longDelta,
            VertDelta = vertDelta,
            LatJerk = latJerk,
            LongJerk = longJerk,
            VertJerk = vertJerk,
            SpeedDeltaMps = speedDelta,
            SpeedDecelerationG = speedDecelerationG,
            AngularRateMagnitude = angularRate,
            HorizontalImpulseG = horizontalImpulseG,
            VerticalImpulseG = verticalImpulseG,
            HorizontalJerkGPerSec = horizontalJerkG,
            VerticalJerkGPerSec = verticalJerkG,
            SuspensionVelocityPeakMps = shockPeak,
            SuspensionVelocityAsymmetryMps = shockAsymmetry,
            RumbleStripWheelCount = rumbleWheelCount,
            MaxRumblePitchHz = maxRumblePitch,
            WheelLockLikely = wheelLockLikely,
            BrakeRecentlyActive = _brakeHoldSeconds > 0,
            IncidentIncreased = incidentIncreased
        };

        UpdateSlowBaseline(dt);
        _lastFrame = frame;
        _lastIncidentCount = frame.IncidentCount ?? _lastIncidentCount;
        return result;
    }

    public void Reset()
    {
        _lastFrame = null;
        _initialized = false;
        _lastIncidentCount = null;
        _smoothLat = _smoothLong = _smoothVert = 0;
        _baselineLat = _baselineLong = _baselineVert = 0;
        _brakeHoldSeconds = 0;
    }

    private void Initialize(TelemetryFrame frame)
    {
        _initialized = true;
        _lastFrame = frame;
        _enteredCarAt = frame.Timestamp;
        _smoothLat = _baselineLat = frame.LatAccelMps2;
        _smoothLong = _baselineLong = frame.LongAccelMps2;
        _smoothVert = _baselineVert = frame.VertAccelMps2;
        _lastIncidentCount = frame.IncidentCount;
        _brakeHoldSeconds = frame.Brake > 0.55f ? 0.35 : 0;
    }

    private ProcessedTelemetry BuildInitial(TelemetryFrame frame) =>
        new()
        {
            Frame = frame,
            DeltaTimeSeconds = 1.0 / 60.0,
            IsWarm = false,
            TimeInCarMilliseconds = 0,
            SmoothedLatAccel = frame.LatAccelMps2,
            SmoothedLongAccel = frame.LongAccelMps2,
            SmoothedVertAccel = frame.VertAccelMps2,
            BaselineLatAccel = frame.LatAccelMps2,
            BaselineLongAccel = frame.LongAccelMps2,
            BaselineVertAccel = frame.VertAccelMps2
        };

    private void UpdateSlowBaseline(double dt)
    {
        var slowAlpha = 1.0 - Math.Exp(-dt / 1.25);
        _baselineLat += slowAlpha * Math.Clamp(_smoothLat - _baselineLat, -3.0, 3.0);
        _baselineLong += slowAlpha * Math.Clamp(_smoothLong - _baselineLong, -3.0, 3.0);
        _baselineVert += slowAlpha * Math.Clamp(_smoothVert - _baselineVert, -3.0, 3.0);
    }

    private static IEnumerable<double> Values(params float?[] values)
    {
        foreach (var value in values)
        {
            if (value.HasValue && float.IsFinite(value.Value))
            {
                yield return value.Value;
            }
        }
    }

    private static bool IsFiniteFrame(TelemetryFrame frame) =>
        float.IsFinite(frame.SpeedMps)
        && float.IsFinite(frame.LatAccelMps2)
        && float.IsFinite(frame.LongAccelMps2)
        && float.IsFinite(frame.VertAccelMps2)
        && float.IsFinite(frame.YawRateRadPerSec)
        && float.IsFinite(frame.PitchRateRadPerSec)
        && float.IsFinite(frame.RollRateRadPerSec);
}
