using System.Diagnostics;
using System.Text;
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
    private readonly CheckBox _incidentEnabled = Check("Enable incident-point haptics");
    private readonly ComboBox _incidentPatternBasis = new();
    private readonly CheckBox _incident1xEnabled = Check("1x");
    private readonly CheckBox _incident2xEnabled = Check("2x");
    private readonly CheckBox _incident4xEnabled = Check("4x");
    private readonly CheckBox _incidentOtherEnabled = Check("Other point changes");
    private readonly CheckBox _incidentOffTrackEnabled = Check("Off track");
    private readonly CheckBox _incidentLossOfControlEnabled = Check("Loss of control");
    private readonly CheckBox _incidentContactEnabled = Check("Contact");
    private readonly CheckBox _incidentRolloverEnabled = Check("Rollover");
    private readonly CheckBox _incidentUnknownEnabled = Check("Unknown / unclassified");
    private readonly CheckBox _incidentSuppressPhysical = Check(
        "Suppress duplicate notification when a related physical impact is detected");
    private readonly NumericUpDown _incidentCooldown = Number(50, 5000, 650, 10);
    private readonly NumericUpDown _incidentEvidenceWindow = Number(250, 5000, 1400, 50);
    private readonly NumericUpDown _incident1xFreq = Number(0, 25, 12);
    private readonly NumericUpDown _incident1xDuration = Number(10, 1000, 105, 5);
    private readonly NumericUpDown _incident1xPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incident1xGap = Number(0, 1000, 0, 5);
    private readonly NumericUpDown _incident2xFreq = Number(0, 25, 16);
    private readonly NumericUpDown _incident2xDuration = Number(10, 1000, 115, 5);
    private readonly NumericUpDown _incident2xPulses = Number(1, 8, 2);
    private readonly NumericUpDown _incident2xGap = Number(0, 1000, 65, 5);
    private readonly NumericUpDown _incident4xFreq = Number(0, 25, 20);
    private readonly NumericUpDown _incident4xDuration = Number(10, 1000, 150, 5);
    private readonly NumericUpDown _incident4xPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incident4xGap = Number(0, 1000, 55, 5);
    private readonly NumericUpDown _incident4xTailFreq = Number(0, 25, 16);
    private readonly NumericUpDown _incident4xTailDuration = Number(0, 1000, 90, 5);
    private readonly NumericUpDown _incidentOtherFreq = Number(0, 25, 14);
    private readonly NumericUpDown _incidentOtherDuration = Number(10, 1000, 120, 5);
    private readonly NumericUpDown _incidentOtherPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incidentOtherGap = Number(0, 1000, 0, 5);
    private readonly NumericUpDown _incidentOffTrackFreq = Number(0, 25, 11);
    private readonly NumericUpDown _incidentOffTrackDuration = Number(10, 1000, 105, 5);
    private readonly NumericUpDown _incidentOffTrackPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incidentOffTrackGap = Number(0, 1000, 0, 5);
    private readonly NumericUpDown _incidentLossFreq = Number(0, 25, 15);
    private readonly NumericUpDown _incidentLossDuration = Number(10, 1000, 110, 5);
    private readonly NumericUpDown _incidentLossPulses = Number(1, 8, 2);
    private readonly NumericUpDown _incidentLossGap = Number(0, 1000, 70, 5);
    private readonly NumericUpDown _incidentContactFreq = Number(0, 25, 20);
    private readonly NumericUpDown _incidentContactDuration = Number(10, 1000, 155, 5);
    private readonly NumericUpDown _incidentContactPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incidentContactGap = Number(0, 1000, 0, 5);
    private readonly NumericUpDown _incidentRolloverFreq = Number(0, 25, 22);
    private readonly NumericUpDown _incidentRolloverDuration = Number(10, 1000, 125, 5);
    private readonly NumericUpDown _incidentRolloverPulses = Number(1, 8, 2);
    private readonly NumericUpDown _incidentRolloverGap = Number(0, 1000, 65, 5);
    private readonly NumericUpDown _incidentUnknownFreq = Number(0, 25, 13);
    private readonly NumericUpDown _incidentUnknownDuration = Number(10, 1000, 120, 5);
    private readonly NumericUpDown _incidentUnknownPulses = Number(1, 8, 1);
    private readonly NumericUpDown _incidentUnknownGap = Number(0, 1000, 0, 5);
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
    private readonly CheckBox _circularBufferEnabled = Check(
        "Always keep recent telemetry in memory");
    private readonly NumericUpDown _circularBufferSeconds = Number(
        10,
        300,
        60,
        5);
    private readonly CheckBox _minimizeToTray = Check(
        "Hide in the notification area when minimized");
    private readonly CheckBox _startMinimized = Check(
        "Start minimized in the notification area");
    private readonly CheckBox _startWithWindows = Check(
        "Start with the current Windows user");
    private readonly CheckBox _checkUpdatesOnStartup = Check(
        "Check GitHub for a newer release at startup");
    private readonly Label _updateStatus = StatusLabel("Update check has not run.");
    private readonly NotifyIcon _notifyIcon = new();
    private readonly CheckBox _autoProfileSelection = Check(
        "Automatically select profiles using iRacing car and track identity");
    private readonly Label _detectedCar = StatusLabel("Waiting for iRacing");
    private readonly Label _detectedTrack = StatusLabel("Waiting for iRacing");
    private readonly Label _profileSelectionStatus = StatusLabel(
        "Automatic profile selection is off.");
    private readonly ComboBox _ruleProfileCombo = new();
    private readonly TextBox _ruleName = new();
    private readonly TextBox _ruleCarPath = new();
    private readonly TextBox _ruleCarName = new();
    private readonly TextBox _ruleCarClass = new();
    private readonly TextBox _ruleTrackName = new();
    private readonly TextBox _ruleTrackConfig = new();
    private readonly NumericUpDown _rulePriority = Number(-1000, 1000, 0);
    private readonly CheckBox _ruleEnabled = Check("Rule enabled");
    private readonly DataGridView _rulesGrid = new();
    private string? _editingRuleId;
    private bool _loadingSettings;
    private bool _allowClose;
    private bool _started;

    public MainForm(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        Text = $"PSVR2 iRacing Haptics {Application.ProductVersion}";
        MinimumSize = new Size(950, 700);
        Size = new Size(1160, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 244, 247);
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            ?? SystemIcons.Application;

        ConfigureNotificationIcon();
        Controls.Add(BuildLayout());
        LoadSettings(_coordinator.Settings);
        HookCoordinator();
        Shown += OnShownAsync;
        FormClosing += OnFormClosingAsync;
        Resize += OnWindowResized;
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
        tabs.TabPages.Add(Tab(
            "Comfort calibration",
            new PhysicalCalibrationControl(_coordinator)));
        tabs.TabPages.Add(Tab(
            "Controls",
            new InputControlsControl(_coordinator)));
        tabs.TabPages.Add(Tab("Profiles", BuildProfilesTab()));
        tabs.TabPages.Add(Tab(
            "Telemetry triggers",
            new TriggerEditorControl(_coordinator)));
        tabs.TabPages.Add(Tab("Collision tuning", BuildImpactTab()));
        tabs.TabPages.Add(Tab("Vertical tuning", BuildVerticalTab()));
        tabs.TabPages.Add(Tab("Incident tuning", BuildIncidentTab()));
        tabs.TabPages.Add(Tab("Diagnostics", BuildDiagnosticsTab()));
        tabs.TabPages.Add(Tab("Calibration & simulator", BuildCalibrationTab()));
        tabs.TabPages.Add(Tab("Application", BuildApplicationTab()));
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
            ("profile", "Active profile"),
            ("autoprofile", "Automatic profile selection"),
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
        AddRow(grid, "Incident notifications", _incidentEnabled);
        AddRow(grid, "Incident point values", InlineChecks(
            _incident1xEnabled,
            _incident2xEnabled,
            _incident4xEnabled,
            _incidentOtherEnabled));

        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "Turn off any event category you do not want to feel. Detection and "
            + "diagnostic logging continue while an effect is disabled, so you can "
            + "calibrate safely without sending rumble. Light kerbs also use a lower "
            + "detection threshold and remain off by default. Incident notifications "
            + "are off by default because they may duplicate a physical collision effect."));
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

    private Control BuildProfilesTab()
    {
        var panel = ContentPanel();

        var summary = SettingsGrid();
        AddRow(summary, "Detected car", _detectedCar);
        AddRow(summary, "Detected track", _detectedTrack);
        AddRow(summary, "Selection result", _profileSelectionStatus);

        var profileButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true
        };
        var apply = Button("Apply selected profile", Color.FromArgb(38, 92, 154));
        apply.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is { } profile)
            {
                await _coordinator.ApplyProfileAsync(profile.Id);
                LoadSettings(_coordinator.Settings);
            }
        });
        var create = Button("New from current");
        create.Click += async (_, _) => await SafeUiAction(async () =>
        {
            var name = PromptForText(
                "Create profile",
                "Profile name:",
                "My profile");
            if (name is null)
            {
                return;
            }
            await _coordinator.CreateProfileAsync(name);
            LoadSettings(_coordinator.Settings);
        });
        var duplicate = Button("Duplicate");
        duplicate.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is not { } profile)
            {
                return;
            }
            var name = PromptForText(
                "Duplicate profile",
                "Name for the copy:",
                profile.Name + " copy");
            if (name is null)
            {
                return;
            }
            await _coordinator.DuplicateProfileAsync(profile.Id, name);
            LoadSettings(_coordinator.Settings);
        });
        var rename = Button("Rename");
        rename.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is not { } profile)
            {
                return;
            }
            var name = PromptForText("Rename profile", "New name:", profile.Name);
            if (name is null)
            {
                return;
            }
            await _coordinator.RenameProfileAsync(profile.Id, name);
            LoadSettings(_coordinator.Settings);
        });
        var delete = Button("Delete");
        delete.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is not { } profile)
            {
                return;
            }
            if (MessageBox.Show(
                    $"Delete profile '{profile.Name}' and all rules assigned to it?",
                    Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            await _coordinator.DeleteProfileAsync(profile.Id);
            LoadSettings(_coordinator.Settings);
        });
        var reset = Button("Reset factory profile");
        reset.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is not { } profile)
            {
                return;
            }
            if (MessageBox.Show(
                    $"Restore all detector, effect and incident values in "
                    + $"'{profile.Name}' to their factory defaults?",
                    Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            await _coordinator.ResetFactoryProfileAsync(profile.Id);
            LoadSettings(_coordinator.Settings);
        });
        var export = Button("Export JSON profile");
        export.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is not { } profile)
            {
                return;
            }
            using var dialog = new SaveFileDialog
            {
                Filter = "Haptics profile (*.psvr2haptics.json)|*.psvr2haptics.json"
                    + "|JSON files (*.json)|*.json",
                FileName = SanitizeFileName(profile.Name) + ".psvr2haptics.json",
                Title = "Export a data-only haptics profile"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            await _coordinator.ExportProfileAsync(profile.Id, dialog.FileName);
            MessageBox.Show(
                this,
                "The profile was exported. Global device and safety settings were "
                + "intentionally excluded.",
                "Profile exported",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
        var import = Button("Import JSON profile");
        import.Click += async (_, _) => await SafeUiAction(async () =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Haptics profile (*.psvr2haptics.json;*.json)"
                    + "|*.psvr2haptics.json;*.json|All files (*.*)|*.*",
                Title = "Preview and import a haptics profile"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            var preview = await _coordinator.PreviewProfileImportAsync(dialog.FileName);
            var warnings = preview.Warnings.Count == 0
                ? "No package warnings."
                : string.Join(Environment.NewLine, preview.Warnings.Select(warning =>
                    "• " + warning));
            var decision = MessageBox.Show(
                this,
                $"Profile: {preview.Name}\n"
                + $"Description: {preview.Description}\n"
                + $"Custom triggers: {preview.CustomTriggerCount} "
                + $"({preview.TriggerConditionCount} conditions)\n"
                + $"Incident haptics: "
                + $"{(preview.IncidentHapticsEnabled ? "enabled" : "disabled")}\n\n"
                + warnings
                + "\n\nImport as a new user profile and activate it?",
                "Profile import preview",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (decision != DialogResult.Yes)
            {
                return;
            }
            var imported = await _coordinator.ImportProfileAsync(dialog.FileName);
            LoadSettings(_coordinator.Settings);
            MessageBox.Show(
                this,
                $"Imported and activated '{imported.Name}'.",
                "Profile imported",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
        profileButtons.Controls.AddRange(
            [apply, create, duplicate, rename, delete, reset, export, import]);

        var automatic = SettingsGrid();
        AddRow(automatic, "Automatic selection", _autoProfileSelection);
        var saveAutomatic = Button("Save automatic-selection setting");
        saveAutomatic.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.SetAutomaticProfileSelectionAsync(
                _autoProfileSelection.Checked);
            LoadSettings(_coordinator.Settings);
        });
        AddRow(automatic, "Apply", saveAutomatic);

        ConfigureRulesGrid();
        var editor = SettingsGrid();
        _ruleProfileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        AddRow(editor, "Rule name", _ruleName);
        AddRow(editor, "Profile to activate", _ruleProfileCombo);
        AddRow(editor, "Enabled", _ruleEnabled);
        AddRow(editor, "Priority", _rulePriority);
        AddRow(editor, "Car path pattern", _ruleCarPath);
        AddRow(editor, "Car display-name pattern", _ruleCarName);
        AddRow(editor, "Car class pattern", _ruleCarClass);
        AddRow(editor, "Track name pattern", _ruleTrackName);
        AddRow(editor, "Track configuration pattern", _ruleTrackConfig);

        var ruleButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true
        };
        var capture = Button("Use detected car and track");
        capture.Click += (_, _) =>
        {
            var context = _coordinator.State.TelemetryContext;
            _ruleCarPath.Text = context.CarPath;
            _ruleCarName.Text = string.Empty;
            _ruleCarClass.Text = string.Empty;
            _ruleTrackName.Text = context.TrackName;
            _ruleTrackConfig.Text = context.TrackConfigName;
            if (string.IsNullOrWhiteSpace(_ruleName.Text))
            {
                _ruleName.Text = $"{context.CarDisplayName} — {context.TrackDisplayLabel}";
            }
        };
        var saveRule = Button("Add or update rule", Color.FromArgb(38, 92, 154));
        saveRule.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.UpsertProfileRuleAsync(ReadRuleEditor());
            _editingRuleId = null;
            ClearRuleEditor();
            LoadSettings(_coordinator.Settings);
        });
        var newRule = Button("Clear editor");
        newRule.Click += (_, _) =>
        {
            _editingRuleId = null;
            ClearRuleEditor();
        };
        var deleteRule = Button("Delete selected rule");
        deleteRule.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (_editingRuleId is null)
            {
                return;
            }
            await _coordinator.DeleteProfileRuleAsync(_editingRuleId);
            _editingRuleId = null;
            ClearRuleEditor();
            LoadSettings(_coordinator.Settings);
        });
        ruleButtons.Controls.AddRange([capture, saveRule, newRule, deleteRule]);

        panel.Controls.Add(ruleButtons);
        panel.Controls.Add(editor);
        panel.Controls.Add(Info(
            "Every populated field is an AND condition. Leave a field blank to match "
            + "any value. '*' matches any sequence and '?' matches one character. "
            + "Higher priority wins; ties prefer the more specific rule. CarPath and "
            + "TrackName are the most stable identifiers."));
        panel.Controls.Add(_rulesGrid);
        panel.Controls.Add(SectionTitle("Car and track assignment rules"));
        panel.Controls.Add(automatic);
        panel.Controls.Add(Info(
            "Automatic selection is optional. The app reads the slowly changing "
            + "SessionInfo YAML block exposed by the iRacing SDK; it does not use "
            + "screen recognition or filenames."));
        panel.Controls.Add(SectionTitle("Automatic selection"));
        panel.Controls.Add(profileButtons);
        panel.Controls.Add(Info(
            "Detector thresholds, enabled event categories, incident settings and "
            + "rumble patterns are stored per profile. Device choice, the emergency "
            + "switch and safety limits remain global. Factory profiles can be edited "
            + "and reset, but not renamed or deleted."));
        panel.Controls.Add(summary);
        panel.Controls.Add(SectionTitle("Profile management"));
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

    private Control BuildIncidentTab()
    {
        var panel = ContentPanel();
        var policy = SettingsGrid();
        _incidentPatternBasis.DropDownStyle = ComboBoxStyle.DropDownList;
        _incidentPatternBasis.Items.AddRange(
        [
            "Incident point value (1x / 2x / 4x / other)",
            "Inferred incident type (off track / loss of control / contact / rollover)"
        ]);
        AddRow(policy, "Master incident switch", _incidentEnabled);
        AddRow(policy, "Choose rumble pattern by", _incidentPatternBasis);
        AddRow(policy, "Point changes", InlineChecks(
            _incident1xEnabled,
            _incident2xEnabled,
            _incident4xEnabled,
            _incidentOtherEnabled));
        AddRow(policy, "Inferred incident types", InlineChecks(
            _incidentOffTrackEnabled,
            _incidentLossOfControlEnabled,
            _incidentContactEnabled,
            _incidentRolloverEnabled,
            _incidentUnknownEnabled));
        AddRow(policy, "Duplicate protection", _incidentSuppressPhysical);
        AddRow(policy, "Incident cooldown (ms)", _incidentCooldown);
        AddRow(policy, "Evidence window (ms)", _incidentEvidenceWindow);

        var patterns = SettingsGrid();
        AddIncidentPatternRows(
            patterns,
            "1x",
            _incident1xFreq,
            _incident1xDuration,
            _incident1xPulses,
            _incident1xGap);
        AddIncidentPatternRows(
            patterns,
            "2x",
            _incident2xFreq,
            _incident2xDuration,
            _incident2xPulses,
            _incident2xGap);
        AddIncidentPatternRows(
            patterns,
            "4x",
            _incident4xFreq,
            _incident4xDuration,
            _incident4xPulses,
            _incident4xGap);
        AddRow(patterns, "4x tail frequency (Hz)", _incident4xTailFreq);
        AddRow(patterns, "4x tail duration (ms; 0 disables)", _incident4xTailDuration);
        AddIncidentPatternRows(
            patterns,
            "Other",
            _incidentOtherFreq,
            _incidentOtherDuration,
            _incidentOtherPulses,
            _incidentOtherGap);

        var typePatterns = SettingsGrid();
        AddIncidentPatternRows(
            typePatterns,
            "Off track",
            _incidentOffTrackFreq,
            _incidentOffTrackDuration,
            _incidentOffTrackPulses,
            _incidentOffTrackGap);
        AddIncidentPatternRows(
            typePatterns,
            "Loss of control",
            _incidentLossFreq,
            _incidentLossDuration,
            _incidentLossPulses,
            _incidentLossGap);
        AddIncidentPatternRows(
            typePatterns,
            "Contact",
            _incidentContactFreq,
            _incidentContactDuration,
            _incidentContactPulses,
            _incidentContactGap);
        AddIncidentPatternRows(
            typePatterns,
            "Rollover",
            _incidentRolloverFreq,
            _incidentRolloverDuration,
            _incidentRolloverPulses,
            _incidentRolloverGap);
        AddIncidentPatternRows(
            typePatterns,
            "Unknown",
            _incidentUnknownFreq,
            _incidentUnknownDuration,
            _incidentUnknownPulses,
            _incidentUnknownGap);

        panel.Controls.Add(SaveButton());
        panel.Controls.Add(typePatterns);
        panel.Controls.Add(Info(
            "Type-based patterns use the best-effort classification. They are useful "
            + "when you want off-track and contact notifications to feel different, "
            + "but they cannot be treated as official incident labels."));
        panel.Controls.Add(SectionTitle("Rumble pattern by inferred type"));
        panel.Controls.Add(patterns);
        panel.Controls.Add(SectionTitle("Rumble pattern by incident points"));
        panel.Controls.Add(policy);
        panel.Controls.Add(Info(
            "iRacing exposes PlayerCarMyIncidentCount as a cumulative integer. "
            + "The app can therefore identify the point increase (normally 1x, 2x "
            + "or 4x) exactly. The SDK does not expose the stewarding cause directly. "
            + "Off track, loss of control, contact and rollover are best-effort "
            + "classifications from track location, acceleration, rotation and nearby "
            + "physical detections. Unknown remains available so no counter change is "
            + "silently discarded. Both the point-value switch and the inferred-type "
            + "switch must be enabled for an incident to produce output; the pattern-basis "
            + "choice only decides which waveform is used."));
        panel.Controls.Add(Info(
            "Incident haptics are disabled by default. With duplicate protection on, "
            + "a contact/rollover counter change is logged but does not add a second "
            + "notification when the physical-impact detector already produced rumble. "
            + "Turn that protection off only if you deliberately want both sensations."));
        panel.Controls.Add(SectionTitle("Incident detection policy"));
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
            ("incidentcount", "Incident counter / latest change"),
            ("incident", "Latest incident classification"),
            ("context", "Detected car and track"),
            ("profile", "Active profile"),
            ("triggers", "Custom trigger evaluation"),
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
            ("Mark landing", "Landing"),
            ("Mark wheel drop", "Wheel drop"),
            ("Mark compression", "Severe compression"),
            ("Mark 1x", "1x incident"),
            ("Mark 2x", "2x incident"),
            ("Mark 4x", "4x incident"),
            ("Mark false positive", "False positive")
        })
        {
            var button = Button(buttonText);
            button.Click += async (_, _) => await SafeUiAction(
                () => _coordinator.MarkAsync(marker));
            markers.Controls.Add(button);
        }
        AddRow(recorder, "Manual marker", markers);

        AddRow(recorder, "Circular buffer", _circularBufferEnabled);
        AddRow(recorder, "Seconds kept in memory", _circularBufferSeconds);
        var saveRecent = Button("Save previous telemetry now");
        saveRecent.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await SaveSettingsAsync();
            var path = await _coordinator.SaveRecentTelemetryAsync(
                marker: "Circular buffer capture");
            _recordingPath.Text = "Saved recent telemetry to " + path;
        });
        AddRow(recorder, "Capture recent history", saveRecent);

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
            "Markers are intentionally matched from 2,000 ms before the click through "
            + "250 ms after it, which accommodates normal human reaction time. Matched "
            + "means the expected event was found. Missed means it was not. Mark false "
            + "positive immediately after an unwanted detection so the advisor can "
            + "recommend a higher threshold."));
        panel.Controls.Add(Info(
            "The circular buffer keeps only recent frames in RAM and writes nothing "
            + "until you press Save previous telemetry. It is ideal when an impact "
            + "cannot be predicted in advance; the saved marker is placed at the end "
            + "of the captured history."));
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
            + "its marker within about two seconds. Use Mark false positive after any "
            + "unwanted detection. An iRacing replay may be recorded too; live replay "
            + "cannot send haptics. Stop the recording when done.\n"
            + "5. Click Compare markers. The advisor shows the matched event, timing and "
            + "peak relevant score. For controlled misses it proposes a threshold 8% "
            + "below the observed peak; for false positives it proposes an 8% margin "
            + "above the detected score.\n"
            + "6. Review every recommendation before applying it. Conflicting evidence "
            + "is never auto-applied. Replay the same JSONL after each change so the "
            + "comparison uses identical telemetry.\n"
            + "7. For a custom rule, open Telemetry triggers and use Analyze JSONL. "
            + "Compare clean-lap p95/p99 with marker-window peaks, change one condition, "
            + "save it and analyze the identical file again.\n"
            + "8. Collect several examples per car/track, then save them in a dedicated "
            + "profile and optionally add an automatic assignment rule.\n"
            + "9. Once detection is reliable, tune frequency and duration for comfort. "
            + "Cooldown only controls how soon the same event family can repeat."));
        panel.Controls.Add(Info(
            "Detection controls: sensitivity and thresholds decide when an event exists. "
            + "Feel controls: frequency, duration, pulse count and pulse gap decide how "
            + "that event feels. Keeping those two stages separate prevents stronger "
            + "rumble from hiding a poorly calibrated detector."));
        panel.Controls.Add(SectionTitle("How to calibrate"));
        return panel;
    }

    private Control BuildApplicationTab()
    {
        var panel = ContentPanel();
        var behavior = SettingsGrid();
        AddRow(behavior, "Notification area", _minimizeToTray);
        AddRow(behavior, "Startup display", _startMinimized);
        AddRow(behavior, "Windows startup", _startWithWindows);
        AddRow(behavior, "Release checks", _checkUpdatesOnStartup);
        var save = Button("Save application settings", Color.FromArgb(38, 92, 154));
        save.Click += async (_, _) => await SafeUiAction(SaveSettingsAsync);
        AddRow(behavior, "Apply", save);

        var check = Button("Check for updates now");
        check.Click += async (_, _) => await CheckForUpdatesAsync(showCurrent: true);
        AddRow(behavior, "Update", check);
        AddRow(behavior, "Update status", _updateStatus);

        panel.Controls.Add(behavior);
        panel.Controls.Add(Info(
            "Start with Windows writes one value under the current user's HKCU Run key "
            + "and requires no administrator rights. Disabling it removes that value. "
            + "The application remains portable; no service or scheduled task is created."));
        panel.Controls.Add(Info(
            "Update checks call the public GitHub latest-release endpoint and do not "
            + "download or install anything automatically."));
        panel.Controls.Add(SectionTitle("Application behavior"));
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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(Info(
            "A diagnostic bundle contains a manifest, redacted settings and up to five "
            + "recent logs. A recording is included only when you explicitly select one. "
            + "Review the ZIP before sharing it publicly."), 0, 0);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true
        };
        var bundle = Button("Create diagnostic ZIP");
        bundle.Click += async (_, _) => await CreateDiagnosticBundleAsync(
            includeRecording: false);
        var bundleWithRecording = Button("Create ZIP with selected recording");
        bundleWithRecording.Click += async (_, _) => await CreateDiagnosticBundleAsync(
            includeRecording: true);
        var openLogs = Button("Open data folder");
        openLogs.Click += (_, _) => OpenDirectory(_coordinator.DataDirectory);
        buttons.Controls.AddRange([bundle, bundleWithRecording, openLogs]);
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(_logBox, 0, 2);
        return root;
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
        var applyProfile = Button("Apply profile");
        applyProfile.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (SelectedProfile(_profileCombo) is { } profile)
            {
                await _coordinator.ApplyProfileAsync(profile.Id);
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
            await SaveSettingsAsync();
        });
        return button;
    }

    private async Task SaveSettingsAsync()
    {
        var previous = _coordinator.Settings;
        var settings = ReadSettings();
        if (settings.Application.StartWithWindows
            || settings.Application.StartWithWindows
                != previous.Application.StartWithWindows)
        {
            ApplicationIntegrationService.SetStartWithWindows(
                settings.Application.StartWithWindows);
        }
        await _coordinator.ApplySettingsAsync(settings);
        LoadSettings(_coordinator.Settings);
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

    private void ConfigureNotificationIcon()
    {
        var windowIcon = Icon ?? SystemIcons.Application;
        _notifyIcon.Icon = (System.Drawing.Icon)windowIcon.Clone();
        _notifyIcon.Text = "PSVR2 iRacing Haptics";
        _notifyIcon.Visible = false;
        _notifyIcon.DoubleClick += (_, _) => ShowFromNotificationArea();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromNotificationArea());
        menu.Items.Add("Toggle all haptics", null, async (_, _) =>
        {
            var settings = _coordinator.Settings;
            settings.HapticsEnabled = !settings.HapticsEnabled;
            await SafeUiAction(() => _coordinator.ApplySettingsAsync(settings));
        });
        menu.Items.Add("STOP ALL RUMBLE NOW", null, async (_, _) =>
            await SafeUiAction(() =>
                _coordinator.EmergencyStopAsync("notification-area command")));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            ShowFromNotificationArea();
            Close();
        });
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void OnWindowResized(object? sender, EventArgs eventArgs)
    {
        if (WindowState == FormWindowState.Minimized
            && _coordinator.Settings.Application.MinimizeToNotificationArea)
        {
            HideToNotificationArea();
        }
    }

    private void HideToNotificationArea()
    {
        _notifyIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowFromNotificationArea()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
        _notifyIcon.Visible = false;
    }

    private async Task CheckForUpdatesAsync(bool showCurrent)
    {
        try
        {
            _updateStatus.Text = "Checking GitHub…";
            var result = await ApplicationIntegrationService.CheckForUpdatesAsync(
                Application.ProductVersion);
            _updateStatus.Text = result.Message;
            if (result.UpdateAvailable)
            {
                if (!showCurrent)
                {
                    _notifyIcon.Visible = true;
                    _notifyIcon.ShowBalloonTip(
                        5000,
                        "PSVR2 iRacing Haptics update",
                        result.Message,
                        ToolTipIcon.Info);
                }
                else
                {
                    var open = MessageBox.Show(
                        this,
                        $"{result.Message}\n\nOpen the release page?",
                        "Update available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    if (open == DialogResult.Yes
                        && Uri.TryCreate(
                            result.ReleaseUrl,
                            UriKind.Absolute,
                            out var uri))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = uri.AbsoluteUri,
                            UseShellExecute = true
                        });
                    }
                }
            }
            else if (showCurrent)
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "No update available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            _updateStatus.Text = "Update check failed: " + exception.Message;
            if (showCurrent)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "Update check failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    private async void OnShownAsync(object? sender, EventArgs eventArgs)
    {
        if (_started)
        {
            return;
        }
        _started = true;
        await SafeUiAction(() => _coordinator.StartAsync());
        var application = _coordinator.Settings.Application;
        if (application.StartMinimized)
        {
            WindowState = FormWindowState.Minimized;
            HideToNotificationArea();
        }
        if (application.CheckForUpdatesOnStartup)
        {
            await CheckForUpdatesAsync(showCurrent: false);
        }
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
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
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
        SetState("profile", true, state.ActiveProfileName);
        SetState(
            "autoprofile",
            state.AutoProfileSelectionEnabled,
            state.ProfileSelectionStatus);
        _stateValues["rumble"].Text = state.RumbleDeviceStatus;
        _stateValues["telemetry"].Text = state.TelemetryStatus;
        _detectedCar.Text = state.TelemetryContext.CarDisplayName
            + (string.IsNullOrWhiteSpace(state.TelemetryContext.CarPath)
                ? ""
                : $" ({state.TelemetryContext.CarPath})");
        _detectedTrack.Text = state.TelemetryContext.TrackDisplayLabel
            + (string.IsNullOrWhiteSpace(state.TelemetryContext.TrackName)
                ? ""
                : $" ({state.TelemetryContext.TrackName})");
        _profileSelectionStatus.Text = state.ProfileSelectionStatus;
        if (!_loadingSettings
            && SelectedProfile(_profileCombo)?.Id != state.ActiveProfileId)
        {
            LoadSettings(_coordinator.Settings);
        }

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
            _diagnosticValues["incidentcount"].Text =
                $"{diagnostics.Frame.IncidentCount?.ToString() ?? "unavailable"}"
                + (diagnostics.IncidentPointDelta > 0
                    ? $" (+{diagnostics.IncidentPointDelta}x)"
                    : "");
            _diagnosticValues["context"].Text =
                $"{diagnostics.Frame.Context.CarDisplayName} / "
                + diagnostics.Frame.Context.TrackDisplayLabel;
        }
        _diagnosticValues["incident"].Text = state.LastIncident;
        _diagnosticValues["profile"].Text =
            $"{state.ActiveProfileName} — {state.ProfileSelectionStatus}";
        _diagnosticValues["triggers"].Text = !state.CustomTriggerEngineEnabled
            ? "Custom telemetry triggers are disabled for this profile."
            : state.TriggerEvaluations.Count == 0
            ? "No enabled custom triggers in this profile."
            : string.Join(
                " | ",
                state.TriggerEvaluations
                    .Where(evaluation =>
                        evaluation.Fired || evaluation.ConditionsMatched)
                    .DefaultIfEmpty(state.TriggerEvaluations[0])
                    .Take(3)
                    .Select(evaluation =>
                        $"{evaluation.TriggerName}: {evaluation.Explanation}"));
        _diagnosticValues["event"].Text = state.LastEvent;
        if (state.Rumble is not null)
        {
            _diagnosticValues["rumble"].Text = state.Rumble.LastAction;
        }
    }

    private void LoadSettings(AppSettings settings)
    {
        _loadingSettings = true;
        try
        {
            RefreshProfileControls(settings);
            _autoProfileSelection.Checked = settings.AutoProfileSelectionEnabled;
            _rumbleModeCombo.SelectedIndex = settings.UseSimulatedRumbleDevice ? 1 : 0;
            _hapticsEnabled.Checked = settings.HapticsEnabled;
            _circularBufferEnabled.Checked =
                settings.Recording.CircularBufferEnabled;
            Set(
                _circularBufferSeconds,
                settings.Recording.CircularBufferSeconds);
            _minimizeToTray.Checked =
                settings.Application.MinimizeToNotificationArea;
            _startMinimized.Checked = settings.Application.StartMinimized;
            _startWithWindows.Checked = settings.Application.StartWithWindows;
            _checkUpdatesOnStartup.Checked =
                settings.Application.CheckForUpdatesOnStartup;
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

            _incidentEnabled.Checked = settings.Incidents.Enabled;
            _incidentPatternBasis.SelectedIndex =
                settings.Incidents.PatternBasis == IncidentPatternBasis.InferredType
                    ? 1
                    : 0;
            _incident1xEnabled.Checked = settings.Incidents.OnePointEnabled;
            _incident2xEnabled.Checked = settings.Incidents.TwoPointEnabled;
            _incident4xEnabled.Checked = settings.Incidents.FourPointEnabled;
            _incidentOtherEnabled.Checked = settings.Incidents.OtherPointValuesEnabled;
            _incidentOffTrackEnabled.Checked = settings.Incidents.OffTrackEnabled;
            _incidentLossOfControlEnabled.Checked =
                settings.Incidents.LossOfControlEnabled;
            _incidentContactEnabled.Checked = settings.Incidents.ContactEnabled;
            _incidentRolloverEnabled.Checked = settings.Incidents.RolloverEnabled;
            _incidentUnknownEnabled.Checked = settings.Incidents.UnknownEnabled;
            _incidentSuppressPhysical.Checked =
                settings.Incidents.SuppressWhenPhysicalImpactDetected;
            Set(_incidentCooldown, settings.Incidents.CooldownMs);
            Set(_incidentEvidenceWindow, settings.Incidents.EvidenceWindowMs);
            LoadIncidentPattern(
                settings.Effects.Incident1x,
                _incident1xFreq,
                _incident1xDuration,
                _incident1xPulses,
                _incident1xGap);
            LoadIncidentPattern(
                settings.Effects.Incident2x,
                _incident2xFreq,
                _incident2xDuration,
                _incident2xPulses,
                _incident2xGap);
            LoadIncidentPattern(
                settings.Effects.Incident4x,
                _incident4xFreq,
                _incident4xDuration,
                _incident4xPulses,
                _incident4xGap);
            Set(_incident4xTailFreq, settings.Effects.Incident4x.TailFrequencyHz);
            Set(_incident4xTailDuration, settings.Effects.Incident4x.TailDurationMs);
            LoadIncidentPattern(
                settings.Effects.IncidentOther,
                _incidentOtherFreq,
                _incidentOtherDuration,
                _incidentOtherPulses,
                _incidentOtherGap);
            LoadIncidentPattern(
                settings.Effects.IncidentOffTrack,
                _incidentOffTrackFreq,
                _incidentOffTrackDuration,
                _incidentOffTrackPulses,
                _incidentOffTrackGap);
            LoadIncidentPattern(
                settings.Effects.IncidentLossOfControl,
                _incidentLossFreq,
                _incidentLossDuration,
                _incidentLossPulses,
                _incidentLossGap);
            LoadIncidentPattern(
                settings.Effects.IncidentContact,
                _incidentContactFreq,
                _incidentContactDuration,
                _incidentContactPulses,
                _incidentContactGap);
            LoadIncidentPattern(
                settings.Effects.IncidentRollover,
                _incidentRolloverFreq,
                _incidentRolloverDuration,
                _incidentRolloverPulses,
                _incidentRolloverGap);
            LoadIncidentPattern(
                settings.Effects.IncidentUnknown,
                _incidentUnknownFreq,
                _incidentUnknownDuration,
                _incidentUnknownPulses,
                _incidentUnknownGap);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private AppSettings ReadSettings()
    {
        var settings = _coordinator.Settings;
        settings.HapticsEnabled = _hapticsEnabled.Checked;
        settings.UseSimulatedRumbleDevice = _rumbleModeCombo.SelectedIndex == 1;
        settings.Recording.CircularBufferEnabled = _circularBufferEnabled.Checked;
        settings.Recording.CircularBufferSeconds =
            (int)_circularBufferSeconds.Value;
        settings.Application.MinimizeToNotificationArea =
            _minimizeToTray.Checked;
        settings.Application.StartMinimized = _startMinimized.Checked;
        settings.Application.StartWithWindows = _startWithWindows.Checked;
        settings.Application.CheckForUpdatesOnStartup =
            _checkUpdatesOnStartup.Checked;
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
        settings.Incidents.Enabled = _incidentEnabled.Checked;
        settings.Incidents.PatternBasis = _incidentPatternBasis.SelectedIndex == 1
            ? IncidentPatternBasis.InferredType
            : IncidentPatternBasis.PointValue;
        settings.Incidents.OnePointEnabled = _incident1xEnabled.Checked;
        settings.Incidents.TwoPointEnabled = _incident2xEnabled.Checked;
        settings.Incidents.FourPointEnabled = _incident4xEnabled.Checked;
        settings.Incidents.OtherPointValuesEnabled = _incidentOtherEnabled.Checked;
        settings.Incidents.OffTrackEnabled = _incidentOffTrackEnabled.Checked;
        settings.Incidents.LossOfControlEnabled = _incidentLossOfControlEnabled.Checked;
        settings.Incidents.ContactEnabled = _incidentContactEnabled.Checked;
        settings.Incidents.RolloverEnabled = _incidentRolloverEnabled.Checked;
        settings.Incidents.UnknownEnabled = _incidentUnknownEnabled.Checked;
        settings.Incidents.SuppressWhenPhysicalImpactDetected =
            _incidentSuppressPhysical.Checked;
        settings.Incidents.CooldownMs = (int)_incidentCooldown.Value;
        settings.Incidents.EvidenceWindowMs = (int)_incidentEvidenceWindow.Value;
        ReadIncidentPattern(
            settings.Effects.Incident1x,
            _incident1xFreq,
            _incident1xDuration,
            _incident1xPulses,
            _incident1xGap);
        ReadIncidentPattern(
            settings.Effects.Incident2x,
            _incident2xFreq,
            _incident2xDuration,
            _incident2xPulses,
            _incident2xGap);
        ReadIncidentPattern(
            settings.Effects.Incident4x,
            _incident4xFreq,
            _incident4xDuration,
            _incident4xPulses,
            _incident4xGap);
        settings.Effects.Incident4x.TailFrequencyHz =
            (byte)_incident4xTailFreq.Value;
        settings.Effects.Incident4x.TailDurationMs =
            (int)_incident4xTailDuration.Value;
        ReadIncidentPattern(
            settings.Effects.IncidentOther,
            _incidentOtherFreq,
            _incidentOtherDuration,
            _incidentOtherPulses,
            _incidentOtherGap);
        ReadIncidentPattern(
            settings.Effects.IncidentOffTrack,
            _incidentOffTrackFreq,
            _incidentOffTrackDuration,
            _incidentOffTrackPulses,
            _incidentOffTrackGap);
        ReadIncidentPattern(
            settings.Effects.IncidentLossOfControl,
            _incidentLossFreq,
            _incidentLossDuration,
            _incidentLossPulses,
            _incidentLossGap);
        ReadIncidentPattern(
            settings.Effects.IncidentContact,
            _incidentContactFreq,
            _incidentContactDuration,
            _incidentContactPulses,
            _incidentContactGap);
        ReadIncidentPattern(
            settings.Effects.IncidentRollover,
            _incidentRolloverFreq,
            _incidentRolloverDuration,
            _incidentRolloverPulses,
            _incidentRolloverGap);
        ReadIncidentPattern(
            settings.Effects.IncidentUnknown,
            _incidentUnknownFreq,
            _incidentUnknownDuration,
            _incidentUnknownPulses,
            _incidentUnknownGap);
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
            var details = BuildCalibrationReportText(report);
            var applicable = report.Recommendations.Count(x => x.CanApply);
            var result = MessageBox.Show(
                details
                + (applicable > 0
                    ? "\n\nApply the safe recommendations to the active profile now?"
                    : ""),
                "Calibration result",
                applicable > 0 ? MessageBoxButtons.YesNo : MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            if (applicable > 0 && result == DialogResult.Yes)
            {
                await _coordinator.ApplyCalibrationRecommendationsAsync(report);
                LoadSettings(_coordinator.Settings);
                MessageBox.Show(
                    $"{applicable} recommendation(s) applied to "
                    + $"'{_coordinator.Settings.ActiveProfile}'. Replay the same file "
                    + "and compare it again before collecting new telemetry.",
                    "Calibration updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        });
    }

    private async Task ChooseAndReplayAsync()
    {
        using var dialog = JsonlDialog("Select a recording to replay");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var settings = _coordinator.Settings;
        if (settings.HapticsEnabled
            && !settings.UseSimulatedRumbleDevice
            && MessageBox.Show(
                this,
                "Replaying a JSONL file runs it through the current detectors and may "
                + "send physical rumble to the headset. Choose No and select the "
                + "simulated rumble device if you only want a silent dry run.\n\n"
                + "Continue with real hardware output?",
                "Replay can send headset rumble",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        await SafeUiAction(() => _coordinator.StartReplayAsync(dialog.FileName, 1.0));
    }

    private async Task CreateDiagnosticBundleAsync(bool includeRecording)
    {
        string? recordingPath = null;
        if (includeRecording)
        {
            using var recordingDialog = JsonlDialog(
                "Select the recording to include in the diagnostic bundle");
            if (recordingDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            recordingPath = recordingDialog.FileName;
        }

        using var outputDialog = new SaveFileDialog
        {
            Filter = "Diagnostic ZIP (*.zip)|*.zip",
            FileName = $"PSVR2-Haptics-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            Title = "Save redacted diagnostic bundle"
        };
        if (outputDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await SafeUiAction(async () =>
        {
            var result = await _coordinator.CreateDiagnosticBundleAsync(
                outputDialog.FileName,
                recordingPath);
            MessageBox.Show(
                this,
                $"Diagnostic bundle created:\n{result.Path}\n\n"
                + $"Logs: {result.LogFileCount}\n"
                + $"Recording: "
                + $"{(result.IncludedRecording ? "included" : "not included")}\n"
                + $"Size: {result.SizeBytes / 1024.0:F1} KiB\n\n"
                + "Review the ZIP before sharing it.",
                "Diagnostic bundle ready",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private void ConfigureRulesGrid()
    {
        _rulesGrid.Dock = DockStyle.Top;
        _rulesGrid.Height = 190;
        _rulesGrid.ReadOnly = true;
        _rulesGrid.AllowUserToAddRows = false;
        _rulesGrid.AllowUserToDeleteRows = false;
        _rulesGrid.AllowUserToResizeRows = false;
        _rulesGrid.MultiSelect = false;
        _rulesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _rulesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _rulesGrid.RowHeadersVisible = false;
        _rulesGrid.BackgroundColor = Color.White;
        _rulesGrid.Columns.Add("ruleName", "Rule");
        _rulesGrid.Columns.Add("ruleProfile", "Profile");
        _rulesGrid.Columns.Add("rulePriority", "Priority");
        _rulesGrid.Columns.Add("ruleEnabled", "Enabled");
        _rulesGrid.Columns.Add("ruleCriteria", "Matching criteria");
        _rulesGrid.Columns["ruleName"]!.FillWeight = 20;
        _rulesGrid.Columns["ruleProfile"]!.FillWeight = 14;
        _rulesGrid.Columns["rulePriority"]!.FillWeight = 8;
        _rulesGrid.Columns["ruleEnabled"]!.FillWeight = 8;
        _rulesGrid.Columns["ruleCriteria"]!.FillWeight = 50;
        _rulesGrid.SelectionChanged += (_, _) =>
        {
            if (_loadingSettings || _rulesGrid.SelectedRows.Count == 0)
            {
                return;
            }
            var ruleId = _rulesGrid.SelectedRows[0].Tag as string;
            var rule = _coordinator.Settings.ProfileRules.FirstOrDefault(candidate =>
                candidate.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
            if (rule is not null)
            {
                LoadRuleEditor(rule);
            }
        };
    }

    private void RefreshProfileControls(AppSettings settings)
    {
        var profileItems = settings.Profiles
            .OrderByDescending(profile => profile.IsBuiltIn)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new ProfileListItem(
                profile.Id,
                profile.Name,
                profile.IsBuiltIn))
            .ToArray();

        RefreshProfileCombo(_profileCombo, profileItems, settings.ActiveProfileId);
        RefreshProfileCombo(_ruleProfileCombo, profileItems, settings.ActiveProfileId);

        _rulesGrid.Rows.Clear();
        foreach (var rule in settings.ProfileRules
                     .OrderByDescending(rule => rule.Priority)
                     .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase))
        {
            var profileName = ProfileCatalog.FindProfile(settings, rule.ProfileId)?.Name
                ?? "(missing profile)";
            var index = _rulesGrid.Rows.Add(
                rule.Name,
                profileName,
                rule.Priority,
                rule.Enabled ? "Yes" : "No",
                RuleCriteria(rule));
            _rulesGrid.Rows[index].Tag = rule.Id;
        }

        if (_editingRuleId is not null)
        {
            var row = _rulesGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Tag as string,
                        _editingRuleId,
                        StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                row.Selected = true;
            }
        }
    }

    private static void RefreshProfileCombo(
        ComboBox combo,
        IReadOnlyList<ProfileListItem> profiles,
        string selectedId)
    {
        combo.BeginUpdate();
        try
        {
            combo.Items.Clear();
            combo.Items.AddRange(profiles.Cast<object>().ToArray());
            combo.SelectedItem = profiles.FirstOrDefault(profile =>
                profile.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase));
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }
        finally
        {
            combo.EndUpdate();
        }
    }

    private static ProfileListItem? SelectedProfile(ComboBox combo) =>
        combo.SelectedItem as ProfileListItem;

    private ProfileAssignmentRule ReadRuleEditor()
    {
        var profile = SelectedProfile(_ruleProfileCombo)
            ?? throw new InvalidOperationException("Select a profile for this rule.");
        return new ProfileAssignmentRule
        {
            Id = _editingRuleId ?? Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(_ruleName.Text)
                ? "Automatic profile rule"
                : _ruleName.Text.Trim(),
            Enabled = _ruleEnabled.Checked,
            Priority = (int)_rulePriority.Value,
            ProfileId = profile.Id,
            CarPathPattern = _ruleCarPath.Text.Trim(),
            CarNamePattern = _ruleCarName.Text.Trim(),
            CarClassPattern = _ruleCarClass.Text.Trim(),
            TrackNamePattern = _ruleTrackName.Text.Trim(),
            TrackConfigPattern = _ruleTrackConfig.Text.Trim()
        };
    }

    private void LoadRuleEditor(ProfileAssignmentRule rule)
    {
        _editingRuleId = rule.Id;
        _ruleName.Text = rule.Name;
        _ruleEnabled.Checked = rule.Enabled;
        Set(_rulePriority, rule.Priority);
        _ruleCarPath.Text = rule.CarPathPattern;
        _ruleCarName.Text = rule.CarNamePattern;
        _ruleCarClass.Text = rule.CarClassPattern;
        _ruleTrackName.Text = rule.TrackNamePattern;
        _ruleTrackConfig.Text = rule.TrackConfigPattern;
        var profile = _ruleProfileCombo.Items
            .Cast<ProfileListItem>()
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(rule.ProfileId, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            _ruleProfileCombo.SelectedItem = profile;
        }
    }

    private void ClearRuleEditor()
    {
        _ruleName.Clear();
        _ruleEnabled.Checked = true;
        Set(_rulePriority, 0);
        _ruleCarPath.Clear();
        _ruleCarName.Clear();
        _ruleCarClass.Clear();
        _ruleTrackName.Clear();
        _ruleTrackConfig.Clear();
        var activeId = _coordinator.Settings.ActiveProfileId;
        _ruleProfileCombo.SelectedItem = _ruleProfileCombo.Items
            .Cast<ProfileListItem>()
            .FirstOrDefault(profile =>
                profile.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase));
        _rulesGrid.ClearSelection();
    }

    private static string RuleCriteria(ProfileAssignmentRule rule)
    {
        var parts = new List<string>();
        Add("CarPath", rule.CarPathPattern);
        Add("car name", rule.CarNamePattern);
        Add("class", rule.CarClassPattern);
        Add("track", rule.TrackNamePattern);
        Add("config", rule.TrackConfigPattern);
        return string.Join("; ", parts);

        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{label}={value}");
            }
        }
    }

    private string? PromptForText(string title, string prompt, string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 135),
            Font = Font
        };
        var label = new Label
        {
            Text = prompt,
            AutoSize = true,
            Location = new Point(14, 15)
        };
        var input = new TextBox
        {
            Text = initialValue,
            Location = new Point(14, 42),
            Width = 400
        };
        var ok = Button("OK", Color.FromArgb(38, 92, 154));
        ok.DialogResult = DialogResult.OK;
        ok.Location = new Point(245, 88);
        var cancel = Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Location = new Point(335, 88);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        dialog.Controls.AddRange([label, input, ok, cancel]);
        input.SelectAll();
        return dialog.ShowDialog(this) == DialogResult.OK
            ? input.Text.Trim()
            : null;
    }

    private static string BuildCalibrationReportText(CalibrationReport report)
    {
        var text = new StringBuilder();
        text.AppendLine($"Markers: {report.MarkerCount}");
        text.AppendLine($"Matched: {report.MatchedCount}");
        text.AppendLine($"Missed: {report.MissedCount}");
        text.AppendLine($"Unmarked detections: {report.UnmarkedDetectionCount}");
        if (report.Matches.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Marker details:");
            foreach (var match in report.Matches.Take(12))
            {
                text.Append("• ").Append(match.Marker).Append(": ")
                    .AppendLine(match.Explanation);
            }
            if (report.Matches.Count > 12)
            {
                text.AppendLine($"• …and {report.Matches.Count - 12} more marker(s).");
            }
        }
        if (report.Recommendations.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Recommendations:");
            foreach (var recommendation in report.Recommendations)
            {
                text.Append("• ")
                    .Append(recommendation.SettingPath)
                    .Append(": ");
                if (recommendation.CanApply)
                {
                    text.Append(recommendation.CurrentValue.ToString("F2"))
                        .Append(" → ")
                        .Append(recommendation.SuggestedValue.ToString("F2"))
                        .Append(". ");
                }
                text.AppendLine(recommendation.Reason);
            }
        }
        if (report.TriggerSummaries.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Custom trigger dry run:");
            foreach (var trigger in report.TriggerSummaries.Take(8))
            {
                text.Append("• ")
                    .Append(trigger.TriggerName)
                    .Append(" → ")
                    .Append(trigger.TargetEvent)
                    .Append(": ")
                    .Append(trigger.FiredCount)
                    .Append(" firing(s), ")
                    .Append(trigger.MatchingFrameCount)
                    .Append('/')
                    .Append(trigger.FrameCount)
                    .AppendLine(" matching frames.");
                foreach (var condition in trigger.Conditions.Take(4))
                {
                    var unit = string.IsNullOrWhiteSpace(condition.Unit)
                        ? string.Empty
                        : $" {condition.Unit}";
                    text.Append("    ")
                        .Append(condition.Signal)
                        .Append(": p95 ")
                        .Append(condition.Percentile95?.ToString("F3") ?? "n/a")
                        .Append(unit)
                        .Append(", p99 ")
                        .Append(condition.Percentile99?.ToString("F3") ?? "n/a")
                        .Append(unit);
                    if (condition.MarkerWindowMaximum.HasValue)
                    {
                        text.Append(", marker max ")
                            .Append(condition.MarkerWindowMaximum.Value.ToString("F3"))
                            .Append(unit);
                    }
                    text.AppendLine();
                }
            }
        }
        return text.ToString().TrimEnd();
    }

    private static void AddIncidentPatternRows(
        TableLayoutPanel grid,
        string prefix,
        NumericUpDown frequency,
        NumericUpDown duration,
        NumericUpDown pulses,
        NumericUpDown gap)
    {
        AddRow(grid, $"{prefix} frequency (Hz)", frequency);
        AddRow(grid, $"{prefix} pulse duration (ms)", duration);
        AddRow(grid, $"{prefix} pulse count", pulses);
        AddRow(grid, $"{prefix} gap between pulses (ms)", gap);
    }

    private static void LoadIncidentPattern(
        EffectPatternSettings pattern,
        NumericUpDown frequency,
        NumericUpDown duration,
        NumericUpDown pulses,
        NumericUpDown gap)
    {
        Set(frequency, pattern.FrequencyHz);
        Set(duration, pattern.DurationMs);
        Set(pulses, pattern.PulseCount);
        Set(gap, pattern.GapMs);
    }

    private static void ReadIncidentPattern(
        EffectPatternSettings pattern,
        NumericUpDown frequency,
        NumericUpDown duration,
        NumericUpDown pulses,
        NumericUpDown gap)
    {
        pattern.FrequencyHz = (byte)frequency.Value;
        pattern.DurationMs = (int)duration.Value;
        pattern.PulseCount = (int)pulses.Value;
        pattern.GapMs = (int)gap.Value;
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
        TelemetryScenario.OffTrackIncident1x => "1x off-track incident",
        TelemetryScenario.LossOfControlIncident2x => "2x loss-of-control incident",
        TelemetryScenario.ContactIncident4x => "4x contact incident",
        TelemetryScenario.ConnectionLoss => "Connection loss",
        _ => scenario.ToString()
    };

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "haptics-profile" : cleaned;
    }

    private static void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private sealed record ProfileListItem(
        string Id,
        string Name,
        bool IsBuiltIn)
    {
        public override string ToString() =>
            IsBuiltIn ? $"{Name} (factory)" : Name;
    }
}
