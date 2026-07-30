using System.Diagnostics;
using System.Runtime.InteropServices;
using PSVR2iRacingHaptics.Core.Abstractions;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.Infrastructure.Psvr2;

public sealed class Psvr2ToolkitClient : IHmdRumbleDevice
{
    private const string CapiFileName = "psvr2_toolkit_capi.dll";
    private const string PathFileName = "psvr2tk_capi_path.txt";

    private readonly IAppLogger _logger;
    private readonly int _nativeCallTimeoutMs;
    private readonly SemaphoreSlim _nativeGate = new(1, 1);
    private readonly object _statusLock = new();
    private nint _libraryHandle;
    private InitDelegate? _init;
    private DeinitDelegate? _deinit;
    private GetDriverActiveDelegate? _getDriverActive;
    private SetHmdRumbleDelegate? _setHmdRumble;
    private bool _disposed;
    private bool _nativeInitialized;
    private bool _nativeCallTimedOut;
    private Psvr2ToolkitStatus _status = new();

    public Psvr2ToolkitClient(
        SafetySettings safety,
        IAppLogger? logger = null)
    {
        _nativeCallTimeoutMs = safety.NativeCallTimeoutMs;
        _logger = logger ?? NullAppLogger.Instance;
    }

    public event EventHandler<Psvr2ToolkitStatus>? StatusChanged;

