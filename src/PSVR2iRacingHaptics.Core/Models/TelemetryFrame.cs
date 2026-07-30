namespace PSVR2iRacingHaptics.Core.Models;

/// <summary>
/// Normalized snapshot of the player's car telemetry.
/// Units follow the iRacing SDK: m/s, m/s², rad and rad/s.
/// </summary>
public sealed record TelemetryFrame
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public long Sequence { get; init; }
    public bool IsConnected { get; init; }
    public bool IsValid { get; init; }
    public bool IsOnTrack { get; init; }
    public bool IsOnTrackCar { get; init; }
    public bool IsInGarage { get; init; }
    public bool IsReplayPlaying { get; init; }
    public int SessionState { get; init; }
    public int EnterExitReset { get; init; }
    public TelemetryContext Context { get; init; } = new();

    public float SpeedMps { get; init; }
    public float LatAccelMps2 { get; init; }
    public float LongAccelMps2 { get; init; }
    public float VertAccelMps2 { get; init; }
    public float VelocityXMps { get; init; }
    public float VelocityYMps { get; init; }
    public float VelocityZMps { get; init; }

    public float YawRad { get; init; }
    public float PitchRad { get; init; }
    public float RollRad { get; init; }
    public float YawRateRadPerSec { get; init; }
    public float PitchRateRadPerSec { get; init; }
    public float RollRateRadPerSec { get; init; }

    public float Brake { get; init; }
    public float Throttle { get; init; }
    public int Gear { get; init; }
    public float Rpm { get; init; }
    public int? IncidentCount { get; init; }
    public int? PlayerTrackSurface { get; init; }
    public int? PlayerTrackSurfaceMaterial { get; init; }

    public float? LfWheelSpeedMps { get; init; }
    public float? RfWheelSpeedMps { get; init; }
    public float? LrWheelSpeedMps { get; init; }
    public float? RrWheelSpeedMps { get; init; }

    public float? LfShockDeflectionM { get; init; }
    public float? RfShockDeflectionM { get; init; }
    public float? LrShockDeflectionM { get; init; }
    public float? RrShockDeflectionM { get; init; }
    public float? LfShockVelocityMps { get; init; }
    public float? RfShockVelocityMps { get; init; }
    public float? LrShockVelocityMps { get; init; }
    public float? RrShockVelocityMps { get; init; }

    public float? TireLfRumblePitchHz { get; init; }
    public float? TireRfRumblePitchHz { get; init; }
    public float? TireLrRumblePitchHz { get; init; }
    public float? TireRrRumblePitchHz { get; init; }

    public bool IsDriverInCar =>
        IsConnected && IsValid && IsOnTrack && !IsReplayPlaying && !IsInGarage;

    public static TelemetryFrame Disconnected(DateTimeOffset? timestamp = null) =>
        new()
        {
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            IsConnected = false,
            IsValid = false,
            Context = new TelemetryContext()
        };
}
