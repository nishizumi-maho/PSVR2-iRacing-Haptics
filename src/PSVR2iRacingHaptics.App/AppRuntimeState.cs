using PSVR2iRacingHaptics.Core.Detection;
using PSVR2iRacingHaptics.Core.Effects;
using PSVR2iRacingHaptics.Core.Models;
using PSVR2iRacingHaptics.Infrastructure.Psvr2;

namespace PSVR2iRacingHaptics.App;

public sealed record AppRuntimeState
{
    public Psvr2ToolkitStatus Toolkit { get; init; } = new();
    public bool IRacingConnected { get; init; }
    public bool DriverInCar { get; init; }
    public bool HapticsEnabled { get; init; }
    public bool SimulatedRumble { get; init; }
    public bool SimulatedTelemetry { get; init; }
    public bool Recording { get; init; }
    public int CircularBufferEntryCount { get; init; }
    public string LastCircularCapture { get; init; } = "None";
    public string ActiveProfileId { get; init; } = string.Empty;
    public string ActiveProfileName { get; init; } = "Default";
    public bool AutoProfileSelectionEnabled { get; init; }
    public bool CustomTriggerEngineEnabled { get; init; } = true;
    public string ProfileSelectionStatus { get; init; } =
        "Automatic profile selection is off.";
    public TelemetryContext TelemetryContext { get; init; } = new();
    public string TelemetryStatus { get; init; } = "Not started";
    public string RumbleDeviceStatus { get; init; } = "Not started";
    public string LastEvent { get; init; } = "None";
    public string LastIncident { get; init; } = "None";
    public ProcessedTelemetry? Diagnostics { get; init; }
    public IReadOnlyList<TriggerEvaluation> TriggerEvaluations { get; init; } =
        Array.Empty<TriggerEvaluation>();
    public RumbleControllerStatus? Rumble { get; init; }
}