    public Psvr2ToolkitStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
    }

    public bool IsAvailable
    {
        get
        {
            var status = Status;
            return status.DllLoaded
                && status.ExportsResolved
                && status.ApiInitialized
                && status.DriverActive
                && !status.NativeCallTimedOut
                && !_disposed;
        }
    }

    public string StatusDescription => Status.Message;

    public async Task<Psvr2ToolkitStatus> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!OperatingSystem.IsWindows())
        {
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                Message = "A API real do PSVR2 Toolkit requer Windows x64."
            });
        }

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                Message = "Arquitetura incompatível: execute a versão x64."
            });
        }

        var pathFile = Path.Combine(Path.GetTempPath(), PathFileName);
        if (!File.Exists(pathFile))
        {
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                PathFile = pathFile,
                Message =
                    "PSVR2 Toolkit não detectado: arquivo de caminho ausente. "
                    + "Inicie o Toolkit/SteamVR primeiro."
            });
        }

        string directory;
        try
        {
            directory = (await File.ReadAllTextAsync(pathFile, cancellationToken)
                .ConfigureAwait(false))
                .Trim()
                .Trim('"');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Não foi possível ler o arquivo de caminho do Toolkit.", ex);
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                PathFileFound = true,
                PathFile = pathFile,
                Message = "Falha ao ler o arquivo de caminho do PSVR2 Toolkit."
            });
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                PathFileFound = true,
                PathFile = pathFile,
                Message = "O arquivo de caminho do Toolkit está vazio."
            });
        }

        var dllPath = Path.GetFullPath(Path.Combine(directory, CapiFileName));
        if (!File.Exists(dllPath))
        {
            return UpdateStatus(new Psvr2ToolkitStatus
            {
                PathFileFound = true,
                PathFile = pathFile,
                DllPath = dllPath,
                Message = $"DLL da C API não encontrada: {dllPath}"
            });
        }

        if (_libraryHandle == 0)
        {
            try
            {
                _libraryHandle = NativeLibrary.Load(dllPath);
            }
            catch (Exception ex) when (
                ex is DllNotFoundException or BadImageFormatException or FileLoadException)
            {
                _logger.Error($"Falha ao carregar {dllPath}.", ex);
                return UpdateStatus(new Psvr2ToolkitStatus
                {
                    PathFileFound = true,
                    PathFile = pathFile,
                    DllFound = true,
                    DllPath = dllPath,
                    Message =
                        "A DLL foi encontrada, mas não pôde ser carregada. "
                        + "Verifique arquitetura e dependências do Toolkit."
                });
            }
        }

        try
        {
            _init ??= Resolve<InitDelegate>("psvr2_toolkit_init");
            _deinit ??= Resolve<DeinitDelegate>("psvr2_toolkit_deinit");
            _getDriverActive ??= Resolve<GetDriverActiveDelegate>(
                "psvr2_toolkit_get_driver_active");
            _setHmdRumble ??= Resolve<SetHmdRumbleDelegate>(
                "psvr2_toolkit_set_hmd_rumble");
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger.Error("A C API é incompatível: export obrigatório ausente.", ex);
            return UpdateStatus(BuildStatus(
                pathFile,
                dllPath,
                exportsResolved: false,
                initializationResult: null,
                driverActive: false,
                "Função obrigatória não exportada pela DLL; atualize o PSVR2 Toolkit."));
        }

        return await TryInitializeNativeAsync(pathFile, dllPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Psvr2ToolkitStatus> RefreshStatusAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_libraryHandle == 0 || _init is null)
        {
            return await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_nativeCallTimedOut)
        {
            return UpdateStatus(Status with
            {
                NativeCallTimedOut = true,
                DriverActive = false,
                ApiInitialized = false,
                Message =
                    "Uma chamada nativa excedeu o tempo limite. "
                    + "Reinicie este aplicativo e o PSVR2 Toolkit."
            });
        }

        var current = Status;
        if (!_nativeInitialized)
        {
            return await TryInitializeNativeAsync(
                current.PathFile ?? Path.Combine(Path.GetTempPath(), PathFileName),
                current.DllPath ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
        }

        bool active;
        try
        {
            active = await InvokeNativeAsync(
                () => _getDriverActive!(),
                "psvr2_toolkit_get_driver_active",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is TimeoutException or InvalidOperationException or OperationCanceledException)
        {
            _logger.Warning($"Falha ao consultar o driver: {ex.Message}");
            active = false;
        }

        return UpdateStatus(current with
        {
            DriverActive = active,
            ApiInitialized = _nativeInitialized,
            NativeCallTimedOut = _nativeCallTimedOut,
            HeadsetAvailable = null,
            Message = active
                ? "Toolkit e driver ativos; presença do headset não é exposta pela C API."
                : "DLL/API carregada, mas o driver do Toolkit está inativo."
        });
    }

    public async Task SetFrequencyAsync(
        byte frequencyHz,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (frequencyHz > 25)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequencyHz),
                "Este aplicativo limita a frequência ao intervalo 0–25 Hz do teste oficial.");
        }

        if (!IsAvailable || _setHmdRumble is null)
        {
            throw new InvalidOperationException(Status.Message);
        }

        await InvokeNativeAsync(
            () =>
            {
                _setHmdRumble(frequencyHz);
                return true;
            },
            $"psvr2_toolkit_set_hmd_rumble({frequencyHz})",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Psvr2ToolkitStatus> TryInitializeNativeAsync(
        string pathFile,
        string dllPath,
        CancellationToken cancellationToken)
    {
        int result;
        try
        {
            result = await InvokeNativeAsync(
                () => _init!(),
                "psvr2_toolkit_init",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is TimeoutException or InvalidOperationException or OperationCanceledException)
        {
            return UpdateStatus(BuildStatus(
                pathFile,
                dllPath,
                exportsResolved: true,
                initializationResult: null,
                driverActive: false,
                $"Falha ao inicializar a C API: {ex.Message}"));
        }

        _nativeInitialized = result == 0;
        var message = result switch
        {
            0 => "C API inicializada; verificando driver.",
            -1 => "O driver do PSVR2 Toolkit está inativo.",
            -2 => "A C API recusou a inicialização: todos os 8 slots estão ocupados.",
            _ => $"A C API recusou a inicialização com código desconhecido {result}."
        };

        var driverActive = false;
        if (_getDriverActive is not null)
        {
            try
            {
                driverActive = await InvokeNativeAsync(
                    () => _getDriverActive(),
                    "psvr2_toolkit_get_driver_active",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is TimeoutException or InvalidOperationException or OperationCanceledException)
            {
                _logger.Warning($"Não foi possível consultar o driver: {ex.Message}");
            }
        }

        if (result == 0 && driverActive)
        {
            message =
                "Toolkit e driver ativos; presença do headset não é exposta pela C API.";
        }

        var status = BuildStatus(
            pathFile,
            dllPath,
            exportsResolved: true,
            initializationResult: result,
            driverActive,
            message) with
        {
            ApiInitialized = _nativeInitialized
        };
        _logger.Info(
            $"Toolkit: DLL={dllPath}; versão={status.ToolkitVersion}; "
            + $"init={result}; driverAtivo={driverActive}.");
        return UpdateStatus(status);
    }

    private Psvr2ToolkitStatus BuildStatus(
        string pathFile,
        string dllPath,
        bool exportsResolved,
        int? initializationResult,
        bool driverActive,
        string message)
    {
        var version = "não exposta pela C API";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dllPath);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion)
                && info.ProductVersion != "0.0.0.0")
            {
                version = info.ProductVersion;
            }
            else if (!string.IsNullOrWhiteSpace(info.FileVersion)
                     && info.FileVersion != "0.0.0.0")
            {
                version = info.FileVersion;
            }
        }
        catch
        {
        }

        return new Psvr2ToolkitStatus
        {
            PathFileFound = File.Exists(pathFile),
            PathFile = pathFile,
            DllFound = File.Exists(dllPath),
            DllPath = dllPath,
            DllLoaded = _libraryHandle != 0,
            ExportsResolved = exportsResolved,
            ApiInitialized = _nativeInitialized,
            DriverActive = driverActive,
            HeadsetAvailable = null,
            InitializationResult = initializationResult,
            ToolkitVersion = version,
            NativeCallTimedOut = _nativeCallTimedOut,
            Message = message
        };
    }

    private T Resolve<T>(string exportName) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_libraryHandle, exportName, out var address))
        {
            throw new EntryPointNotFoundException(exportName);
        }

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private async Task<T> InvokeNativeAsync<T>(
        Func<T> action,
        string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _nativeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_nativeCallTimedOut)
            {
                throw new InvalidOperationException(
                    "Chamadas nativas foram bloqueadas após um timeout anterior.");
            }

            var nativeTask = Task.Run(action, CancellationToken.None);
            var completed = await Task.WhenAny(
                nativeTask,
                Task.Delay(_nativeCallTimeoutMs, CancellationToken.None))
                .ConfigureAwait(false);
            if (completed != nativeTask)
            {
                _nativeCallTimedOut = true;
                UpdateStatus(Status with
                {
                    NativeCallTimedOut = true,
                    DriverActive = false,
                    ApiInitialized = false,
                    Message =
                        $"Timeout em {operation}; novas chamadas foram bloqueadas por segurança."
                });
                throw new TimeoutException(
                    $"{operation} não retornou em {_nativeCallTimeoutMs} ms.");
            }

            var result = await nativeTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            _nativeGate.Release();
        }
    }

    private Psvr2ToolkitStatus UpdateStatus(Psvr2ToolkitStatus status)
    {
        lock (_statusLock)
        {
            _status = status;
        }
        StatusChanged?.Invoke(this, status);
        return status;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!_nativeCallTimedOut && _nativeInitialized && _setHmdRumble is not null)
        {
            try
            {
                await InvokeNativeAsync(
                    () =>
                    {
                        _setHmdRumble(0);
                        return true;
                    },
                    "Rumble OFF no encerramento",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Falha ao enviar OFF no encerramento: {ex.Message}");
            }
        }

        if (!_nativeCallTimedOut && _nativeInitialized && _deinit is not null)
        {
            try
            {
                await InvokeNativeAsync(
                    () =>
                    {
                        _deinit();
                        return true;
                    },
                    "psvr2_toolkit_deinit",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Falha ao desinicializar a C API: {ex.Message}");
            }
        }

        _disposed = true;
        _nativeInitialized = false;
        if (_libraryHandle != 0 && !_nativeCallTimedOut)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = 0;
        }
        else if (_nativeCallTimedOut)
        {
            _logger.Warning(
                "A DLL permaneceu carregada até o fim do processo porque uma chamada "
                + "nativa pode ainda estar em execução.");
        }

        _nativeGate.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DeinitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool GetDriverActiveDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetHmdRumbleDelegate(byte rumbleHz);
}
