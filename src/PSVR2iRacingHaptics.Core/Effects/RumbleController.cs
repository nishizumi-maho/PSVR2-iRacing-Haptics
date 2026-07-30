using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Core.Effects;

public sealed class RumbleController : IAsyncDisposable
{
    private readonly IHmdRumbleDevice _device;
    private readonly SafetySettings _safety;
    private readonly IAppLogger _logger;
    private readonly bool _disposeDevice;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _deviceGate = new(1, 1);
    private readonly Queue<DateTimeOffset> _nonZeroCallTimes = new();
    private CancellationTokenSource? _activeCancellation;
    private Task _activeTask = Task.CompletedTask;
    private long _generation;
    private int _activePriority = int.MinValue;
    private string _activeEffect = string.Empty;
    private bool _enabled = true;
    private bool _disposed;
    private byte _lastFrequency;
    private int _lastDuration;
    private string _lastAction = "Waiting";

    public RumbleController(
        IHmdRumbleDevice device,
        SafetySettings safety,
        IAppLogger? logger = null,
        bool disposeDevice = true)
    {
        _device = device;
        _safety = safety;
        _logger = logger ?? NullAppLogger.Instance;
        _disposeDevice = disposeDevice;
    }

    public event EventHandler<RumbleControllerStatus>? StatusChanged;

    public bool IsEnabled
    {
        get
        {
            lock (_stateLock)
            {
                return _enabled && !_disposed;
            }
        }
    }

    public Task<bool> TryPlayAsync(
        RumbleEffect effect,
        CancellationToken cancellationToken = default)
    {
        if (effect.Pulses.Count == 0)
        {
            return Task.FromResult(false);
        }

        lock (_stateLock)
        {
            if (_disposed || !_enabled || !_device.IsAvailable)
            {
                return Task.FromResult(false);
            }

            if (!_activeTask.IsCompleted && effect.Priority <= _activePriority)
            {
                _logger.Info(
                    $"Effect ignored because of priority: {effect.Name} ({effect.Priority}) "
                    + $"≤ {_activeEffect} ({_activePriority}).");
                return Task.FromResult(false);
            }

            var previous = _activeTask;
            _activeCancellation?.Cancel();
            _activeCancellation?.Dispose();
            _activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

            var generation = ++_generation;
            _activePriority = effect.Priority;
            _activeEffect = effect.Name;
            _lastAction = previous.IsCompleted
                ? $"Starting {effect.Name}"
                : $"Replacing current effect with {effect.Name}";
            PublishStatusLocked();

            _activeTask = RunAfterPreviousAsync(
                previous,
                effect,
                generation,
                _activeCancellation.Token);
            return Task.FromResult(true);
        }
    }

