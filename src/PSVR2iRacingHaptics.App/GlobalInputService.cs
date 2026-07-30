using System.Runtime.InteropServices;
using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.App;

/// <summary>
/// Registers process-independent keyboard hotkeys and polls the legacy Windows
/// joystick API for rising button edges. Many wheel bases expose their buttons
/// through this API; when they do not, the wheel software can map a button to
/// one of the configured global keyboard shortcuts.
/// </summary>
public sealed class GlobalInputService : IDisposable
{
    private readonly object _gate = new();
    private readonly HotkeyWindow _window = new();
    private readonly System.Threading.Timer _joystickTimer;
    private readonly Dictionary<int, InputAction> _hotkeyActions = [];
    private readonly Dictionary<int, uint> _previousButtons = [];
    private ActionInputBinding[] _bindings = [];
    private bool _disposed;

    public GlobalInputService()
    {
        _window.HotkeyPressed += OnHotkeyPressed;
        _joystickTimer = new System.Threading.Timer(
            PollJoysticks,
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    public event EventHandler<InputAction>? ActionTriggered;
    public IReadOnlyList<string> RegistrationMessages { get; private set; } =
        Array.Empty<string>();

    public void Configure(InputSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            UnregisterKeyboardHotkeys();
            _bindings = settings.Bindings
                .Select(Clone)
                .ToArray();
            _previousButtons.Clear();
            var messages = new List<string>();
            var identifier = 0x5100;
            foreach (var binding in _bindings.Where(binding =>
                         binding.KeyboardEnabled && binding.VirtualKey > 0))
            {
                var id = identifier++;
                var registered = RegisterHotKey(
                    _window.Handle,
                    id,
                    (uint)binding.KeyboardModifiers | ModNoRepeat,
                    (uint)binding.VirtualKey);
                if (registered)
                {
                    _hotkeyActions[id] = binding.Action;
                    messages.Add(
                        $"{binding.Action}: keyboard hotkey registered.");
                }
                else
                {
                    messages.Add(
                        $"{binding.Action}: keyboard hotkey unavailable "
                        + $"(Windows error {Marshal.GetLastWin32Error()}).");
                }
            }
            RegistrationMessages = messages;
            _joystickTimer.Change(
                _bindings.Any(binding => binding.JoystickEnabled)
                    ? TimeSpan.Zero
                    : Timeout.InfiniteTimeSpan,
                _bindings.Any(binding => binding.JoystickEnabled)
                    ? TimeSpan.FromMilliseconds(40)
                    : Timeout.InfiniteTimeSpan);
        }
    }

    public static string KeyDisplayName(int virtualKey)
    {
        if (virtualKey <= 0)
        {
            return "None";
        }
        return ((Keys)virtualKey).ToString();
    }

    private void OnHotkeyPressed(object? sender, int identifier)
    {
        InputAction? action;
        lock (_gate)
        {
            action = _hotkeyActions.TryGetValue(identifier, out var found)
                ? found
                : null;
        }
        if (action.HasValue)
        {
            ActionTriggered?.Invoke(this, action.Value);
        }
    }

    private void PollJoysticks(object? state)
    {
        ActionInputBinding[] bindings;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            bindings = _bindings
                .Where(binding =>
                    binding.JoystickEnabled
                    && binding.JoystickButtonNumber is >= 1 and <= 32)
                .ToArray();
        }

        foreach (var deviceGroup in bindings.GroupBy(binding =>
                     binding.JoystickDeviceId))
        {
            var info = new JoyInfoEx
            {
                Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
                Flags = JoyReturnButtons
            };
            if (JoyGetPosEx((uint)deviceGroup.Key, ref info) != JoyErrorNoError)
            {
                lock (_gate)
                {
                    _previousButtons.Remove(deviceGroup.Key);
                }
                continue;
            }

            uint previous;
            bool initialized;
            lock (_gate)
            {
                initialized = _previousButtons.TryGetValue(
                    deviceGroup.Key,
                    out previous);
                _previousButtons[deviceGroup.Key] = info.Buttons;
            }
            if (!initialized)
            {
                continue;
            }
            var rising = info.Buttons & ~previous;
            if (rising == 0)
            {
                continue;
            }
            foreach (var binding in deviceGroup)
            {
                var mask = 1u << (binding.JoystickButtonNumber - 1);
                if ((rising & mask) != 0)
                {
                    ActionTriggered?.Invoke(this, binding.Action);
                }
            }
        }
    }

    private void UnregisterKeyboardHotkeys()
    {
        foreach (var identifier in _hotkeyActions.Keys)
        {
            UnregisterHotKey(_window.Handle, identifier);
        }
        _hotkeyActions.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        lock (_gate)
        {
            _joystickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            UnregisterKeyboardHotkeys();
            _bindings = [];
        }
        _joystickTimer.Dispose();
        _window.HotkeyPressed -= OnHotkeyPressed;
        _window.Dispose();
    }

    private static ActionInputBinding Clone(ActionInputBinding binding) => new()
    {
        Action = binding.Action,
        KeyboardEnabled = binding.KeyboardEnabled,
        VirtualKey = binding.VirtualKey,
        KeyboardModifiers = binding.KeyboardModifiers,
        JoystickEnabled = binding.JoystickEnabled,
        JoystickDeviceId = binding.JoystickDeviceId,
        JoystickButtonNumber = binding.JoystickButtonNumber
    };

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "PSVR2 iRacing Haptics global input"
            });
        }

        public event EventHandler<int>? HotkeyPressed;

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey)
            {
                HotkeyPressed?.Invoke(this, message.WParam.ToInt32());
            }
            base.WndProc(ref message);
        }

        public void Dispose() => DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;
        public uint Flags;
        public uint X;
        public uint Y;
        public uint Z;
        public uint Rudder;
        public uint U;
        public uint V;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }

    private const uint JoyReturnButtons = 0x00000080;
    private const uint JoyErrorNoError = 0;
    private const uint ModNoRepeat = 0x00004000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr window,
        int identifier,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int identifier);

    [DllImport("winmm.dll", EntryPoint = "joyGetPosEx")]
    private static extern uint JoyGetPosEx(uint joystickIdentifier, ref JoyInfoEx info);
}
