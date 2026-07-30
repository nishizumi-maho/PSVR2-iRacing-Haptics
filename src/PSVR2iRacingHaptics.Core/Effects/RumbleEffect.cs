namespace PSVR2iRacingHaptics.Core.Effects;

public sealed record RumblePulse(byte FrequencyHz, int DurationMs, int PauseAfterMs = 0);

public sealed record RumbleEffect(
    string Name,
    int Priority,
    IReadOnlyList<RumblePulse> Pulses)
{
    public int TotalDurationMs =>
        Pulses.Sum(x => Math.Max(0, x.DurationMs) + Math.Max(0, x.PauseAfterMs));
}

public sealed record RumbleControllerStatus(
    bool Enabled,
    bool IsPlaying,
    string ActiveEffect,
    int ActivePriority,
    byte LastFrequencyHz,
    int LastDurationMs,
    string LastAction);
