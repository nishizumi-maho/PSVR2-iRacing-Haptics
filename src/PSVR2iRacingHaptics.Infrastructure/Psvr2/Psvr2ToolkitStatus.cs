namespace PSVR2iRacingHaptics.Infrastructure.Psvr2;

public sealed record Psvr2ToolkitStatus
{
    public bool PathFileFound { get; init; }
    public string? PathFile { get; init; }
    public bool DllFound { get; init; }
    public string? DllPath { get; init; }
    public bool DllLoaded { get; init; }
    public bool ExportsResolved { get; init; }
    public bool ApiInitialized { get; init; }
    public bool DriverActive { get; init; }
    public bool? HeadsetAvailable { get; init; }
    public int? InitializationResult { get; init; }
    public string ToolkitVersion { get; init; } = "not exposed by the C API";
    public bool NativeCallTimedOut { get; init; }
    public string Message { get; init; } = "Not checked";
}
