using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Detection;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public sealed record CalibrationMatch(
    DateTimeOffset MarkerTimestamp,
    string Marker,
    HapticEventKind? MatchedDetection,
    double? Score,
    double DistanceMilliseconds);

public sealed record CalibrationReport(
    int MarkerCount,
    int MatchedCount,
    int MissedCount,
    int UnmarkedDetectionCount,
    IReadOnlyList<CalibrationMatch> Matches);

public static class CalibrationAnalyzer
{
    public static async Task<CalibrationReport> AnalyzeAsync(
        string path,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var pipeline = new HapticDetectionPipeline();
        var markers = new List<TelemetryLogEntry>();
        var detections = new List<DetectedHapticEvent>();

        await foreach (var entry in TelemetryReplayClient.ReadEntriesAsync(
            path,
            cancellationToken))
        {
            if (entry.EntryType == "marker" && !string.IsNullOrWhiteSpace(entry.Marker))
            {
                markers.Add(entry);
            }

            if (entry.EntryType == "frame" && entry.Frame is not null)
            {
                var result = pipeline.Process(entry.Frame, settings);
                if (result.SelectedEvent is not null)
                {
                    detections.Add(result.SelectedEvent);
                }
            }
        }

        var usedDetections = new HashSet<int>();
        var matches = new List<CalibrationMatch>();
        foreach (var marker in markers)
        {
            var expectedKinds = ExpectedKinds(marker.Marker!);
            var best = detections
                .Select((detection, index) => new
                {
                    Detection = detection,
                    Index = index,
                    Distance = Math.Abs(
                        (detection.Timestamp - marker.Timestamp).TotalMilliseconds)
                })
                .Where(x => !usedDetections.Contains(x.Index)
                            && x.Distance <= 500
                            && expectedKinds.Contains(x.Detection.Kind))
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (best is not null)
            {
                usedDetections.Add(best.Index);
            }

            matches.Add(new CalibrationMatch(
                marker.Timestamp,
                marker.Marker!,
                best?.Detection.Kind,
                best?.Detection.Score,
                best?.Distance ?? double.PositiveInfinity));
        }

        var matched = matches.Count(x => x.MatchedDetection.HasValue);
        return new CalibrationReport(
            markers.Count,
            matched,
            markers.Count - matched,
            Math.Max(0, detections.Count - usedDetections.Count),
            matches);
    }

    private static IReadOnlySet<HapticEventKind> ExpectedKinds(string marker)
    {
        if (marker.Contains("zebra", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.StrongKerb };
        }

        if (marker.Contains("pouso", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind>
            {
                HapticEventKind.Landing,
                HapticEventKind.SevereVerticalCompression
            };
        }

        return new HashSet<HapticEventKind>
        {
            HapticEventKind.LightImpact,
            HapticEventKind.MediumImpact,
            HapticEventKind.StrongImpact,
            HapticEventKind.RolloverImpact
        };
    }
}
