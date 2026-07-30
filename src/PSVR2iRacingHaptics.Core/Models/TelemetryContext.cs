namespace PSVR2iRacingHaptics.Core.Models;

/// <summary>
/// Slowly changing car and circuit identity read from the iRacing SessionInfo
/// YAML block. Empty values mean that iRacing has not exposed that field yet.
/// </summary>
public sealed record TelemetryContext
{
    public int SessionInfoUpdate { get; init; } = -1;
    public int? DriverCarIdx { get; init; }
    public int? CarId { get; init; }
    public int? CarClassId { get; init; }
    public string CarPath { get; init; } = string.Empty;
    public string CarName { get; init; } = string.Empty;
    public string CarClass { get; init; } = string.Empty;
    public int? TrackId { get; init; }
    public string TrackName { get; init; } = string.Empty;
    public string TrackDisplayName { get; init; } = string.Empty;
    public string TrackConfigName { get; init; } = string.Empty;

    public bool HasIdentity =>
        !string.IsNullOrWhiteSpace(CarPath)
        || !string.IsNullOrWhiteSpace(CarName)
        || !string.IsNullOrWhiteSpace(TrackName)
        || CarId.HasValue
        || TrackId.HasValue;

    public string CarDisplayName =>
        FirstNonEmpty(CarName, CarPath, CarId.HasValue ? $"Car {CarId}" : null, "Unknown car");

    public string TrackDisplayLabel
    {
        get
        {
            var track = FirstNonEmpty(
                TrackDisplayName,
                TrackName,
                TrackId.HasValue ? $"Track {TrackId}" : null,
                "Unknown track");
            return string.IsNullOrWhiteSpace(TrackConfigName)
                ? track
                : $"{track} — {TrackConfigName}";
        }
    }

    public string Fingerprint => string.Join(
        "|",
        SessionInfoUpdate,
        DriverCarIdx,
        CarId,
        CarClassId,
        CarPath,
        CarName,
        CarClass,
        TrackId,
        TrackName,
        TrackConfigName);

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;
}
