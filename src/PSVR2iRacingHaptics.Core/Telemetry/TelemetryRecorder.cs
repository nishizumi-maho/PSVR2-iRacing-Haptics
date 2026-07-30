using System.Text.Json;
using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public sealed record TelemetryLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string EntryType { get; init; } = "frame";
    public TelemetryFrame? Frame { get; init; }
    public string? Marker { get; init; }
    public HapticEventKind? DetectedKind { get; init; }
    public EventSeverity? DetectedSeverity { get; init; }
    public double? DetectedScore { get; init; }
    public string? DetectionReason { get; init; }
}

public sealed class TelemetryRecorder : IAsyncDisposable
{
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private StreamWriter? _writer;
    private TelemetryFrame? _latestFrame;

    public TelemetryRecorder(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsRecording => _writer is not null;
    public string? CurrentPath { get; private set; }

    public async Task StartAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_writer is not null)
            {
                throw new InvalidOperationException("A recording is already in progress.");
            }

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("Invalid recording path."));
            _writer = new StreamWriter(new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous));
            CurrentPath = fullPath;
            _logger.Info($"Telemetry recording started: {fullPath}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordFrameAsync(
        TelemetryFrame frame,
        DetectedHapticEvent? detectedEvent = null,
        CancellationToken cancellationToken = default)
    {
        _latestFrame = frame;
        if (_writer is null)
        {
            return;
        }

        await WriteEntryAsync(
            new TelemetryLogEntry
            {
                Timestamp = frame.Timestamp,
                EntryType = "frame",
                Frame = frame,
                DetectedKind = detectedEvent?.Kind,
                DetectedSeverity = detectedEvent?.Severity,
                DetectedScore = detectedEvent?.Score,
                DetectionReason = detectedEvent?.Reason
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task MarkAsync(
        string marker,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            throw new ArgumentException("The marker cannot be empty.", nameof(marker));
        }

        var frame = _latestFrame;
        return WriteEntryAsync(
            new TelemetryLogEntry
            {
                Timestamp = frame?.Timestamp ?? DateTimeOffset.UtcNow,
                EntryType = "marker",
                Frame = frame,
                Marker = marker.Trim()
            },
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_writer is null)
            {
                return;
            }

            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
            _logger.Info($"Telemetry recording stopped: {CurrentPath}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteEntryAsync(
        TelemetryLogEntry entry,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_writer is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
