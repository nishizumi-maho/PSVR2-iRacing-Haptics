using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed record PipelineResult(
    DetectedHapticEvent? SelectedEvent,
    IReadOnlyList<DetectedHapticEvent> Candidates,
    ProcessedTelemetry Diagnostics,
    IReadOnlyList<TriggerEvaluation> TriggerEvaluations);

public sealed class HapticDetectionPipeline
{
    private readonly TelemetrySignalProcessor _processor = new();
    private readonly ImpactDetector _impactDetector = new();
    private readonly VerticalImpactDetector _verticalDetector = new();
    private readonly IncidentDetector _incidentDetector = new();
    private readonly TelemetryTriggerEngine _triggerEngine = new();

    public PipelineResult Process(TelemetryFrame frame, AppSettings settings)
    {
        if (!frame.IsDriverInCar)
        {
            _impactDetector.Reset();
            _verticalDetector.Reset();
            _incidentDetector.Reset();
        }

        var processed = _processor.Process(frame);
        var impact = _impactDetector.Evaluate(processed, settings.Impacts);
        var vertical = _verticalDetector.Evaluate(impact.Diagnostics, settings.Vertical);
        var physicalCandidates = new[] { impact.Event, vertical.Event }
            .Where(x => x is not null)
            .Cast<DetectedHapticEvent>()
            .ToArray();
        var incident = _incidentDetector.Evaluate(
            vertical.Diagnostics,
            settings.Incidents,
            physicalCandidates);
        var builtInCandidates = physicalCandidates
            .Cast<DetectedHapticEvent?>()
            .Append(incident.Event)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.Score)
            .ToArray();
        var triggerResult = _triggerEngine.Evaluate(
            vertical.Diagnostics,
            settings.Triggers,
            builtInCandidates);
        var candidates = triggerResult.Candidates;

        return new PipelineResult(
            candidates.FirstOrDefault(),
            candidates,
            vertical.Diagnostics,
            triggerResult.Evaluations);
    }

    public void Reset()
    {
        _processor.Reset();
        _impactDetector.Reset();
        _verticalDetector.Reset();
        _incidentDetector.Reset();
        _triggerEngine.Reset();
    }
}
