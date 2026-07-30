using PSVR2iRacingHaptics.Core.Configuration;

namespace PSVR2iRacingHaptics.App;

public sealed class InputControlsControl : UserControl
{
    private readonly AppCoordinator _coordinator;
    private readonly GlobalInputService _input = new();
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Top,
        Height = 330,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _status = new()
    {
        AutoSize = true,
        MaximumSize = new Size(900, 0),
        ForeColor = Color.FromArgb(47, 58, 72)
    };

    public InputControlsControl(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Controls.Add(Build());
        ConfigureGrid();
        LoadBindings();
        _input.ActionTriggered += OnActionTriggered;
        Disposed += (_, _) =>
        {
            _input.ActionTriggered -= OnActionTriggered;
            _input.Dispose();
        };
    }

    private Control Build()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        panel.Controls.Add(Heading("Global keyboard and wheel controls"));
        panel.Controls.Add(Info(
            "Keyboard bindings work while another application has focus. Wheel buttons "
            + "use the Windows joystick API with a zero-based device ID and a one-based "
            + "button number. If your wheel does not expose buttons through that API, "
            + "map the wheel button to the configured keyboard shortcut in its software."));
        panel.Controls.Add(Info(
            "The emergency stop is Ctrl+Shift+F12 by default. Avoid shortcuts already "
            + "used by iRacing, overlays or Windows. A registration message below tells "
            + "you when another application already owns a keyboard combination."));
        panel.Controls.Add(_grid);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 8)
        };
        var save = Button("Save and activate bindings", Color.FromArgb(30, 105, 170));
        save.Click += async (_, _) => await SaveAsync();
        var defaults = Button("Restore default keyboard bindings");
        defaults.Click += (_, _) => LoadRows(InputSettings.CreateDefaults());
        buttons.Controls.Add(save);
        buttons.Controls.Add(defaults);
        panel.Controls.Add(buttons);
        panel.Controls.Add(_status);
        panel.Controls.Add(Info(
            "Marker actions write into an active JSONL recording. Save circular buffer "
            + "captures the configured number of seconds before the button press. Input "
            + "actions never synthesize iRacing controls or modify the PSVR2 Toolkit."));
        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "action",
            HeaderText = "Action",
            ReadOnly = true,
            Width = 165
        });
        _grid.Columns.Add(CheckColumn("keyboard", "Keyboard", 66));
        _grid.Columns.Add(CheckColumn("ctrl", "Ctrl", 42));
        _grid.Columns.Add(CheckColumn("alt", "Alt", 42));
        _grid.Columns.Add(CheckColumn("shift", "Shift", 48));
        _grid.Columns.Add(CheckColumn("windows", "Win", 42));

        var keys = Enum.GetValues<Keys>()
            .Select(key => new KeyChoice((int)key, key.ToString()))
            .Where(choice => choice.Value is > 0 and <= 255)
            .GroupBy(choice => choice.Value)
            .Select(group => group.First())
            .OrderBy(choice => choice.Label, StringComparer.OrdinalIgnoreCase)
            .Prepend(new KeyChoice(0, "None"))
            .ToArray();
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "key",
            HeaderText = "Key",
            DataSource = keys,
            DisplayMember = nameof(KeyChoice.Label),
            ValueMember = nameof(KeyChoice.Value),
            FlatStyle = FlatStyle.Flat,
            Width = 110
        });
        _grid.Columns.Add(CheckColumn("joystick", "Wheel", 55));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "device",
            HeaderText = "Device ID",
            Width = 70
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "button",
            HeaderText = "Button",
            Width = 62
        });
    }

    private void LoadBindings()
    {
        var bindings = _coordinator.Settings.Input.Bindings;
        LoadRows(bindings);
        _input.Configure(_coordinator.Settings.Input);
        ShowRegistrationStatus();
    }

    private void LoadRows(IEnumerable<ActionInputBinding> bindings)
    {
        _grid.Rows.Clear();
        foreach (var binding in bindings.OrderBy(binding => binding.Action))
        {
            var modifiers = binding.KeyboardModifiers;
            var row = _grid.Rows.Add(
                ActionName(binding.Action),
                binding.KeyboardEnabled,
                modifiers.HasFlag(KeyboardModifier.Control),
                modifiers.HasFlag(KeyboardModifier.Alt),
                modifiers.HasFlag(KeyboardModifier.Shift),
                modifiers.HasFlag(KeyboardModifier.Windows),
                binding.VirtualKey,
                binding.JoystickEnabled,
                binding.JoystickDeviceId,
                binding.JoystickButtonNumber);
            _grid.Rows[row].Tag = binding.Action;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            _grid.EndEdit();
            var settings = _coordinator.Settings;
            settings.Input.Bindings = _grid.Rows
                .Cast<DataGridViewRow>()
                .Where(row => !row.IsNewRow && row.Tag is InputAction)
                .Select(ReadBinding)
                .ToList();
            await _coordinator.ApplySettingsAsync(settings);
            _input.Configure(_coordinator.Settings.Input);
            ShowRegistrationStatus();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private ActionInputBinding ReadBinding(DataGridViewRow row)
    {
        var action = (InputAction)row.Tag!;
        var modifiers = KeyboardModifier.None;
        if (Bool(row, "ctrl"))
        {
            modifiers |= KeyboardModifier.Control;
        }
        if (Bool(row, "alt"))
        {
            modifiers |= KeyboardModifier.Alt;
        }
        if (Bool(row, "shift"))
        {
            modifiers |= KeyboardModifier.Shift;
        }
        if (Bool(row, "windows"))
        {
            modifiers |= KeyboardModifier.Windows;
        }
        return new ActionInputBinding
        {
            Action = action,
            KeyboardEnabled = Bool(row, "keyboard"),
            VirtualKey = Int(row, "key", 0, 255),
            KeyboardModifiers = modifiers,
            JoystickEnabled = Bool(row, "joystick"),
            JoystickDeviceId = Int(row, "device", 0, 15),
            JoystickButtonNumber = Int(row, "button", 0, 32)
        };
    }

    private void OnActionTriggered(object? sender, InputAction action)
    {
        if (IsDisposed)
        {
            return;
        }
        BeginInvoke((Action)(async () => await ExecuteActionAsync(action)));
    }

    private async Task ExecuteActionAsync(InputAction action)
    {
        try
        {
            switch (action)
            {
                case InputAction.EmergencyStop:
                    await _coordinator.EmergencyStopAsync("global input binding");
                    break;
                case InputAction.ToggleHaptics:
                {
                    var settings = _coordinator.Settings;
                    settings.HapticsEnabled = !settings.HapticsEnabled;
                    await _coordinator.ApplySettingsAsync(settings);
                    break;
                }
                case InputAction.ToggleRecording:
                    if (_coordinator.State.Recording)
                    {
                        await _coordinator.StopRecordingAsync();
                    }
                    else
                    {
                        await _coordinator.StartRecordingAsync();
                    }
                    break;
                case InputAction.SaveCircularBuffer:
                    await _coordinator.SaveRecentTelemetryAsync(
                        marker: "Circular buffer capture from input binding");
                    break;
                case InputAction.MarkImpact:
                    await _coordinator.MarkAsync("Impact");
                    break;
                case InputAction.MarkStrongKerb:
                    await _coordinator.MarkAsync("Strong kerb");
                    break;
                case InputAction.MarkLanding:
                    await _coordinator.MarkAsync("Landing");
                    break;
                case InputAction.MarkWheelDrop:
                    await _coordinator.MarkAsync("Wheel drop");
                    break;
                case InputAction.MarkFalsePositive:
                    await _coordinator.MarkAsync("False positive");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
            _status.Text =
                $"{DateTime.Now:T} — {ActionName(action)} executed successfully.";
        }
        catch (Exception exception)
        {
            _status.Text =
                $"{DateTime.Now:T} — {ActionName(action)} failed: {exception.Message}";
        }
    }

    private void ShowRegistrationStatus()
    {
        _status.Text = _input.RegistrationMessages.Count == 0
            ? "No global keyboard bindings are enabled. Wheel polling remains active "
                + "for enabled wheel bindings."
            : string.Join(Environment.NewLine, _input.RegistrationMessages);
    }

    private static DataGridViewCheckBoxColumn CheckColumn(
        string name,
        string title,
        int width) => new()
        {
            Name = name,
            HeaderText = title,
            Width = width
        };

    private static bool Bool(DataGridViewRow row, string column) =>
        Convert.ToBoolean(row.Cells[column].Value ?? false);

    private static int Int(
        DataGridViewRow row,
        string column,
        int minimum,
        int maximum)
    {
        var text = Convert.ToString(row.Cells[column].Value);
        if (!int.TryParse(text, out var value))
        {
            throw new FormatException(
                $"'{text}' is not a valid integer for {column}.");
        }
        return Math.Clamp(value, minimum, maximum);
    }

    private static string ActionName(InputAction action) => action switch
    {
        InputAction.EmergencyStop => "Emergency stop",
        InputAction.ToggleHaptics => "Toggle all haptics",
        InputAction.ToggleRecording => "Start / stop recording",
        InputAction.SaveCircularBuffer => "Save circular buffer",
        InputAction.MarkImpact => "Mark impact",
        InputAction.MarkStrongKerb => "Mark strong kerb",
        InputAction.MarkLanding => "Mark landing",
        InputAction.MarkWheelDrop => "Mark wheel drop",
        InputAction.MarkFalsePositive => "Mark false positive",
        _ => action.ToString()
    };

    private static Button Button(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(8, 3, 8, 3),
        Margin = new Padding(0, 0, 7, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = color ?? Color.FromArgb(226, 232, 240),
        ForeColor = color.HasValue ? Color.White : Color.FromArgb(30, 41, 59)
    };

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 12f, FontStyle.Bold),
        ForeColor = Color.FromArgb(24, 39, 58),
        Margin = new Padding(0, 8, 0, 6)
    };

    private static Label Info(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(900, 0),
        ForeColor = Color.FromArgb(72, 84, 99),
        Margin = new Padding(0, 0, 0, 8)
    };

    private void ShowError(Exception exception) => MessageBox.Show(
        this,
        exception.Message,
        "Input bindings",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);

    private sealed record KeyChoice(int Value, string Label);
}
