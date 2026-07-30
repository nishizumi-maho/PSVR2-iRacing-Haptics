using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Core.Telemetry;

public enum RumblePerceptionRating
{
    NotFelt = 0,
    Clear = 1,
    Uncomfortable = 2
}

public enum PhysicalCalibrationPhase
{
    Frequency = 0,
    Duration = 1,
    Completed = 2
}

public sealed record PhysicalCalibrationStep(
    PhysicalCalibrationPhase Phase,
    byte FrequencyHz,
    int DurationMs,
    int StepNumber,
    int EstimatedTotalSteps,
    string Instruction);

public sealed record PhysicalCalibrationResult(
    bool UsableRangeFound,
    byte MinimumClearlyPerceptibleFrequencyHz,
    byte PreferredFrequencyHz,
    byte MaximumComfortableFrequencyHz,
    int MinimumClearlyPerceptibleDurationMs,
    int PreferredDurationMs);

/// <summary>
/// Conservative two-phase listening test. It does not assume that a higher
/// frequency is equivalent to greater intensity; it records the user's actual
/// perception and stops increasing a dimension after discomfort.
/// </summary>
public sealed class PhysicalCalibrationSession
{
    private static readonly byte[] FrequencySteps = [8, 10, 12, 14, 16, 18, 20, 22, 24];
    private static readonly int[] DurationSteps = [50, 70, 90, 110, 140, 180, 220];
    private readonly List<RatedStep> _ratings = [];
    private int _frequencyIndex;
    private int _durationIndex;
    private byte _durationTestFrequency = 16;

    public PhysicalCalibrationPhase Phase { get; private set; } =
        PhysicalCalibrationPhase.Frequency;
    public PhysicalCalibrationResult? Result { get; private set; }

    public PhysicalCalibrationStep CurrentStep
    {
        get
        {
            if (Phase == PhysicalCalibrationPhase.Completed)
            {
                throw new InvalidOperationException("The calibration is complete.");
            }
            var step = Phase == PhysicalCalibrationPhase.Frequency
                ? new TestPoint(FrequencySteps[_frequencyIndex], 140)
                : new TestPoint(_durationTestFrequency, DurationSteps[_durationIndex]);
            return new PhysicalCalibrationStep(
                Phase,
                step.FrequencyHz,
                step.DurationMs,
                _ratings.Count + 1,
                FrequencySteps.Length + DurationSteps.Length,
                Phase == PhysicalCalibrationPhase.Frequency
                    ? "Rate whether this frequency is perceptible and comfortable. "
                        + "Frequency is not an intensity control."
                    : "Rate whether this duration is long enough to recognize without "
                        + "becoming distracting.");
        }
    }

    public PhysicalCalibrationStep? Record(RumblePerceptionRating rating)
    {
        var current = CurrentStep;
        _ratings.Add(new RatedStep(
            current.Phase,
            current.FrequencyHz,
            current.DurationMs,
            rating));

        if (Phase == PhysicalCalibrationPhase.Frequency)
        {
            var stopFrequency = rating == RumblePerceptionRating.Uncomfortable
                || _frequencyIndex == FrequencySteps.Length - 1;
            if (!stopFrequency)
            {
                _frequencyIndex++;
                return CurrentStep;
            }

            var clearFrequencies = _ratings
                .Where(step =>
                    step.Phase == PhysicalCalibrationPhase.Frequency
                    && step.Rating == RumblePerceptionRating.Clear)
                .Select(step => step.FrequencyHz)
                .ToArray();
            if (clearFrequencies.Length == 0)
            {
                Result = NoUsableRange();
                Phase = PhysicalCalibrationPhase.Completed;
                return null;
            }
            _durationTestFrequency =
                clearFrequencies[clearFrequencies.Length / 2];
            Phase = PhysicalCalibrationPhase.Duration;
            return CurrentStep;
        }

        var stopDuration = rating == RumblePerceptionRating.Uncomfortable
            || _durationIndex == DurationSteps.Length - 1;
        if (!stopDuration)
        {
            _durationIndex++;
            return CurrentStep;
        }

        var hasClearDuration = _ratings.Any(step =>
            step.Phase == PhysicalCalibrationPhase.Duration
            && step.Rating == RumblePerceptionRating.Clear);
        Result = hasClearDuration ? BuildResult() : NoUsableRange();
        Phase = PhysicalCalibrationPhase.Completed;
        return null;
    }

