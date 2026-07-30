namespace PSVR2iRacingHaptics.Core.Configuration;

public enum InputAction
{
    EmergencyStop = 0,
    ToggleHaptics,
    ToggleRecording,
    SaveCircularBuffer,
    MarkImpact,
    MarkStrongKerb,
    MarkLanding,
    MarkWheelDrop,
    MarkFalsePositive
}

[Flags]
public enum KeyboardModifier
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed class ActionInputBinding
{
    public InputAction Action { get; set; }
    public bool KeyboardEnabled { get; set; }
    public int VirtualKey { get; set; }
    public KeyboardModifier KeyboardModifiers { get; set; }
    public bool JoystickEnabled { get; set; }
    public int JoystickDeviceId { get; set; }
    public int JoystickButtonNumber { get; set; }
}

public sealed class InputSettings
{
    public List<ActionInputBinding> Bindings { get; set; } =
        CreateDefaults().ToList();

    public static IReadOnlyList<ActionInputBinding> CreateDefaults() =>
    [
        Keyboard(
            InputAction.EmergencyStop,
            virtualKey: 0x7B,
            KeyboardModifier.Control | KeyboardModifier.Shift), // F12
        Keyboard(
            InputAction.ToggleHaptics,
            virtualKey: 0x7A,
            KeyboardModifier.Control | KeyboardModifier.Shift), // F11
        Keyboard(
            InputAction.ToggleRecording,
            virtualKey: 0x78,
            KeyboardModifier.Control | KeyboardModifier.Shift), // F9
        Keyboard(
            InputAction.SaveCircularBuffer,
            virtualKey: 0x77,
            KeyboardModifier.Control | KeyboardModifier.Shift), // F8
        Disabled(InputAction.MarkImpact),
        Disabled(InputAction.MarkStrongKerb),
        Disabled(InputAction.MarkLanding),
        Disabled(InputAction.MarkWheelDrop),
        Disabled(InputAction.MarkFalsePositive)
    ];

    private static ActionInputBinding Keyboard(
        InputAction action,
        int virtualKey,
        KeyboardModifier modifiers) =>
        new()
        {
            Action = action,
            KeyboardEnabled = true,
            VirtualKey = virtualKey,
            KeyboardModifiers = modifiers
        };

    private static ActionInputBinding Disabled(InputAction action) =>
        new() { Action = action };
}
