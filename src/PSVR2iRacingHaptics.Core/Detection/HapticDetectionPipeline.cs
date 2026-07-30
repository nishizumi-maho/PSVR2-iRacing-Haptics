using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Detection;

public sealed record PipelineResult(
    DetectedHapticEvent? SelectedEvent,
    IReadOnlyList<DetectedHapticEvent> Candidates,
    ProcessedTelemetry Diagnostics);

public sealed class HapticDetectionPipeline
{
    private readonly TelemetrySignalProcessor _processor = new();
    private readonly ImpactDetector _impactDetector = new();
    private readonly VerticalImpactDetector _verticalDetector = new();

    public PipelineResult Process(TelemetryFrame frame, AppSettings settings)
    {
        if (!frame.IsDriverInCar)
        {
            _impactDetector.Reset();
            _verticalDetector.Reset();
        }

        var processed = _processor.Process(frame);
        var impact = _impactDetector.Evaluate(processed, settings.Impacts);
        var vertical = _verticalDetector.Evaluate(impact.Diagnostics, settings.Vertical);
        var candidates = new[] { impact.Event, vertical.Event }
            .Where(x => x is not null)
            .Cast<DetectedHapticEvent>()
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.Score)
            .ToArray();

        return new PipelineResult(
            candidates.FirstOrDefault(),
            candidates,
            vertical.Diagnostics);
    }

    public void Reset()
    {
        _processor.Reset();
        _impactDetector.Reset();
        _verticalDetector.Reset();
    }
}
