using PSVR2iRacingHaptics.Core.Detection;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public enum TelemetryScenario
{
    Parked,
    NormalAcceleration,
    HardBraking,
    LightKerb,
    StrongKerb,
    WheelDrop,
    Landing,
    SideImpact,
    FrontImpact,
    StrongCollision,
    Rollover,
    ConnectionLoss
}

public static class TelemetryScenarioFactory
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1.0 / 60.0);

    public static IReadOnlyList<TelemetryFrame> Create(
        TelemetryScenario scenario,
        DateTimeOffset? start = null,
        long sequenceStart = 1)
    {
        var timestamp = start ?? DateTimeOffset.UtcNow;
        var frames = new List<TelemetryFrame>();
        var sequence = sequenceStart;
        var incident = 0;
        var speed = scenario == TelemetryScenario.Parked ? 0f : 30f;

        void Add(
            int count,
            Func<int, TelemetryFrame>? transform = null)
        {
            for (var index = 0; index < count; index++)
            {
                var baseline = BaseFrame(timestamp, sequence++, speed, incident);
                var frame = transform?.Invoke(index) ?? baseline;
                frames.Add(frame with
                {
                    Timestamp = timestamp,
                    Sequence = baseline.Sequence
                });
                timestamp += Tick;
                speed = Math.Max(0, frame.SpeedMps);
                incident = frame.IncidentCount ?? incident;
            }
        }

        Add(scenario == TelemetryScenario.Parked ? 15 : 70);

        switch (scenario)
        {
            case TelemetryScenario.Parked:
                Add(30, _ => BaseFrame(timestamp, sequence, 0, incident) with
                {
                    IsOnTrack = false,
                    IsInGarage = true
                });
                break;

            case TelemetryScenario.NormalAcceleration:
                Add(35, _ => BaseFrame(timestamp, sequence, speed + 0.05f, incident) with
                {
                    LongAccelMps2 = 3.0f,
                    Throttle = 0.85f
                });
                break;

            case TelemetryScenario.HardBraking:
                Add(18, index => BaseFrame(
                    timestamp,
                    sequence,
                    Math.Max(5, speed - 0.2f),
                    incident) with
                {
                    LongAccelMps2 = index < 3 ? -12f : -9.5f,
                    Brake = 1.0f,
                    LfWheelSpeedMps = speed * 0.4f,
                    RfWheelSpeedMps = speed * 0.4f,
                    LrWheelSpeedMps = speed * 0.4f,
                    RrWheelSpeedMps = speed * 0.4f
                });
                break;

            case TelemetryScenario.LightKerb:
                Add(24, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    VertAccelMps2 = (float)(TelemetrySignalProcessor.GravityMps2
                        + Math.Sin(index * 1.8) * 3.0),
                    TireRfRumblePitchHz = 28,
                    TireRrRumblePitchHz = 31,
                    RfShockVelocityMps = 0.18f,
                    RrShockVelocityMps = 0.2f,
                    PlayerTrackSurfaceMaterial = 12
                });
                break;

            case TelemetryScenario.StrongKerb:
                Add(18, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    VertAccelMps2 = index is 5 or 10
                        ? 36f
                        : (float)(TelemetrySignalProcessor.GravityMps2
                            + Math.Sin(index * 2.2) * 8),
                    TireRfRumblePitchHz = 42,
                    TireRrRumblePitchHz = 46,
                    RfShockVelocityMps = index is 5 or 10 ? 1.8f : 0.4f,
                    RrShockVelocityMps = index is 5 or 10 ? 1.6f : 0.35f,
                    PlayerTrackSurfaceMaterial = 13
                });
                break;

            case TelemetryScenario.WheelDrop:
                Add(18, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    VertAccelMps2 = index == 8 ? 34f : (float)TelemetrySignalProcessor.GravityMps2,
                    RfShockVelocityMps = index == 8 ? 2.4f : 0.05f,
                    RrShockVelocityMps = 0.08f,
                    LfShockVelocityMps = 0.04f,
                    LrShockVelocityMps = 0.05f
                });
                break;

            case TelemetryScenario.Landing:
                Add(18, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    VertAccelMps2 = index < 14 ? 0.4f : 1.5f,
                    VelocityZMps = -2.3f,
                    LfShockVelocityMps = 0.03f,
                    RfShockVelocityMps = 0.03f,
                    LrShockVelocityMps = 0.03f,
                    RrShockVelocityMps = 0.03f
                });
                Add(5, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    VertAccelMps2 = index == 0 ? 52f : 18f,
                    VelocityZMps = 0.1f,
                    LfShockVelocityMps = index == 0 ? 2.8f : 0.4f,
                    RfShockVelocityMps = index == 0 ? 2.7f : 0.4f,
                    LrShockVelocityMps = index == 0 ? 2.5f : 0.35f,
                    RrShockVelocityMps = index == 0 ? 2.6f : 0.35f
                });
                break;

            case TelemetryScenario.SideImpact:
                Add(10, index => BaseFrame(timestamp, sequence, speed, incident) with
                {
                    LatAccelMps2 = index == 3 ? 56f : 0,
                    YawRateRadPerSec = index == 3 ? 1.3f : 0
                });
                break;

            case TelemetryScenario.FrontImpact:
                Add(10, index => BaseFrame(
                    timestamp,
                    sequence,
                    index >= 3 ? Math.Max(0, speed - 7f) : speed,
                    index >= 3 ? 1 : incident) with
                {
                    LongAccelMps2 = index == 3 ? -72f : 0,
                    PitchRateRadPerSec = index == 3 ? 1.4f : 0
                });
                break;

            case TelemetryScenario.StrongCollision:
                Add(12, index => BaseFrame(
                    timestamp,
                    sequence,
                    index >= 4 ? Math.Max(0, speed - 9f) : speed,
                    index >= 4 ? 2 : incident) with
                {
                    LatAccelMps2 = index == 4 ? 95f : 0,
                    LongAccelMps2 = index == 4 ? -78f : 0,
                    YawRateRadPerSec = index == 4 ? 3.2f : 0
                });
                break;

            case TelemetryScenario.Rollover:
                Add(36, index => BaseFrame(timestamp, sequence, Math.Max(8, speed - 0.25f), incident) with
                {
                    RollRad = Math.Min(2.4f, index * 0.11f),
                    RollRateRadPerSec = index < 24 ? 4.2f : 1.8f,
                    VertAccelMps2 = index % 8 == 4 ? 48f : (float)TelemetrySignalProcessor.GravityMps2,
                    LatAccelMps2 = index % 8 == 4 ? 42f : 0
                });
                break;

            case TelemetryScenario.ConnectionLoss:
                Add(20, _ => TelemetryFrame.Disconnected(timestamp));
                break;
        }

        Add(35);
        return frames;
    }

    private static TelemetryFrame BaseFrame(
        DateTimeOffset timestamp,
        long sequence,
        float speed,
        int incident) =>
        new()
        {
            Timestamp = timestamp,
            Sequence = sequence,
            IsConnected = true,
            IsValid = true,
            IsOnTrack = true,
            IsOnTrackCar = true,
            IsInGarage = false,
            IsReplayPlaying = false,
            SessionState = 4,
            SpeedMps = speed,
            VertAccelMps2 = (float)TelemetrySignalProcessor.GravityMps2,
            IncidentCount = incident,
            LfWheelSpeedMps = speed,
            RfWheelSpeedMps = speed,
            LrWheelSpeedMps = speed,
            RrWheelSpeedMps = speed,
            LfShockVelocityMps = 0.05f,
            RfShockVelocityMps = 0.05f,
            LrShockVelocityMps = 0.05f,
            RrShockVelocityMps = 0.05f
        };
}
