using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public sealed class TelemetrySimulator : ITelemetryClient
{
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _scenarioGate = new(1, 1);
    private CancellationTokenSource? _lifetimeCancellation;
    private Task _idleLoop = Task.CompletedTask;
    private long _sequence;

    public TelemetrySimulator(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsConnected { get; private set; }
    public string StatusDescription => IsConnected
        ? "Telemetry simulator connected"
        : "Telemetry simulator stopped";

    public event EventHandler<TelemetryFrame>? FrameReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_lifetimeCancellation is not null)
        {
            return Task.CompletedTask;
        }

        _lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsConnected = true;
        ConnectionChanged?.Invoke(this, true);
        _logger.Info("Telemetry simulator started.");
        _idleLoop = IdleLoopAsync(_lifetimeCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task PlayScenarioAsync(
        TelemetryScenario scenario,
        CancellationToken cancellationToken = default)
    {
        if (_lifetimeCancellation is null)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }

        await _scenarioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.Info($"Scenario started: {scenario}.");
            var frames = TelemetryScenarioFactory.Create(
                scenario,
                DateTimeOffset.UtcNow,
                Interlocked.Read(ref _sequence) + 1);
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Exchange(ref _sequence, Math.Max(_sequence, frame.Sequence));
                var connected = frame.IsConnected;
                if (IsConnected != connected)
                {
                    IsConnected = connected;
                    ConnectionChanged?.Invoke(this, connected);
                }

                FrameReceived?.Invoke(this, frame);
                await Task.Delay(TimeSpan.FromSeconds(1.0 / 60.0), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!IsConnected)
            {
                IsConnected = true;
                ConnectionChanged?.Invoke(this, true);
            }
            _logger.Info($"Scenario completed: {scenario}.");
        }
        finally
        {
            _scenarioGate.Release();
        }
    }

    private async Task IdleLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_scenarioGate.Wait(0))
                {
                    try
                    {
                        var frame = TelemetryScenarioFactory.Create(
                            TelemetryScenario.Parked,
                            DateTimeOffset.UtcNow,
                            Interlocked.Increment(ref _sequence))[0];
                        FrameReceived?.Invoke(this, frame);
                    }
                    finally
                    {
                        _scenarioGate.Release();
                    }
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var lifetime = Interlocked.Exchange(ref _lifetimeCancellation, null);
        if (lifetime is null)
        {
            return;
        }

        lifetime.Cancel();
        try
        {
            await _idleLoop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
        }
        lifetime.Dispose();
        IsConnected = false;
        ConnectionChanged?.Invoke(this, false);
        _logger.Info("Telemetry simulator stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _scenarioGate.Dispose();
    }
}
