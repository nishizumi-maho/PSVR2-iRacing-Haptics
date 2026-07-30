using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Abstractions;

public interface ITelemetryClient : IAsyncDisposable
{
    bool IsConnected { get; }
    string StatusDescription { get; }
    event EventHandler<TelemetryFrame>? FrameReceived;
    event EventHandler<bool>? ConnectionChanged;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