    public async Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            _enabled = enabled;
            _lastAction = enabled ? "Haptics enabled" : "Haptics disabled";
            PublishStatusLocked();
        }

        if (!enabled)
        {
            await EmergencyStopAsync("haptics disabled", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task EmergencyStopAsync(
        string reason = "immediate stop",
        CancellationToken cancellationToken = default)
    {
        Task active;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _activeCancellation?.Cancel();
            active = _activeTask;
            _lastAction = $"Stopped: {reason}";
            PublishStatusLocked();
        }

        try
        {
            await active.WaitAsync(TimeSpan.FromMilliseconds(
                _safety.NativeCallTimeoutMs + 500), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is OperationCanceledException or TimeoutException or InvalidOperationException)
        {
            _logger.Warning($"Effect did not stop normally during {reason}: {ex.Message}");
        }

        await SendOffBestEffortAsync(reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAfterPreviousAsync(
        Task previous,
        RumbleEffect effect,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch
            {
                // The previous task already performed its own shutdown and logging.
            }

            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteEffectAsync(effect, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Info($"Effect canceled: {effect.Name}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Effect {effect.Name} failed; shutdown requested.", ex);
        }
        finally
        {
            await SendOffBestEffortAsync($"end of {effect.Name}", CancellationToken.None)
                .ConfigureAwait(false);
            lock (_stateLock)
            {
                if (_generation == generation)
                {
                    _activePriority = int.MinValue;
                    _activeEffect = string.Empty;
                    _lastFrequency = 0;
                    _lastDuration = 0;
                    _lastAction = $"Completed: {effect.Name}";
                    PublishStatusLocked();
                }
            }
        }
    }

    private async Task ExecuteEffectAsync(
        RumbleEffect effect,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        _logger.Info(
            $"Effect accepted: {effect.Name}; priority={effect.Priority}; "
            + $"planned duration={effect.TotalDurationMs} ms.");

        foreach (var pulse in effect.Pulses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsed = (DateTimeOffset.UtcNow - started).TotalMilliseconds;
            if (elapsed >= _safety.MaximumEffectDurationMs)
            {
                _logger.Warning($"Effect {effect.Name} reached the maximum duration.");
                break;
            }

            var frequency = (byte)Math.Clamp(pulse.FrequencyHz, (byte)0, (byte)25);
            var duration = Math.Min(
                Math.Max(10, pulse.DurationMs),
                _safety.MaximumContinuousRumbleMs);
            duration = Math.Min(
                duration,
                Math.Max(0, _safety.MaximumEffectDurationMs - (int)elapsed));
            if (frequency == 0 || duration <= 0)
            {
                continue;
            }

            await SendFrequencyAsync(frequency, cancellationToken).ConfigureAwait(false);
            lock (_stateLock)
            {
                _lastFrequency = frequency;
                _lastDuration = duration;
                _lastAction = $"Rumble: {frequency} Hz for {duration} ms";
                PublishStatusLocked();
            }
            _logger.Info($"Rumble: {frequency} Hz for {duration} ms");

            try
            {
                await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await SendOffBestEffortAsync("end of pulse", CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (pulse.PauseAfterMs > 0)
            {
                var pause = Math.Min(pulse.PauseAfterMs, 1000);
                _logger.Info($"Pause: {pause} ms");
                await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendFrequencyAsync(
        byte frequency,
        CancellationToken cancellationToken)
    {
        await _deviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (frequency > 0)
            {
                await ApplyRateLimitAsync(cancellationToken).ConfigureAwait(false);
            }

            await _device.SetFrequencyAsync(frequency, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceGate.Release();
        }
    }

    private async Task ApplyRateLimitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = DateTimeOffset.UtcNow;
            while (_nonZeroCallTimes.Count > 0
                   && (now - _nonZeroCallTimes.Peek()).TotalSeconds >= 1)
            {
                _nonZeroCallTimes.Dequeue();
            }

            if (_nonZeroCallTimes.Count < _safety.MaximumCallsPerSecond)
            {
                _nonZeroCallTimes.Enqueue(now);
                return;
            }

            var wait = TimeSpan.FromSeconds(1)
                - (now - _nonZeroCallTimes.Peek());
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendOffBestEffortAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _deviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_device.IsAvailable)
                {
                    await _device.SetFrequencyAsync(0, cancellationToken).ConfigureAwait(false);
                    _logger.Info($"Rumble: OFF ({reason}).");
                }
            }
            finally
            {
                _deviceGate.Release();
            }
        }
        catch (Exception ex) when (
            ex is OperationCanceledException or InvalidOperationException or TimeoutException)
        {
            _logger.Warning($"Could not confirm Rumble OFF ({reason}): {ex.Message}");
        }
    }

    private void PublishStatusLocked()
    {
        var status = new RumbleControllerStatus(
            _enabled,
            !_activeTask.IsCompleted,
            _activeEffect,
            _activePriority,
            _lastFrequency,
            _lastDuration,
            _lastAction);
        StatusChanged?.Invoke(this, status);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }
        }

        await EmergencyStopAsync("shutdown").ConfigureAwait(false);
        lock (_stateLock)
        {
            _disposed = true;
            _enabled = false;
            _activeCancellation?.Dispose();
            _activeCancellation = null;
        }

        _deviceGate.Dispose();
        if (_disposeDevice)
        {
            await _device.DisposeAsync().ConfigureAwait(false);
        }
    }
}
