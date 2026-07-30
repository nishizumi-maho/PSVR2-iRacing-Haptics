using System.Diagnostics;
using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;
using PSVR2iRacingHaptics.Core.Telemetry;

namespace PSVR2iRacingHaptics.App;

public sealed class MainForm : Form
{
    private readonly AppCoordinator _coordinator;
    private readonly Dictionary<string, Label> _stateValues = new();
    private readonly Dictionary<string, Label> _diagnosticValues = new();
    private readonly TextBox _logBox = new();
    private readonly ComboBox _profileCombo = new();
    private readonly ComboBox _rumbleModeCombo = new();
    private readonly NumericUpDown _manualFrequency = Number(0, 25, 18);
    private readonly NumericUpDown _manualDuration = Number(20, 1000, 150);
    private readonly NumericUpDown _manualPulses = Number(1, 8, 1);
    private readonly NumericUpDown _manualGap = Number(0, 1000, 30);
    private readonly CheckBox _hapticsEnabled = Check("Enable all haptic output");
    private readonly CheckBox _impactEnabled = Check("Enable collision haptics");
    private readonly CheckBox _lightImpactsEnabled = Check("Light impacts");
    private readonly CheckBox _mediumImpactsEnabled = Check("Medium impacts");
    private readonly CheckBox _strongImpactsEnabled = Check("Strong impacts");
    private readonly CheckBox _rolloverImpactsEnabled = Check("Rollover impacts");
    private readonly NumericUpDown _impactSensitivity = Number(0.2m, 3, 1, 0.05m, 2);
    private readonly NumericUpDown _impactLight = Number(0.2m, 20, 1.45m, 0.05m, 2);
    private readonly NumericUpDown _impactMedium = Number(0.25m, 25, 2.85m, 0.05m, 2);
    private readonly NumericUpDown _impactStrong = Number(0.3m, 30, 5, 0.05m, 2);
    private readonly NumericUpDown _impactCooldown = Number(50, 5000, 260, 10);
    private readonly NumericUpDown _impactMinSpeed = Number(0, 100, 2.5m, 0.5m, 1);
    private readonly NumericUpDown _lightFreq = Number(0, 25, 12);
    private readonly NumericUpDown _lightDuration = Number(10, 1000, 120, 5);
    private readonly NumericUpDown _mediumFreq = Number(0, 25, 18);
    private readonly NumericUpDown _mediumDuration = Number(10, 1000, 160, 5);
    private readonly NumericUpDown _strongFreq = Number(0, 25, 24);
    private readonly NumericUpDown _strongDuration = Number(10, 1000, 200, 5);
    private readonly CheckBox _strongKerbsEnabled = Check("Strong kerbs");
    private readonly CheckBox _lightKerbsEnabled =
        Check("Light kerbs (off by default)");
    private readonly CheckBox _landingsEnabled = Check("Car landings");
    private readonly CheckBox _wheelDropsEnabled = Check("Wheel drops");
    private readonly CheckBox _compressionEnabled = Check("Severe vertical compression");
    private readonly NumericUpDown _verticalSensitivity = Number(0.2m, 3, 1, 0.05m, 2);
    private readonly NumericUpDown _kerbThreshold = Number(0.2m, 30, 2.05m, 0.05m, 2);
    private readonly NumericUpDown _landingThreshold = Number(0.2m, 30, 2.25m, 0.05m, 2);
    private readonly NumericUpDown _compressionThreshold = Number(0.2m, 40, 3.25m, 0.05m, 2);
    private readonly NumericUpDown _verticalCooldown = Number(50, 5000, 360, 10);
    private readonly NumericUpDown _kerbFreq = Number(0, 25, 14);
    private readonly NumericUpDown _kerbDuration = Number(10, 1000, 110, 5);
    private readonly NumericUpDown _landingFreq = Number(0, 25, 19);
    private readonly NumericUpDown _landingDuration = Number(10, 1000, 140, 5);
    private readonly CheckBox _landingDoublePulse = Check("Use a second landing pulse");
    private readonly NumericUpDown _landingGap = Number(0, 1000, 60, 5);
    private readonly NumericUpDown _landingTailFrequency = Number(0, 25, 15);
    private readonly NumericUpDown _landingTailDuration = Number(10, 1000, 110, 5);
    private readonly ComboBox _scenarioCombo = new();
    private readonly CheckBox _telemetrySimulatorCheck = Check("Use simulated telemetry");
    private readonly Label _recordingPath = new();
    private bool _allowClose;
    private bool _started;