    public void Reset()
    {
        _ratings.Clear();
        _frequencyIndex = 0;
        _durationIndex = 0;
        _durationTestFrequency = 16;
        Result = null;
        Phase = PhysicalCalibrationPhase.Frequency;
    }

    public PhysicalCalibrationSettings ToSettings()
    {
        var result = Result
            ?? throw new InvalidOperationException("Complete the calibration first.");
        return new PhysicalCalibrationSettings
        {
            Completed = true,
            UsableRangeFound = result.UsableRangeFound,
            MinimumClearlyPerceptibleFrequencyHz =
                result.MinimumClearlyPerceptibleFrequencyHz,
            PreferredFrequencyHz = result.PreferredFrequencyHz,
            MaximumComfortableFrequencyHz = result.MaximumComfortableFrequencyHz,
            MinimumClearlyPerceptibleDurationMs =
                result.MinimumClearlyPerceptibleDurationMs,
            PreferredDurationMs = result.PreferredDurationMs,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private PhysicalCalibrationResult BuildResult()
    {
        var frequencyRatings = _ratings
            .Where(step => step.Phase == PhysicalCalibrationPhase.Frequency)
            .ToArray();
        var clearFrequencies = frequencyRatings
            .Where(step => step.Rating == RumblePerceptionRating.Clear)
            .Select(step => step.FrequencyHz)
            .Order()
            .ToArray();
        var firstUncomfortableFrequency = frequencyRatings
            .Where(step => step.Rating == RumblePerceptionRating.Uncomfortable)
            .Select(step => (byte?)step.FrequencyHz)
            .FirstOrDefault();
        var minimumFrequency = clearFrequencies.FirstOrDefault((byte)10);
        var preferredFrequency = clearFrequencies.Length == 0
            ? (byte)16
            : clearFrequencies[clearFrequencies.Length / 2];
        var maximumFrequency = firstUncomfortableFrequency.HasValue
            ? FrequencySteps
                .Where(value => value < firstUncomfortableFrequency.Value)
                .DefaultIfEmpty(minimumFrequency)
                .Max()
            : clearFrequencies.LastOrDefault(preferredFrequency);

        var durationRatings = _ratings
            .Where(step => step.Phase == PhysicalCalibrationPhase.Duration)
            .ToArray();
        var clearDurations = durationRatings
            .Where(step => step.Rating == RumblePerceptionRating.Clear)
            .Select(step => step.DurationMs)
            .Order()
            .ToArray();
        var minimumDuration = clearDurations.FirstOrDefault(90);
        var preferredDuration = clearDurations.Length == 0
            ? 140
            : clearDurations[clearDurations.Length / 2];

        return new PhysicalCalibrationResult(
            true,
            minimumFrequency,
            preferredFrequency,
            Math.Max(preferredFrequency, maximumFrequency),
            minimumDuration,
            Math.Max(minimumDuration, preferredDuration));
    }

    private static PhysicalCalibrationResult NoUsableRange() =>
        new(
            UsableRangeFound: false,
            MinimumClearlyPerceptibleFrequencyHz: 0,
            PreferredFrequencyHz: 0,
            MaximumComfortableFrequencyHz: 0,
            MinimumClearlyPerceptibleDurationMs: 0,
            PreferredDurationMs: 0);

    private sealed record TestPoint(byte FrequencyHz, int DurationMs);

    private sealed record RatedStep(
        PhysicalCalibrationPhase Phase,
        byte FrequencyHz,
        int DurationMs,
        RumblePerceptionRating Rating);
}
