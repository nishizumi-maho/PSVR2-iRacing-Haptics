using System.Runtime.InteropServices;
using System.Text;
using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Infrastructure.IRacing;

public sealed class IRacingSharedMemoryClient : ITelemetryClient
{
    private const string MemoryMapName = @"Local\IRSDKMemMapFileName";
    private const string DataValidEventName = @"Local\IRSDKDataValidEvent";
    private const uint FileMapRead = 0x0004;
    private const uint Synchronize = 0x00100000;
    private const uint WaitObject0 = 0x00000000;
    private const int HeaderSize = 112;
    private const int VarHeaderSize = 144;
    public const int SessionInfoUpdateOffset = 12;
    public const int SessionInfoLengthOffset = 16;
    public const int SessionInfoDataOffsetOffset = 20;

    private readonly IAppLogger _logger;
    private readonly Dictionary<string, VariableDescriptor> _variables =
        new(StringComparer.Ordinal);
    private CancellationTokenSource? _lifetime;
    private Task _readerTask = Task.CompletedTask;
    private nint _mappingHandle;
    private nint _memory;
    private nint _dataEvent;
    private int _lastTick = int.MinValue;
    private int _lastSessionInfoUpdate = int.MinValue;
    private bool _lastInCar;
    private TelemetryContext _context = new();

    public IRacingSharedMemoryClient(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsConnected { get; private set; }
    public string StatusDescription { get; private set; } = "iRacing not connected";
    public event EventHandler<TelemetryFrame>? FrameReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_lifetime is not null)
        {
            return Task.CompletedTask;
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = ReaderLoopAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var lifetime = Interlocked.Exchange(ref _lifetime, null);
        if (lifetime is null)
        {
            return;
        }

        lifetime.Cancel();
        try
        {
            await _readerTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
        }
        lifetime.Dispose();
        CloseMapping();
        SetConnected(false, "iRacing disconnected");
    }

