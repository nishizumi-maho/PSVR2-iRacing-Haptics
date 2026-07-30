namespace PSVR2iRacingHaptics.Core.Abstractions;

public interface IHmdRumbleDevice : IAsyncDisposable
{
    bool IsAvailable { get; }
    string StatusDescription { get; }

    Task SetFrequencyAsync(
        byte frequencyHz,
        CancellationToken cancellationToken);
}
