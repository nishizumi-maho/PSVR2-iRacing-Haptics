using System.Collections.Concurrent;
using PSVR2iRacingHaptics.Core.Abstractions;

namespace PSVR2iRacingHaptics.Core.Devices;

public sealed record SimulatedRumbleCommand(DateTimeOffset Timestamp, byte FrequencyHz);

public sealed class SimulatedRumbleDevice : IHmdRumbleDevice
{
    private readonly ConcurrentQueue<SimulatedRumbleCommand> _commands = new();
    private readonly IAppLogger _logger;
    private volatile bool _isAvailable = true;

    public SimulatedRumbleDevice(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsAvailable => _isAvailable;
    public string StatusDescription =>
        _isAvailable ? "Simulated rumble device available" : "Simulated connection loss";
    public IReadOnlyCollection<SimulatedRumbleCommand> Commands => _commands.ToArray();

    public void SetAvailable(bool available)
    {
        _isAvailable = available;
        _logger.Info(available
            ? "Simulated rumble device reconnected."
            : "Simulated rumble device disconnected.");
    }

    public Task SetFrequencyAsync(byte frequencyHz, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isAvailable)
        {
            throw new InvalidOperationException("The simulated device is unavailable.");
        }

        _commands.Enqueue(new SimulatedRumbleCommand(DateTimeOffset.UtcNow, frequencyHz));
        _logger.Info(frequencyHz == 0
            ? "SIMULATOR — Rumble: OFF"
            : $"SIMULATOR — Rumble: {frequencyHz} Hz");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _isAvailable = false;
        return ValueTask.CompletedTask;
    }
}
