using System.Text.Json;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

/// <summary>
/// Keeps a bounded in-memory history so the user can save the seconds preceding
/// an event. No disk writes occur until SaveSnapshotAsync is requested.
/// </summary>
public sealed class TelemetryCircularBuffer
{
    private const int MaximumEntries = 100_000;
    private readonly object _gate = new();
    private readonly Queue<TelemetryLogEntry> _entries = new();
    private int _retentionSeconds;

    public TelemetryCircularBuffer(int retentionSeconds = 60)
    {
        _retentionSeconds = Math.Clamp(retentionSeconds, 10, 300);
    }

    public int RetentionSeconds
    {
        get
        {
            lock (_gate)
            {
                return _retentionSeconds;
            }
        }
        set
        {
            lock (_gate)
            {
                _retentionSeconds = Math.Clamp(value, 10, 300);
                Trim(DateTimeOffset.UtcNow);
            }
        }
    }

    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public void AddFrame(
        TelemetryFrame frame,
        DetectedHapticEvent? detectedEvent = null)
    {
        lock (_gate)
        {
            _entries.Enqueue(new TelemetryLogEntry
            {
                Timestamp = frame.Timestamp,
                EntryType = "frame",
                Frame = frame,
                DetectedKind = detectedEvent?.Kind,
                DetectedSeverity = detectedEvent?.Severity,
                DetectedScore = detectedEvent?.Score,
                DetectionReason = detectedEvent?.Reason
            });
            Trim(frame.Timestamp);
            while (_entries.Count > MaximumEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public async Task<int> SaveSnapshotAsync(
        string path,
        string marker = "circular buffer saved",
        CancellationToken cancellationToken = default)
    {
        TelemetryLogEntry[] snapshot;
        lock (_gate)
        {
            snapshot = _entries.ToArray();
        }
        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "The circular buffer does not contain telemetry yet.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Invalid recording path."));
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        await using var writer = new StreamWriter(new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous));
        foreach (var entry in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(entry, options);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }
        var last = snapshot[^1];
        var markerEntry = new TelemetryLogEntry
        {
            Timestamp = last.Timestamp,
            EntryType = "marker",
            Frame = last.Frame,
            Marker = marker
        };
        await writer.WriteLineAsync(
            JsonSerializer.Serialize(markerEntry, options).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Length;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private void Trim(DateTimeOffset newestTimestamp)
    {
        var cutoff = newestTimestamp - TimeSpan.FromSeconds(_retentionSeconds);
        while (_entries.Count > 0 && _entries.Peek().Timestamp < cutoff)
        {
            _entries.Dequeue();
        }
    }
}
