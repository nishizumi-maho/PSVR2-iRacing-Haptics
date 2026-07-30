using System.Text.Json;
using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public sealed class TelemetryReplayClient : ITelemetryClient
{
    private readonly string _path;
    private readonly double _speedMultiplier;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _lifetime;
    private Task _replayTask = Task.CompletedTask;

    public TelemetryReplayClient(
        string path,
        double speedMultiplier = 1.0,
        IAppLogger? logger = null)
    {
        _path = Path.GetFullPath(path);
        _speedMultiplier = Math.Clamp(speedMultiplier, 0.1, 20);
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsConnected { get; private set; }
    public string StatusDescription => IsConnected
        ? $"Reproduzindo {Path.GetFileName(_path)}"
        : "Replay parado";
    public event EventHandler<TelemetryFrame>? FrameReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_lifetime is not null)
        {
            return Task.CompletedTask;
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _replayTask = ReplayAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    private async Task ReplayAsync(CancellationToken cancellationToken)
    {
        IsConnected = true;
        ConnectionChanged?.Invoke(this, true);
        _logger.Info($"Replay iniciado: {_path}; velocidade={_speedMultiplier:F1}x.");
        DateTimeOffset? previousTimestamp = null;

        try
        {
            await foreach (var entry in ReadEntriesAsync(_path, cancellationToken))
            {
                if (entry.Frame is null || entry.EntryType != "frame")
                {
                    continue;
                }

                if (previousTimestamp.HasValue)
                {
                    var delay = (entry.Timestamp - previousTimestamp.Value).TotalMilliseconds
                        / _speedMultiplier;
                    if (delay > 0)
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(Math.Min(delay, 1000)),
                            cancellationToken).ConfigureAwait(false);
                    }
                }

                FrameReceived?.Invoke(this, entry.Frame);
                previousTimestamp = entry.Timestamp;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(this, false);
            _logger.Info("Replay encerrado.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var lifetime = Interlocked.Exchange(ref _lifetime, null);
        if (lifetime is null)
        {
            return;
        }

        lifetime.Cancel();
        try
        {
            await _replayTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
        }
        lifetime.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public static async IAsyncEnumerable<TelemetryLogEntry> ReadEntriesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            TelemetryLogEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<TelemetryLogEntry>(
                    line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is not null)
            {
                yield return entry;
            }
        }
    }
}
