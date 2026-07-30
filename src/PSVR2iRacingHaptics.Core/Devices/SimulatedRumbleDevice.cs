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
        _isAvailable ? "Dispositivo simulado disponível" : "Perda simulada de conexão";
    public IReadOnlyCollection<SimulatedRumbleCommand> Commands => _commands.ToArray();

    public void SetAvailable(bool available)
    {
        _isAvailable = available;
        _logger.Info(available
            ? "Dispositivo de vibração simulado reconectado."
            : "Dispositivo de vibração simulado desconectado.");
    }

    public Task SetFrequencyAsync(byte frequencyHz, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isAvailable)
        {
            throw new InvalidOperationException("O dispositivo simulado está indisponível.");
        }

        _commands.Enqueue(new SimulatedRumbleCommand(DateTimeOffset.UtcNow, frequencyHz));
        _logger.Info(frequencyHz == 0
            ? "SIMULADOR — Rumble: OFF"
            : $"SIMULADOR — Rumble: {frequencyHz} Hz");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _isAvailable = false;
        return ValueTask.CompletedTask;
    }
}
