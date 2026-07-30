using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Detection;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public sealed record CalibrationMatch(
    DateTimeOffset MarkerTimestamp,
    string Marker,
    HapticEventKind? MatchedDetection,
    double? Score,
    double DistanceMilliseconds)
{
    public double? PeakRelevantScore { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

public sealed record CalibrationRecommendation(
    string SettingPath,
    double CurrentValue,
    double SuggestedValue,
    string Direction,
    string Reason,
    bool CanApply = true);

public sealed record TriggerConditionStatistics(
    int ConditionIndex,
    TelemetrySignal Signal,
    string Unit,
    int SampleCount,
    int MissingSampleCount,
    double? Minimum,
    double? Maximum,
    double? Median,
    double? Percentile95,
    double? Percentile99,
    double? MarkerWindowMinimum,
    double? MarkerWindowMaximum);

public sealed record TriggerCalibrationSummary(
    string TriggerId,
    string TriggerName,
    HapticEventKind TargetEvent,
    int FrameCount,
    int MatchingFrameCount,
    int FiredCount,
    int MatchedButSuppressedCount,
    IReadOnlyList<TriggerConditionStatistics> Conditions);

public sealed record CalibrationReport(
    int MarkerCount,
    int MatchedCount,
    int MissedCount,
    int UnmarkedDetectionCount,
    IReadOnlyList<CalibrationMatch> Matches)
{
    public IReadOnlyList<CalibrationRecommendation> Recommendations { get; init; } =
        Array.Empty<CalibrationRecommendation>();
    public IReadOnlyList<TriggerCalibrationSummary> TriggerSummaries { get; init; } =
        Array.Empty<TriggerCalibrationSummary>();
}

/// <summary>
/// Replays a recording with the current profile, compares human markers to
/// detector output and proposes small, bounded threshold adjustments.
/// </summary>
public static class CalibrationAnalyzer
{
    private const int LookBehindMilliseconds = 2000;
    private const int LookAheadMilliseconds = 250;

    public static async Task<CalibrationReport> AnalyzeAsync(
        string path,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var pipeline = new HapticDetectionPipeline();
        var triggerDryRunPipeline = settings.Triggers.Enabled
            ? null
            : new HapticDetectionPipeline();
        var triggerDryRunSettings = settings.Triggers.Enabled
            ? settings
            : settings.DeepClone();
        triggerDryRunSettings.Triggers.Enabled = true;
        var markers = new List<TelemetryLogEntry>();
        var detections = new List<DetectedHapticEvent>();
        var diagnostics = new List<ProcessedTelemetry>();
        var triggerFrames = new List<TriggerFrameSample>();

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
                var frame = entry.Frame.IsReplayPlaying
                    ? entry.Frame with { AllowDetectionDuringReplay = true }
                    : entry.Frame;
                var result = pipeline.Process(frame, settings);
                diagnostics.Add(result.Diagnostics);
                detections.AddRange(result.Candidates);
                var triggerEvaluations = triggerDryRunPipeline is null
                    ? result.TriggerEvaluations
                    : triggerDryRunPipeline.Process(
                        frame,
                        triggerDryRunSettings).TriggerEvaluations;
                triggerFrames.AddRange(triggerEvaluations.Select(evaluation =>
                    new TriggerFrameSample(frame.Timestamp, evaluation)));
            }
        }

        var usedDetections = new HashSet<int>();
        var matches = new List<CalibrationMatch>();
        var rawRecommendations = new List<CalibrationRecommendation>();
        foreach (var marker in markers)
        {
            var markerText = marker.Marker!;
            var expectedKinds = ExpectedKinds(markerText);
            var best = detections
                .Select((detection, index) => new
                {
                    Detection = detection,
                    Index = index,
                    Delta = (detection.Timestamp - marker.Timestamp).TotalMilliseconds
                })
                .Where(candidate =>
                    !usedDetections.Contains(candidate.Index)
                    && candidate.Delta >= -LookBehindMilliseconds
                    && candidate.Delta <= LookAheadMilliseconds
                    && expectedKinds.Contains(candidate.Detection.Kind))
                .OrderBy(candidate => Math.Abs(candidate.Delta))
                .FirstOrDefault();

            if (best is not null)
            {
                usedDetections.Add(best.Index);
            }

            var settingPath = ThresholdSetting(markerText, best?.Detection.Kind);
            var peak = settingPath is null
                ? null
                : PeakRelevantScore(diagnostics, marker.Timestamp, settingPath);
            var explanation = best is not null
                ? $"Matched {best.Detection.Kind} "
                    + $"{Math.Abs(best.Delta):F0} ms from the marker."
                : settingPath is null
                    ? "No compatible event was detected; this marker has no threshold "
                        + "that can be adjusted automatically."
                    : $"No compatible event was detected. Peak relevant score was "
                        + $"{peak.GetValueOrDefault():F2}.";

            matches.Add(new CalibrationMatch(
                marker.Timestamp,
                markerText,
                best?.Detection.Kind,
                best?.Detection.Score,
                best is null ? double.PositiveInfinity : Math.Abs(best.Delta))
            {
                PeakRelevantScore = peak,
                Explanation = explanation
            });

            var recommendation = BuildRecommendation(
                markerText,
                settingPath,
                best?.Detection,
                peak,
                settings);
            if (recommendation is not null)
            {
                rawRecommendations.Add(recommendation);
            }
        }

        var recommendations = Consolidate(rawRecommendations);
        var triggerSummaries = BuildTriggerSummaries(
            settings,
            triggerFrames,
            markers);
        var matched = matches.Count(match => match.MatchedDetection.HasValue);
        return new CalibrationReport(
            markers.Count,
            matched,
            markers.Count - matched,
            Math.Max(0, detections.Count - usedDetections.Count),
            matches)
        {
            Recommendations = recommendations,
            TriggerSummaries = triggerSummaries
        };
    }

    private static IReadOnlyList<TriggerCalibrationSummary> BuildTriggerSummaries(
        AppSettings settings,
        IReadOnlyList<TriggerFrameSample> frames,
        IReadOnlyList<TelemetryLogEntry> markers)
    {
        var summaries = new List<TriggerCalibrationSummary>();
        foreach (var trigger in settings.Triggers.CustomTriggers.Where(trigger =>
                     trigger.Enabled))
        {
            var triggerFrames = frames
                .Where(frame => frame.Evaluation.TriggerId.Equals(
                    trigger.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var compatibleMarkers = markers
                .Where(marker =>
                    !string.IsNullOrWhiteSpace(marker.Marker)
                    && ExpectedKinds(marker.Marker!).Contains(trigger.TargetEvent))
                .Select(marker => marker.Timestamp)
                .ToArray();
            var conditionStatistics = new List<TriggerConditionStatistics>();
            for (var conditionIndex = 0;
                 conditionIndex < trigger.Conditions.Count;
                 conditionIndex++)
            {
                var condition = trigger.Conditions[conditionIndex];
                var samples = triggerFrames
                    .Select(frame => frame.Evaluation.Conditions
                        .ElementAtOrDefault(conditionIndex)?.ObservedValue)
                    .Where(value => value.HasValue && double.IsFinite(value.Value))
                    .Select(value => value!.Value)
                    .Order()
                    .ToArray();
                var markerSamples = triggerFrames
                    .Where(frame => compatibleMarkers.Any(marker =>
                    {
                        var delta = (frame.Timestamp - marker).TotalMilliseconds;
                        return delta >= -LookBehindMilliseconds
                            && delta <= LookAheadMilliseconds;
                    }))
                    .Select(frame => frame.Evaluation.Conditions
                        .ElementAtOrDefault(conditionIndex)?.ObservedValue)
                    .Where(value => value.HasValue && double.IsFinite(value.Value))
                    .Select(value => value!.Value)
                    .Order()
                    .ToArray();
                var descriptor = TelemetrySignalCatalog.Describe(condition.Signal);
                conditionStatistics.Add(new TriggerConditionStatistics(
                    conditionIndex,
                    condition.Signal,
                    descriptor.Unit,
                    samples.Length,
                    Math.Max(0, triggerFrames.Length - samples.Length),
                    samples.Length == 0 ? null : samples[0],
                    samples.Length == 0 ? null : samples[^1],
                    Percentile(samples, 0.50),
                    Percentile(samples, 0.95),
                    Percentile(samples, 0.99),
                    markerSamples.Length == 0 ? null : markerSamples[0],
                    markerSamples.Length == 0 ? null : markerSamples[^1]));
            }

            summaries.Add(new TriggerCalibrationSummary(
                trigger.Id,
                trigger.Name,
                trigger.TargetEvent,
                triggerFrames.Length,
                triggerFrames.Count(frame => frame.Evaluation.ConditionsMatched),
                triggerFrames.Count(frame => frame.Evaluation.Fired),
                triggerFrames.Count(frame =>
                    frame.Evaluation.ConditionsMatched && !frame.Evaluation.Fired),
                conditionStatistics));
        }
        return summaries;
    }

    private static double? Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return null;
        }
        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position = Math.Clamp(percentile, 0, 1) * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }
        var fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }

    private static CalibrationRecommendation? BuildRecommendation(
        string marker,
        string? settingPath,
        DetectedHapticEvent? matched,
        double? peak,
        AppSettings settings)
    {
        if (settingPath is null)
        {
            return null;
        }

        var current = CurrentThreshold(settingPath, settings);
        var falsePositive = marker.Contains(
            "false positive",
            StringComparison.OrdinalIgnoreCase);
        if (falsePositive && matched is not null)
        {
            var suggestion = Math.Round(
                Math.Clamp(Math.Max(current + 0.05, matched.Score * 1.08), 0.2, 40),
                2);
            if (suggestion <= current)
            {
                return null;
            }
            return new CalibrationRecommendation(
                settingPath,
                current,
                suggestion,
                "raise",
                $"The marked false positive scored {matched.Score:F2}; "
                + "the suggestion adds an 8% margin.");
        }

        if (matched is null && peak is > 0)
        {
            var suggestion = Math.Round(
                Math.Clamp(Math.Min(current - 0.05, peak.Value * 0.92), 0.2, 40),
                2);
            if (suggestion >= current)
            {
                return null;
            }
            return new CalibrationRecommendation(
                settingPath,
                current,
                suggestion,
                "lower",
                $"A missed marker reached {peak.Value:F2}; "
                + "the suggestion places the threshold 8% below that peak.");
        }

        return null;
    }

    private static IReadOnlyList<CalibrationRecommendation> Consolidate(
        IReadOnlyList<CalibrationRecommendation> raw)
    {
        var result = new List<CalibrationRecommendation>();
        foreach (var group in raw.GroupBy(
                     recommendation => recommendation.SettingPath,
                     StringComparer.Ordinal))
        {
            var directions = group
                .Select(recommendation => recommendation.Direction)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (directions.Length > 1)
            {
                var first = group.First();
                result.Add(new CalibrationRecommendation(
                    first.SettingPath,
                    first.CurrentValue,
                    first.CurrentValue,
                    "review",
                    "This recording contains both missed events and false positives for "
                    + "the same threshold. Gather more controlled samples before changing it.",
                    CanApply: false));
                continue;
            }

            result.Add(directions[0] == "lower"
                ? group.OrderBy(recommendation => recommendation.SuggestedValue).First()
                : group.OrderByDescending(
                    recommendation => recommendation.SuggestedValue).First());
        }
        return result;
    }

    private static double? PeakRelevantScore(
        IReadOnlyList<ProcessedTelemetry> samples,
        DateTimeOffset markerTimestamp,
        string settingPath)
    {
        var values = samples
            .Where(sample =>
            {
                var delta = (sample.Frame.Timestamp - markerTimestamp).TotalMilliseconds;
                return delta >= -LookBehindMilliseconds && delta <= LookAheadMilliseconds;
            })
            .Select(sample => settingPath.StartsWith("Impacts.", StringComparison.Ordinal)
                ? sample.ImpactScore
                : sample.VerticalScore)
            .Where(double.IsFinite)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static string? ThresholdSetting(
        string marker,
        HapticEventKind? matchedKind)
    {
        if (marker.Contains("incident", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (marker.Contains("kerb", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("zebra", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("wheel drop", StringComparison.OrdinalIgnoreCase))
        {
            return "Vertical.StrongKerbThreshold";
        }
        if (marker.Contains("landing", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("pouso", StringComparison.OrdinalIgnoreCase))
        {
            return "Vertical.LandingThreshold";
        }
        if (marker.Contains("compression", StringComparison.OrdinalIgnoreCase))
        {
            return "Vertical.SevereCompressionThreshold";
        }

        return matchedKind switch
        {
            HapticEventKind.MediumImpact => "Impacts.MediumThreshold",
            HapticEventKind.StrongImpact or HapticEventKind.RolloverImpact =>
                "Impacts.StrongThreshold",
            _ => "Impacts.LightThreshold"
        };
    }

    private static double CurrentThreshold(string settingPath, AppSettings settings) =>
        settingPath switch
        {
            "Impacts.LightThreshold" => settings.Impacts.LightThreshold,
            "Impacts.MediumThreshold" => settings.Impacts.MediumThreshold,
            "Impacts.StrongThreshold" => settings.Impacts.StrongThreshold,
            "Vertical.StrongKerbThreshold" => settings.Vertical.StrongKerbThreshold,
            "Vertical.LandingThreshold" => settings.Vertical.LandingThreshold,
            "Vertical.SevereCompressionThreshold" =>
                settings.Vertical.SevereCompressionThreshold,
            _ => throw new ArgumentOutOfRangeException(nameof(settingPath))
        };

    private static IReadOnlySet<HapticEventKind> ExpectedKinds(string marker)
    {
        if (marker.Contains("false positive", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.GetValues<HapticEventKind>()
                .Where(kind => kind != HapticEventKind.None)
                .ToHashSet();
        }
        if (marker.Contains("1x", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.Incident1x };
        }
        if (marker.Contains("2x", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.Incident2x };
        }
        if (marker.Contains("4x", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.Incident4x };
        }
        if (marker.Contains("incident", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind>
            {
                HapticEventKind.Incident1x,
                HapticEventKind.Incident2x,
                HapticEventKind.Incident4x,
                HapticEventKind.IncidentOther
            };
        }
        if (marker.Contains("kerb", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("zebra", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.StrongKerb };
        }
        if (marker.Contains("wheel drop", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind> { HapticEventKind.WheelDrop };
        }
        if (marker.Contains("compression", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<HapticEventKind>
            {
                HapticEventKind.SevereVerticalCompression
            };
        }
        if (marker.Contains("landing", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("pouso", StringComparison.OrdinalIgnoreCase))
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

    private sealed record TriggerFrameSample(
        DateTimeOffset Timestamp,
        TriggerEvaluation Evaluation);
}