    public MainForm(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        Text = "PSVR2 iRacing Haptics";
        MinimumSize = new Size(950, 700);
        Size = new Size(1160, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 244, 247);

        Controls.Add(BuildLayout());
        LoadSettings(_coordinator.Settings);
        HookCoordinator();
        Shown += OnShownAsync;
        FormClosing += OnFormClosingAsync;
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(BuildHeader(), 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Tab("Status", BuildStateTab()));
        tabs.TabPages.Add(Tab("Effects", BuildEffectsTab()));
        tabs.TabPages.Add(Tab("Manual test", BuildManualTab()));
        tabs.TabPages.Add(Tab("Collision tuning", BuildImpactTab()));
        tabs.TabPages.Add(Tab("Vertical tuning", BuildVerticalTab()));
        tabs.TabPages.Add(Tab("Diagnostics", BuildDiagnosticsTab()));
        tabs.TabPages.Add(Tab("Calibration & simulator", BuildCalibrationTab()));
        tabs.TabPages.Add(Tab("Logs", BuildLogsTab()));
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(255, 244, 214)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var text = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            Text =
                "Headset rumble requires an active PSVR2 Toolkit and, in the version "
                + "reviewed, requires a jailbroken headset. This app does not perform "
                + "the jailbreak. The procedure can damage the headset; use it at your "
                + "own risk.",
            ForeColor = Color.FromArgb(108, 69, 0),
            Font = new Font(Font, FontStyle.Bold)
        };
        var stop = Button("STOP ALL RUMBLE NOW", Color.FromArgb(177, 32, 37));
        stop.Font = new Font(Font, FontStyle.Bold);
        stop.Padding = new Padding(8);
        stop.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.EmergencyStopAsync());
        panel.Controls.Add(text, 0, 0);
        panel.Controls.Add(stop, 1, 0);
        return panel;
    }

    private Control BuildStateTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        foreach (var (key, title) in new[]
        {
            ("toolkit", "PSVR2 Toolkit found"),
            ("dll", "C API DLL loaded"),
            ("api", "C API initialized"),
            ("driver", "Toolkit driver active"),
            ("headset", "Headset available"),
            ("iracing", "iRacing connected"),
            ("incar", "Driver in car"),
            ("haptics", "Haptics enabled"),
            ("rumble", "Rumble device"),
            ("telemetry", "Telemetry source")
        })
        {
            var value = StatusLabel("Waiting");
            _stateValues[key] = value;
            AddRow(grid, title, value);
        }

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 14, 0, 0)
        };
        var refresh = Button("Check again");
        refresh.Click += async (_, _) => await SafeUiAction(
            async () =>
            {
                await _coordinator.SetSimulatedRumbleAsync(
                    _coordinator.Settings.UseSimulatedRumbleDevice);
            });
        var openData = Button("Open data folder");
        openData.Click += (_, _) => OpenDirectory(_coordinator.DataDirectory);
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(openData);
        panel.Controls.Add(buttons);
        panel.Controls.Add(grid);
        panel.Controls.Add(SectionTitle("Connection status"));
        return panel;
    }

    private Control BuildEffectsTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        AddRow(grid, "Master switch", _hapticsEnabled);
        AddRow(grid, "All collisions", _impactEnabled);
        AddRow(grid, "Collision severity", InlineChecks(
            _lightImpactsEnabled,
            _mediumImpactsEnabled,
            _strongImpactsEnabled,
            _rolloverImpactsEnabled));
        AddRow(grid, "Kerb effects", InlineChecks(
            _strongKerbsEnabled,
            _lightKerbsEnabled));
        AddRow(grid, "Vertical effects", InlineChecks(
            _landingsEnabled,
            _wheelDropsEnabled,
            _compressionEnabled));

        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "Turn off any event category you do not want to feel. Detection and "
            + "diagnostic logging continue while an effect is disabled, so you can "
            + "calibrate safely without sending rumble. Light kerbs also use a lower "
            + "detection threshold and remain off by default."));
        panel.Controls.Add(SectionTitle("Choose which events produce rumble"));
        return panel;
    }

    private Control BuildManualTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        _rumbleModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _rumbleModeCombo.Items.AddRange(new object[]
        {
            "PSVR2 Toolkit (real hardware)",
            "Simulated rumble device"
        });
        AddRow(grid, "Device", _rumbleModeCombo);
        AddRow(grid, "Frequency (0–25 Hz)", _manualFrequency);
        AddRow(grid, "Pulse duration (ms)", _manualDuration);
        AddRow(grid, "Pulse count", _manualPulses);
        AddRow(grid, "Gap between pulses (ms)", _manualGap);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var applyDevice = Button("Apply device");
        applyDevice.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.SetSimulatedRumbleAsync(_rumbleModeCombo.SelectedIndex == 1));
        var start = Button("Start test", Color.FromArgb(25, 112, 71));
        start.Click += async (_, _) => await SafeUiAction(async () =>
        {
            var accepted = await _coordinator.PlayManualTestAsync(
                (byte)_manualFrequency.Value,
                (int)_manualDuration.Value,
                (int)_manualPulses.Value,
                (int)_manualGap.Value);
            if (!accepted)
            {
                MessageBox.Show(
                    "The effect was not accepted. Check that the selected device is "
                    + "available and that haptics are enabled.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        });
        var stop = Button("Stop immediately", Color.FromArgb(177, 32, 37));
        stop.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.EmergencyStopAsync("manual test stopped"));
        buttons.Controls.Add(applyDevice);
        buttons.Controls.Add(start);
        buttons.Controls.Add(stop);

        panel.Controls.Add(buttons);
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "The manual test does not require iRacing. Frequency is the only value "
            + "exposed by the Toolkit C API; this app creates duration, gaps and "
            + "multiple pulses by sending timed commands followed by 0 Hz."));
        panel.Controls.Add(SectionTitle("Rumble test"));
        return panel;
    }

    private Control BuildImpactTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        AddRow(grid, "Sensitivity multiplier", _impactSensitivity);
        AddRow(grid, "Light threshold", _impactLight);
        AddRow(grid, "Medium threshold", _impactMedium);
        AddRow(grid, "Strong threshold", _impactStrong);
        AddRow(grid, "Cooldown (ms)", _impactCooldown);
        AddRow(grid, "Minimum speed (m/s)", _impactMinSpeed);
        AddRow(grid, "Light impact frequency (Hz)", _lightFreq);
        AddRow(grid, "Light impact duration (ms)", _lightDuration);
        AddRow(grid, "Medium impact frequency (Hz)", _mediumFreq);
        AddRow(grid, "Medium impact duration (ms)", _mediumDuration);
        AddRow(grid, "Strong impact frequency (Hz)", _strongFreq);
        AddRow(grid, "Strong impact initial duration (ms)", _strongDuration);
        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "Thresholds decide whether an event is detected. Frequency and duration "
            + "only change how an already-detected event feels."));
        panel.Controls.Add(SectionTitle("Collision detection and feel"));
        return panel;
    }

    private Control BuildVerticalTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        AddRow(grid, "Vertical sensitivity multiplier", _verticalSensitivity);
        AddRow(grid, "Strong kerb threshold", _kerbThreshold);
        AddRow(grid, "Landing threshold", _landingThreshold);
        AddRow(grid, "Severe compression threshold", _compressionThreshold);
        AddRow(grid, "Vertical cooldown (ms)", _verticalCooldown);
        AddRow(grid, "Kerb frequency (Hz)", _kerbFreq);
        AddRow(grid, "Kerb duration (ms)", _kerbDuration);
        AddRow(grid, "Landing frequency (Hz)", _landingFreq);
        AddRow(grid, "Landing first pulse (ms)", _landingDuration);
        AddRow(grid, "Landing pattern", _landingDoublePulse);
        AddRow(grid, "Landing pulse gap (ms)", _landingGap);
        AddRow(grid, "Landing second pulse (Hz)", _landingTailFrequency);
        AddRow(grid, "Landing second pulse (ms)", _landingTailDuration);
        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "TireLF/RF/LR/RR_RumblePitch and suspension telemetry are used when the "
            + "car exposes them. Otherwise, the detector falls back to acceleration, "
            + "jerk, vertical velocity and rotation."));
        panel.Controls.Add(SectionTitle("Vertical impact detection and feel"));
        return panel;
    }

    private Control BuildDiagnosticsTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        foreach (var (key, title) in new[]
        {
            ("lat", "Lateral acceleration"),
            ("long", "Longitudinal acceleration"),
            ("vert", "Vertical acceleration"),
            ("latjerk", "Lateral jerk"),
            ("longjerk", "Longitudinal jerk"),
            ("vertjerk", "Vertical jerk"),
            ("impact", "Collision score"),
            ("verticalscore", "Vertical score"),
            ("event", "Detected event"),
            ("reason", "Classification reason"),
            ("rumble", "Last rumble command")
        })
        {
            var value = StatusLabel("—");
            if (key == "reason")
            {
                value.MaximumSize = new Size(700, 0);
            }
            _diagnosticValues[key] = value;
            AddRow(grid, title, value);
        }
        panel.Controls.Add(grid);
        panel.Controls.Add(SectionTitle("Live diagnostics"));
        return panel;
    }

    private Control BuildCalibrationTab()
    {
        var panel = ContentPanel();

        var simulation = SettingsGrid();
        _scenarioCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _scenarioCombo.DataSource = Enum.GetValues<TelemetryScenario>();
        _scenarioCombo.FormattingEnabled = true;
        _scenarioCombo.Format += (_, args) =>
        {
            if (args.ListItem is TelemetryScenario scenario)
            {
                args.Value = ScenarioDisplayName(scenario);
            }
        };
        AddRow(simulation, "Telemetry source", _telemetrySimulatorCheck);
        AddRow(simulation, "Scenario", _scenarioCombo);
        var playScenario = Button("Run scenario");
        playScenario.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (!_telemetrySimulatorCheck.Checked)
            {
                _telemetrySimulatorCheck.Checked = true;
                await _coordinator.UseTelemetrySimulatorAsync(true);
            }
            await _coordinator.PlayScenarioAsync((TelemetryScenario)_scenarioCombo.SelectedItem!);
        });
        AddRow(simulation, "Run", playScenario);
        _telemetrySimulatorCheck.CheckedChanged += async (_, _) => await SafeUiAction(
            () => _coordinator.UseTelemetrySimulatorAsync(_telemetrySimulatorCheck.Checked));

        var recorder = SettingsGrid();
        _recordingPath.AutoSize = true;
        _recordingPath.Text = "No active recording";
        AddRow(recorder, "Current file", _recordingPath);
        var recordButtons = new FlowLayoutPanel { AutoSize = true };
        var startRecord = Button("Start recording");
        startRecord.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.StartRecordingAsync();
            _recordingPath.Text = "Recording to " + _coordinator.RecordingsDirectory;
        });
        var stopRecord = Button("Stop recording");
        stopRecord.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.StopRecordingAsync();
            _recordingPath.Text = "Recording stopped";
        });
        recordButtons.Controls.Add(startRecord);
        recordButtons.Controls.Add(stopRecord);
        AddRow(recorder, "JSONL recording", recordButtons);

        var markers = new FlowLayoutPanel { AutoSize = true };
        foreach (var (buttonText, marker) in new[]
        {
            ("Mark impact", "Impact"),
            ("Mark strong kerb", "Strong kerb"),
            ("Mark landing", "Landing")
        })
        {
            var button = Button(buttonText);
            button.Click += async (_, _) => await SafeUiAction(
                () => _coordinator.MarkAsync(marker));
            markers.Controls.Add(button);
        }
        AddRow(recorder, "Manual marker", markers);

        var files = new FlowLayoutPanel { AutoSize = true };
        var analyze = Button("Compare markers");
        analyze.Click += async (_, _) => await ChooseAndAnalyzeAsync();
        var replay = Button("Replay JSONL");
        replay.Click += async (_, _) => await ChooseAndReplayAsync();
        var stopReplay = Button("Stop replay");
        stopReplay.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.StopReplayAsync());
        var openFolder = Button("Open recordings folder");
        openFolder.Click += (_, _) => OpenDirectory(_coordinator.RecordingsDirectory);
        files.Controls.Add(analyze);
        files.Controls.Add(replay);
        files.Controls.Add(stopReplay);
        files.Controls.Add(openFolder);
        AddRow(recorder, "Saved recording", files);

        panel.Controls.Add(recorder);
        panel.Controls.Add(Info(
            "Matched means the current detector found the expected event within 500 ms "
            + "of your marker. Missed means the corresponding threshold may be too high. "
            + "Unmarked detections usually indicate false positives or an event that was "
            + "not marked."));
        panel.Controls.Add(SectionTitle("Recording, markers and replay"));
        panel.Controls.Add(simulation);
        panel.Controls.Add(Info(
            "A scenario passes through the same detectors, event switches and effect "
            + "patterns as live iRacing telemetry. Select PSVR2 Toolkit (real hardware) "
            + "on the Manual test tab to feel it; the simulated rumble device only logs "
            + "commands."));
        panel.Controls.Add(SectionTitle("Telemetry simulator"));
        panel.Controls.Add(Info(
            "1. Open Effects and enable only the event categories you want to feel.\n"
            + "2. On Manual test, find a comfortable frequency and duration first. "
            + "These values change the feel, not event detection.\n"
            + "3. Apply the Default profile. In an iRacing solo test session, drive "
            + "two or three clean laps using normal braking and ordinary kerbs. The app "
            + "should remain quiet.\n"
            + "4. Start a JSONL recording. Reproduce one clear event at a time and click "
            + "its marker immediately after it happens. Stop the recording when done.\n"
            + "5. Click Compare markers. For a missed collision, lower only the matching "
            + "collision threshold by 0.10–0.20. For a missed kerb or landing, lower the "
            + "matching vertical threshold by 0.10–0.20. If normal driving triggers an "
            + "event, raise that threshold by the same amount.\n"
            + "6. Change one value at a time, save, and replay the same JSONL. Use the "
            + "Collision score and Vertical score on Diagnostics as a guide.\n"
            + "7. Once detection is reliable, tune frequency and duration for comfort. "
            + "Cooldown only controls how soon the same family of events can repeat."));
        panel.Controls.Add(Info(
            "Detection controls: sensitivity and thresholds decide when an event exists. "
            + "Feel controls: frequency, duration, pulse count and pulse gap decide how "
            + "that event feels. Keeping those two stages separate prevents stronger "
            + "rumble from hiding a poorly calibrated detector."));
        panel.Controls.Add(SectionTitle("How to calibrate"));
        return panel;
    }

    private Control BuildLogsTab()
    {
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Both;
        _logBox.WordWrap = false;
        _logBox.Font = new Font("Cascadia Mono", 9f);
        _logBox.BackColor = Color.FromArgb(25, 28, 34);
        _logBox.ForeColor = Color.Gainsboro;
        return _logBox;
    }

    private Control BuildFooter()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };
        _profileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileCombo.Items.AddRange(ProfileCatalog.Names.Cast<object>().ToArray());
        var applyProfile = Button("Apply profile");
        applyProfile.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (_profileCombo.SelectedItem is string profile)
            {
                await _coordinator.ApplyProfileAsync(profile);
                LoadSettings(_coordinator.Settings);
            }
        });
        var save = SaveButton();
        panel.Controls.Add(new Label
        {
            Text = "Profile:",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        });
        panel.Controls.Add(_profileCombo);
        panel.Controls.Add(applyProfile);
        panel.Controls.Add(save);
        return panel;
    }

    private Button SaveButton()
    {
        var button = Button("Save settings", Color.FromArgb(38, 92, 154));
        button.Dock = DockStyle.Top;
        button.Margin = new Padding(0, 12, 0, 0);
        button.Click += async (_, _) => await SafeUiAction(async () =>
        {
            var settings = ReadSettings();
            await _coordinator.ApplySettingsAsync(settings);
            LoadSettings(_coordinator.Settings);
        });
        return button;
    }

    private void HookCoordinator()
    {
        _coordinator.StateChanged += (_, state) => OnUi(() => UpdateState(state));
        _coordinator.LogLine += (_, line) => OnUi(() => AppendLog(line));
        _coordinator.EventDetected += (_, detected) => OnUi(() =>
        {
            _diagnosticValues["event"].Text =
                $"{detected.Kind} / {detected.Severity} / {detected.Score:F2}";
            _diagnosticValues["reason"].Text = detected.Reason;
        });
    }

    private async void OnShownAsync(object? sender, EventArgs eventArgs)
    {
        if (_started)
        {
            return;
        }
        _started = true;
        await SafeUiAction(() => _coordinator.StartAsync());
    }

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_allowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        Enabled = false;
        await _coordinator.DisposeAsync();
        _allowClose = true;
        Close();
    }

    private void UpdateState(AppRuntimeState state)
    {
        SetState("toolkit", state.Toolkit.PathFileFound, state.Toolkit.Message);
        SetState("dll", state.Toolkit.DllLoaded, state.Toolkit.DllPath ?? state.Toolkit.Message);
        SetState("api", state.Toolkit.ApiInitialized, $"init={state.Toolkit.InitializationResult?.ToString() ?? "—"}");
        SetState("driver", state.Toolkit.DriverActive, state.Toolkit.Message);
        SetUnknownState(
            "headset",
            state.Toolkit.HeadsetAvailable,
            "The current C API does not expose headset presence separately; "
            + "verify it with the manual test.");
        SetState("iracing", state.IRacingConnected, state.TelemetryStatus);
        SetState("incar", state.DriverInCar, state.DriverInCar ? "Driver in car" : "Out of car");
        SetState(
            "haptics",
            state.HapticsEnabled,
            state.HapticsEnabled ? "Enabled" : "Disabled");
        _stateValues["rumble"].Text = state.RumbleDeviceStatus;
        _stateValues["telemetry"].Text = state.TelemetryStatus;

        var diagnostics = state.Diagnostics;
        if (diagnostics is not null)
        {
            _diagnosticValues["lat"].Text =
                $"{diagnostics.Frame.LatAccelMps2:F2} m/s² "
                + $"(smoothed {diagnostics.SmoothedLatAccel:F2})";
            _diagnosticValues["long"].Text =
                $"{diagnostics.Frame.LongAccelMps2:F2} m/s² "
                + $"(smoothed {diagnostics.SmoothedLongAccel:F2})";
            _diagnosticValues["vert"].Text =
                $"{diagnostics.Frame.VertAccelMps2:F2} m/s² (Δ {diagnostics.VertDelta:F2})";
            _diagnosticValues["latjerk"].Text = $"{diagnostics.LatJerk:F1} m/s³";
            _diagnosticValues["longjerk"].Text = $"{diagnostics.LongJerk:F1} m/s³";
            _diagnosticValues["vertjerk"].Text = $"{diagnostics.VertJerk:F1} m/s³";
            _diagnosticValues["impact"].Text = $"{diagnostics.ImpactScore:F2}";
            _diagnosticValues["verticalscore"].Text = $"{diagnostics.VerticalScore:F2}";
        }
        _diagnosticValues["event"].Text = state.LastEvent;
        if (state.Rumble is not null)
        {
            _diagnosticValues["rumble"].Text = state.Rumble.LastAction;
        }
    }

    private void LoadSettings(AppSettings settings)
    {
        _profileCombo.SelectedItem = settings.ActiveProfile;
        if (_profileCombo.SelectedIndex < 0)
        {
            _profileCombo.SelectedItem = "Custom";
        }
        _rumbleModeCombo.SelectedIndex = settings.UseSimulatedRumbleDevice ? 1 : 0;
        _hapticsEnabled.Checked = settings.HapticsEnabled;
        _impactEnabled.Checked = settings.Impacts.Enabled;
        _lightImpactsEnabled.Checked = settings.Impacts.LightEnabled;
        _mediumImpactsEnabled.Checked = settings.Impacts.MediumEnabled;
        _strongImpactsEnabled.Checked = settings.Impacts.StrongEnabled;
        _rolloverImpactsEnabled.Checked = settings.Impacts.RolloverEnabled;
        Set(_impactSensitivity, settings.Impacts.Sensitivity);
        Set(_impactLight, settings.Impacts.LightThreshold);
        Set(_impactMedium, settings.Impacts.MediumThreshold);
        Set(_impactStrong, settings.Impacts.StrongThreshold);
        Set(_impactCooldown, settings.Impacts.CooldownMs);
        Set(_impactMinSpeed, settings.Impacts.MinimumSpeedMps);
        Set(_lightFreq, settings.Effects.LightImpact.FrequencyHz);
        Set(_lightDuration, settings.Effects.LightImpact.DurationMs);
        Set(_mediumFreq, settings.Effects.MediumImpact.FrequencyHz);
        Set(_mediumDuration, settings.Effects.MediumImpact.DurationMs);
        Set(_strongFreq, settings.Effects.StrongImpact.FrequencyHz);
        Set(_strongDuration, settings.Effects.StrongImpact.DurationMs);
        _strongKerbsEnabled.Checked = settings.Vertical.StrongKerbsEnabled;
        _lightKerbsEnabled.Checked = settings.Vertical.LightKerbsEnabled;
        _landingsEnabled.Checked = settings.Vertical.LandingsEnabled;
        _wheelDropsEnabled.Checked = settings.Vertical.WheelDropsEnabled;
        _compressionEnabled.Checked = settings.Vertical.SevereCompressionEnabled;
        Set(_verticalSensitivity, settings.Vertical.Sensitivity);
        Set(_kerbThreshold, settings.Vertical.StrongKerbThreshold);
        Set(_landingThreshold, settings.Vertical.LandingThreshold);
        Set(_compressionThreshold, settings.Vertical.SevereCompressionThreshold);
        Set(_verticalCooldown, settings.Vertical.CooldownMs);
        Set(_kerbFreq, settings.Effects.StrongKerb.FrequencyHz);
        Set(_kerbDuration, settings.Effects.StrongKerb.DurationMs);
        Set(_landingFreq, settings.Effects.Landing.FrequencyHz);
        Set(_landingDuration, settings.Effects.Landing.DurationMs);
        _landingDoublePulse.Checked = settings.Effects.Landing.TailDurationMs > 0;
        Set(_landingGap, settings.Effects.Landing.GapMs);
        Set(_landingTailFrequency, settings.Effects.Landing.TailFrequencyHz);
        Set(
            _landingTailDuration,
            settings.Effects.Landing.TailDurationMs > 0
                ? settings.Effects.Landing.TailDurationMs
                : 110);
    }

    private AppSettings ReadSettings()
    {
        var settings = _coordinator.Settings;
        settings.ActiveProfile = "Custom";
        settings.HapticsEnabled = _hapticsEnabled.Checked;
        settings.UseSimulatedRumbleDevice = _rumbleModeCombo.SelectedIndex == 1;
        settings.Impacts.Enabled = _impactEnabled.Checked;
        settings.Impacts.LightEnabled = _lightImpactsEnabled.Checked;
        settings.Impacts.MediumEnabled = _mediumImpactsEnabled.Checked;
        settings.Impacts.StrongEnabled = _strongImpactsEnabled.Checked;
        settings.Impacts.RolloverEnabled = _rolloverImpactsEnabled.Checked;
        settings.Impacts.Sensitivity = (double)_impactSensitivity.Value;
        settings.Impacts.LightThreshold = (double)_impactLight.Value;
        settings.Impacts.MediumThreshold = (double)_impactMedium.Value;
        settings.Impacts.StrongThreshold = (double)_impactStrong.Value;
        settings.Impacts.CooldownMs = (int)_impactCooldown.Value;
        settings.Impacts.MinimumSpeedMps = (double)_impactMinSpeed.Value;
        settings.Effects.LightImpact.FrequencyHz = (byte)_lightFreq.Value;
        settings.Effects.LightImpact.DurationMs = (int)_lightDuration.Value;
        settings.Effects.MediumImpact.FrequencyHz = (byte)_mediumFreq.Value;
        settings.Effects.MediumImpact.DurationMs = (int)_mediumDuration.Value;
        settings.Effects.StrongImpact.FrequencyHz = (byte)_strongFreq.Value;
        settings.Effects.StrongImpact.DurationMs = (int)_strongDuration.Value;
        settings.Vertical.StrongKerbsEnabled = _strongKerbsEnabled.Checked;
        settings.Vertical.LightKerbsEnabled = _lightKerbsEnabled.Checked;
        settings.Vertical.LandingsEnabled = _landingsEnabled.Checked;
        settings.Vertical.WheelDropsEnabled = _wheelDropsEnabled.Checked;
        settings.Vertical.SevereCompressionEnabled = _compressionEnabled.Checked;
        settings.Vertical.Sensitivity = (double)_verticalSensitivity.Value;
        settings.Vertical.StrongKerbThreshold = (double)_kerbThreshold.Value;
        settings.Vertical.LandingThreshold = (double)_landingThreshold.Value;
        settings.Vertical.SevereCompressionThreshold = (double)_compressionThreshold.Value;
        settings.Vertical.CooldownMs = (int)_verticalCooldown.Value;
        settings.Effects.StrongKerb.FrequencyHz = (byte)_kerbFreq.Value;
        settings.Effects.StrongKerb.DurationMs = (int)_kerbDuration.Value;
        settings.Effects.Landing.FrequencyHz = (byte)_landingFreq.Value;
        settings.Effects.Landing.DurationMs = (int)_landingDuration.Value;
        settings.Effects.Landing.GapMs = (int)_landingGap.Value;
        settings.Effects.Landing.TailFrequencyHz =
            _landingDoublePulse.Checked ? (byte)_landingTailFrequency.Value : (byte)0;
        settings.Effects.Landing.TailDurationMs =
            _landingDoublePulse.Checked ? (int)_landingTailDuration.Value : 0;
        return settings;
    }

    private async Task ChooseAndAnalyzeAsync()
    {
        using var dialog = JsonlDialog("Select a recording to compare");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        await SafeUiAction(async () =>
        {
            var report = await _coordinator.AnalyzeRecordingAsync(dialog.FileName);
            MessageBox.Show(
                $"Markers: {report.MarkerCount}\n"
                + $"Matched: {report.MatchedCount}\n"
                + $"Missed: {report.MissedCount}\n"
                + $"Unmarked detections: {report.UnmarkedDetectionCount}",
                "Calibration result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private async Task ChooseAndReplayAsync()
    {
        using var dialog = JsonlDialog("Select a recording to replay");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        await SafeUiAction(() => _coordinator.StartReplayAsync(dialog.FileName, 1.0));
    }

    private async Task SafeUiAction(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void AppendLog(string line)
    {
        if (_logBox.TextLength > 250_000)
        {
            _logBox.Text = _logBox.Text[^150_000..];
        }
        _logBox.AppendText(line + Environment.NewLine);
    }

    private void SetState(string key, bool value, string tooltip)
    {
        var label = _stateValues[key];
        label.Text = (value ? "● " : "○ ") + tooltip;
        label.ForeColor = value ? Color.FromArgb(24, 120, 70) : Color.FromArgb(170, 45, 45);
    }

    private void SetUnknownState(string key, bool? value, string unknownText)
    {
        if (value.HasValue)
        {
            SetState(key, value.Value, value.Value ? "Available" : "Unavailable");
            return;
        }
        var label = _stateValues[key];
        label.Text = "◐ " + unknownText;
        label.ForeColor = Color.FromArgb(150, 100, 15);
    }

    private static TabPage Tab(string text, Control content)
    {
        var tab = new TabPage(text) { Padding = new Padding(8), BackColor = Color.White };
        content.Dock = DockStyle.Fill;
        tab.Controls.Add(content);
        return tab;
    }

    private static Panel ContentPanel() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        Padding = new Padding(18),
        BackColor = Color.White
    };

    private static TableLayoutPanel SettingsGrid() => new()
    {
        AutoSize = true,
        Dock = DockStyle.Top,
        ColumnCount = 2,
        Padding = new Padding(0, 4, 0, 8)
    };

    private static void AddRow(TableLayoutPanel grid, string title, Control control)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.ColumnStyles.Clear();
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Padding = new Padding(0, 7, 12, 7),
            ForeColor = Color.FromArgb(55, 62, 72)
        };
        control.Margin = new Padding(3, 4, 3, 4);
        if (control is ComboBox or NumericUpDown)
        {
            control.Width = 260;
        }
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(0, 4, 0, 12),
        Font = new Font("Segoe UI Semibold", 15f),
        ForeColor = Color.FromArgb(29, 48, 70)
    };

    private static Label Info(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        MaximumSize = new Size(880, 0),
        Padding = new Padding(0, 4, 0, 14),
        ForeColor = Color.FromArgb(80, 88, 98)
    };

    private static Label StatusLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(0, 7, 0, 7)
    };

    private static Button Button(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        BackColor = color ?? Color.FromArgb(229, 233, 239),
        ForeColor = color.HasValue ? Color.White : Color.FromArgb(35, 45, 58),
        Padding = new Padding(7, 3, 7, 3),
        Margin = new Padding(4),
        UseVisualStyleBackColor = false
    };

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(0, 4, 0, 4)
    };

    private static FlowLayoutPanel InlineChecks(params CheckBox[] checkBoxes)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = Padding.Empty
        };
        panel.Controls.AddRange(checkBoxes.Cast<Control>().ToArray());
        return panel;
    }

    private static NumericUpDown Number(
        decimal minimum,
        decimal maximum,
        decimal value,
        decimal increment = 1,
        int decimalPlaces = 0) =>
        new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Increment = increment,
            DecimalPlaces = decimalPlaces,
            ThousandsSeparator = false
        };

    private static void Set(NumericUpDown control, double value)
    {
        var converted = (decimal)value;
        control.Value = Math.Clamp(converted, control.Minimum, control.Maximum);
    }

    private static OpenFileDialog JsonlDialog(string title) => new()
    {
        Title = title,
        Filter = "JSONL telemetry (*.jsonl)|*.jsonl|All files (*.*)|*.*",
        CheckFileExists = true,
        Multiselect = false
    };

    private static string ScenarioDisplayName(TelemetryScenario scenario) => scenario switch
    {
        TelemetryScenario.Parked => "Parked car",
        TelemetryScenario.NormalAcceleration => "Normal acceleration",
        TelemetryScenario.HardBraking => "Hard braking",
        TelemetryScenario.LightKerb => "Light kerb",
        TelemetryScenario.StrongKerb => "Strong kerb",
        TelemetryScenario.WheelDrop => "Wheel drop",
        TelemetryScenario.Landing => "Car landing",
        TelemetryScenario.SideImpact => "Side impact",
        TelemetryScenario.FrontImpact => "Front impact",
        TelemetryScenario.StrongCollision => "Strong collision",
        TelemetryScenario.Rollover => "Rollover",
        TelemetryScenario.ConnectionLoss => "Connection loss",
        _ => scenario.ToString()
    };

    private static void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