    private async Task ReaderLoopAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            StatusDescription = "iRacing shared memory requires Windows.";
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_memory == 0)
                {
                    if (!TryOpenMapping())
                    {
                        SetConnected(false, "Waiting for iRacing");
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }

                if (!HeaderIsConnected())
                {
                    CloseMapping();
                    SetConnected(false, "Waiting for valid iRacing telemetry");
                    await Task.Delay(750, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!IsConnected)
                {
                    BuildVariableIndex();
                    RefreshSessionContext(force: true);
                    SetConnected(true, $"iRacing connected ({_variables.Count} variables)");
                    LogVariableAvailability();
                }

                WaitForTelemetryTick(20);
                if (TryReadLatestRow(out var tick, out var row) && tick != _lastTick)
                {
                    _lastTick = tick;
                    RefreshSessionContext();
                    var frame = BuildFrame(tick, row);
                    if (_lastInCar != frame.IsDriverInCar)
                    {
                        _lastInCar = frame.IsDriverInCar;
                        _logger.Info(frame.IsDriverInCar
                            ? "Driver entered the car."
                            : "Driver left the car or telemetry became invalid.");
                    }
                    FrameReceived?.Invoke(this, frame);
                }
                else
                {
                    await Task.Delay(4, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to read iRacing telemetry; reconnecting.", ex);
                CloseMapping();
                SetConnected(false, "Read failure; attempting to reconnect");
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool TryOpenMapping()
    {
        _mappingHandle = OpenFileMapping(FileMapRead, false, MemoryMapName);
        if (_mappingHandle == 0)
        {
            return false;
        }

        _memory = MapViewOfFile(_mappingHandle, FileMapRead, 0, 0, UIntPtr.Zero);
        if (_memory == 0)
        {
            CloseHandle(_mappingHandle);
            _mappingHandle = 0;
            return false;
        }

        _dataEvent = OpenEvent(Synchronize, false, DataValidEventName);
        _lastTick = int.MinValue;
        return true;
    }

    private bool HeaderIsConnected()
    {
        if (_memory == 0)
        {
            return false;
        }

        var version = ReadInt32(0);
        var status = ReadInt32(4);
        var numBuffers = ReadInt32(32);
        var bufferLength = ReadInt32(36);
        return version is >= 1 and <= 10
            && (status & 1) != 0
            && numBuffers is >= 1 and <= 4
            && bufferLength is > 0 and < 4_000_000;
    }

    private void BuildVariableIndex()
    {
        _variables.Clear();
        var numVars = ReadInt32(24);
        var varHeaderOffset = ReadInt32(28);
        if (numVars is < 1 or > 5000 || varHeaderOffset < HeaderSize)
        {
            throw new InvalidDataException(
                $"Invalid variable header: count={numVars}, offset={varHeaderOffset}.");
        }

        for (var index = 0; index < numVars; index++)
        {
            var address = _memory + varHeaderOffset + index * VarHeaderSize;
            var type = Marshal.ReadInt32(address, 0);
            var offset = Marshal.ReadInt32(address, 4);
            var count = Marshal.ReadInt32(address, 8);
            var name = ReadAscii(address + 16, 32);
            if (!string.IsNullOrEmpty(name)
                && type is >= 0 and <= 5
                && offset >= 0
                && count > 0)
            {
                _variables[name] = new VariableDescriptor(type, offset, count);
            }
        }
    }

    private bool TryReadLatestRow(out int tick, out byte[] row)
    {
        tick = int.MinValue;
        row = Array.Empty<byte>();
        var numBuffers = Math.Clamp(ReadInt32(32), 0, 4);
        var bufferLength = ReadInt32(36);
        if (numBuffers == 0 || bufferLength is <= 0 or > 4_000_000)
        {
            return false;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var selected = -1;
            var selectedTick = int.MinValue;
            var selectedOffset = 0;
            for (var index = 0; index < numBuffers; index++)
            {
                var headerOffset = 48 + index * 16;
                var candidateTick = ReadInt32(headerOffset);
                if (candidateTick > selectedTick)
                {
                    selected = index;
                    selectedTick = candidateTick;
                    selectedOffset = ReadInt32(headerOffset + 4);
                }
            }

            if (selected < 0 || selectedOffset < HeaderSize)
            {
                return false;
            }

            var copy = new byte[bufferLength];
            Marshal.Copy(_memory + selectedOffset, copy, 0, bufferLength);
            var tickAfterCopy = ReadInt32(48 + selected * 16);
            if (tickAfterCopy == selectedTick)
            {
                tick = selectedTick;
                row = copy;
                return true;
            }
        }

        return false;
    }

    private TelemetryFrame BuildFrame(int tick, byte[] row)
    {
        var statusConnected = HeaderIsConnected();
        return new TelemetryFrame
        {
            Timestamp = DateTimeOffset.UtcNow,
            Sequence = tick,
            IsConnected = statusConnected,
            IsValid = statusConnected,
            IsOnTrack = ReadBool(row, "IsOnTrack"),
            IsOnTrackCar = ReadBool(row, "IsOnTrackCar"),
            IsInGarage = ReadBool(row, "IsInGarage"),
            IsReplayPlaying = ReadBool(row, "IsReplayPlaying"),
            SessionState = ReadInt(row, "SessionState"),
            EnterExitReset = ReadInt(row, "EnterExitReset"),
            Context = _context,
            SpeedMps = ReadFloat(row, "Speed"),
            LatAccelMps2 = ReadFloat(row, "LatAccel"),
            LongAccelMps2 = ReadFloat(row, "LongAccel"),
            VertAccelMps2 = ReadFloat(row, "VertAccel"),
            VelocityXMps = ReadFloat(row, "VelocityX"),
            VelocityYMps = ReadFloat(row, "VelocityY"),
            VelocityZMps = ReadFloat(row, "VelocityZ"),
            YawRad = ReadFloat(row, "Yaw"),
            PitchRad = ReadFloat(row, "Pitch"),
            RollRad = ReadFloat(row, "Roll"),
            YawRateRadPerSec = ReadFloat(row, "YawRate"),
            PitchRateRadPerSec = ReadFloat(row, "PitchRate"),
            RollRateRadPerSec = ReadFloat(row, "RollRate"),
            Brake = ReadFloat(row, "Brake"),
            Throttle = ReadFloat(row, "Throttle"),
            Gear = ReadInt(row, "Gear"),
            Rpm = ReadFloat(row, "RPM"),
            IncidentCount = ReadNullableInt(row, "PlayerCarMyIncidentCount"),
            PlayerTrackSurface = ReadNullableInt(row, "PlayerTrackSurface"),
            PlayerTrackSurfaceMaterial = ReadNullableInt(row, "PlayerTrackSurfaceMaterial"),
            LfWheelSpeedMps = ReadNullableFloat(row, "LFspeed"),
            RfWheelSpeedMps = ReadNullableFloat(row, "RFspeed"),
            LrWheelSpeedMps = ReadNullableFloat(row, "LRspeed"),
            RrWheelSpeedMps = ReadNullableFloat(row, "RRspeed"),
            LfShockDeflectionM = ReadNullableFloat(row, "LFshockDefl"),
            RfShockDeflectionM = ReadNullableFloat(row, "RFshockDefl"),
            LrShockDeflectionM = ReadNullableFloat(row, "LRshockDefl"),
            RrShockDeflectionM = ReadNullableFloat(row, "RRshockDefl"),
            LfShockVelocityMps = ReadNullableFloat(row, "LFshockVel"),
            RfShockVelocityMps = ReadNullableFloat(row, "RFshockVel"),
            LrShockVelocityMps = ReadNullableFloat(row, "LRshockVel"),
            RrShockVelocityMps = ReadNullableFloat(row, "RRshockVel"),
            TireLfRumblePitchHz = ReadNullableFloat(row, "TireLF_RumblePitch"),
            TireRfRumblePitchHz = ReadNullableFloat(row, "TireRF_RumblePitch"),
            TireLrRumblePitchHz = ReadNullableFloat(row, "TireLR_RumblePitch"),
            TireRrRumblePitchHz = ReadNullableFloat(row, "TireRR_RumblePitch")
        };
    }

    private void RefreshSessionContext(bool force = false)
    {
        var update = ReadInt32(SessionInfoUpdateOffset);
        if (!force && update == _lastSessionInfoUpdate)
        {
            return;
        }

        var length = ReadInt32(SessionInfoLengthOffset);
        var offset = ReadInt32(SessionInfoDataOffsetOffset);
        if (length is <= 0 or > 8_000_000 || offset < HeaderSize)
        {
            _logger.Warning(
                $"Invalid iRacing SessionInfo location: length={length}, offset={offset}.");
            _lastSessionInfoUpdate = update;
            return;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var bytes = new byte[length];
            Marshal.Copy(_memory + offset, bytes, 0, length);
            if (ReadInt32(SessionInfoUpdateOffset) != update)
            {
                update = ReadInt32(SessionInfoUpdateOffset);
                continue;
            }

            var terminator = Array.IndexOf(bytes, (byte)0);
            var yaml = Encoding.UTF8.GetString(
                bytes,
                0,
                terminator >= 0 ? terminator : bytes.Length);
            var parsed = IRacingSessionInfoParser.Parse(yaml, update);
            var identityChanged =
                !parsed.CarPath.Equals(_context.CarPath, StringComparison.OrdinalIgnoreCase)
                || parsed.CarId != _context.CarId
                || !parsed.TrackName.Equals(
                    _context.TrackName,
                    StringComparison.OrdinalIgnoreCase)
                || !parsed.TrackConfigName.Equals(
                    _context.TrackConfigName,
                    StringComparison.OrdinalIgnoreCase);
            _context = parsed;
            _lastSessionInfoUpdate = update;
            if (identityChanged && parsed.HasIdentity)
            {
                _logger.Info(
                    $"iRacing identity: car={parsed.CarDisplayName}; "
                    + $"class={EmptyAsUnknown(parsed.CarClass)}; "
                    + $"track={parsed.TrackDisplayLabel}; "
                    + $"CarPath={EmptyAsUnknown(parsed.CarPath)}.");
            }
            return;
        }
    }

    private bool ReadBool(byte[] row, string name) =>
        _variables.TryGetValue(name, out var variable)
        && variable.Type == 1
        && variable.Offset < row.Length
        && row[variable.Offset] != 0;

    private int ReadInt(byte[] row, string name) =>
        ReadNullableInt(row, name) ?? 0;

    private int? ReadNullableInt(byte[] row, string name)
    {
        if (!_variables.TryGetValue(name, out var variable)
            || variable.Type is not (2 or 3)
            || variable.Offset < 0
            || variable.Offset + sizeof(int) > row.Length)
        {
            return null;
        }
        return BitConverter.ToInt32(row, variable.Offset);
    }

    private float ReadFloat(byte[] row, string name) =>
        ReadNullableFloat(row, name) ?? 0;

    private float? ReadNullableFloat(byte[] row, string name)
    {
        if (!_variables.TryGetValue(name, out var variable)
            || variable.Type != 4
            || variable.Offset < 0
            || variable.Offset + sizeof(float) > row.Length)
        {
            return null;
        }
        var value = BitConverter.ToSingle(row, variable.Offset);
        return float.IsFinite(value) ? value : null;
    }

    private void LogVariableAvailability()
    {
        var required = new[]
        {
            "LatAccel", "LongAccel", "VertAccel", "Speed", "IsOnTrack",
            "YawRate", "PitchRate", "RollRate"
        };
        var missingRequired = required.Where(x => !_variables.ContainsKey(x)).ToArray();
        if (missingRequired.Length > 0)
        {
            _logger.Warning(
                "Missing required variables: " + string.Join(", ", missingRequired));
        }

        var optionalGroups = new[]
        {
            ("suspension", new[] { "LFshockVel", "RFshockVel", "LRshockVel", "RRshockVel" }),
            ("rumble strip", new[]
            {
                "TireLF_RumblePitch", "TireRF_RumblePitch",
                "TireLR_RumblePitch", "TireRR_RumblePitch"
            }),
            ("incidents", new[] { "PlayerCarMyIncidentCount" })
        };
        foreach (var (group, variables) in optionalGroups)
        {
            if (!variables.Any(_variables.ContainsKey))
            {
                _logger.Warning(
                    $"Optional {group} telemetry is unavailable; fallback signals will be used.");
            }
        }
    }

    private void WaitForTelemetryTick(uint timeoutMs)
    {
        if (_dataEvent != 0)
        {
            var result = WaitForSingleObject(_dataEvent, timeoutMs);
            if (result == WaitObject0)
            {
                return;
            }
        }
    }

    private int ReadInt32(int offset) => Marshal.ReadInt32(_memory, offset);

    private static string ReadAscii(nint address, int maximumLength)
    {
        var bytes = new byte[maximumLength];
        Marshal.Copy(address, bytes, 0, maximumLength);
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }
        return Encoding.ASCII.GetString(bytes, 0, length);
    }

    private void SetConnected(bool connected, string status)
    {
        StatusDescription = status;
        if (IsConnected == connected)
        {
            return;
        }

        IsConnected = connected;
        ConnectionChanged?.Invoke(this, connected);
        _logger.Info(status + ".");
    }

    private void CloseMapping()
    {
        if (_memory != 0)
        {
            UnmapViewOfFile(_memory);
            _memory = 0;
        }
        if (_mappingHandle != 0)
        {
            CloseHandle(_mappingHandle);
            _mappingHandle = 0;
        }
        if (_dataEvent != 0)
        {
            CloseHandle(_dataEvent);
            _dataEvent = 0;
        }
        _variables.Clear();
        _lastTick = int.MinValue;
        _lastSessionInfoUpdate = int.MinValue;
        _lastInCar = false;
        _context = new TelemetryContext();
    }

    private static string EmptyAsUnknown(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private sealed record VariableDescriptor(int Type, int Offset, int Count);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint OpenFileMapping(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint MapViewOfFile(
        nint fileMappingObject,
        uint desiredAccess,
        uint fileOffsetHigh,
        uint fileOffsetLow,
        UIntPtr numberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(nint baseAddress);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint OpenEvent(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
