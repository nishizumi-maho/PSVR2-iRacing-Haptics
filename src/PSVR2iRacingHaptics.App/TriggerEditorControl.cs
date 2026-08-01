using System.Globalization;
using System.Text;
using PSVR2iRacingHaptics.Core.Configuration;
using PSVR2iRacingHaptics.Core.Models;
using PSVR2iRacingHaptics.Core.Telemetry;

namespace PSVR2iRacingHaptics.App;

/// <summary>
/// Visual editor for profile-owned telemetry triggers. The control deliberately
/// keeps trigger detection separate from rumble-pattern editing and provides a
/// dry-run JSONL analyzer so a rule can be calibrated without iRacing running.
/// </summary>
public sealed class TriggerEditorControl : UserControl
{
    private readonly AppCoordinator _coordinator;
    private readonly DataGridView _triggerGrid = Grid();
    private readonly DataGridView _conditionGrid = Grid();
    private readonly TextBox _name = new();
    private readonly TextBox _description = new() { Multiline = true, Height = 54 };
    private readonly ComboBox _target = Combo();
    private readonly ComboBox _sourceMode = Combo();
    private readonly ComboBox _matchMode = Combo();
    private readonly CheckBox _enabled = new() { Text = "Trigger enabled", AutoSize = true };
    private readonly CheckBox _engineEnabled = new()
    {
        Text = "Enable custom telemetry triggers for this profile",
        AutoSize = true
    };
    private readonly NumericUpDown _holdMs = Number(0, 10_000, 0, 10);
    private readonly NumericUpDown _cooldownMs = Number(0, 30_000, 300, 10);
    private readonly NumericUpDown _releaseMs = Number(0, 10_000, 80, 10);
    private readonly NumericUpDown _priority = Number(1, 200, 60, 1);
    private readonly CheckBox _requireRelease = new()
    {
        Text = "Require conditions to release before retriggering",
        AutoSize = true,
        Checked = true
    };
    private readonly CheckBox _customEffect = new()
    {
        Text = "Use a trigger-specific rumble pattern",
        AutoSize = true
    };
    private readonly NumericUpDown _frequency = Number(0, 25, 14, 1);
    private readonly NumericUpDown _duration = Number(10, 1000, 120, 5);
    private readonly NumericUpDown _pulses = Number(1, 8, 1, 1);
    private readonly NumericUpDown _gap = Number(0, 1000, 0, 5);
    private readonly NumericUpDown _tailFrequency = Number(0, 25, 0, 1);
    private readonly NumericUpDown _tailDuration = Number(0, 1000, 0, 5);
    private readonly Label _analysis = new()
    {
        AutoSize = true,
        MaximumSize = new Size(760, 0),
        ForeColor = Color.FromArgb(47, 58, 72),
        Text = "Choose Analyze JSONL to dry-run every trigger against a recorded session."
    };
    private readonly Label _signalHelp = new()
    {
        AutoSize = true,
        MaximumSize = new Size(780, 0),
        ForeColor = Color.FromArgb(47, 90, 120),
        Text = "Select a condition to see the signal's source and unit."
    };
    private string? _editingId;
    private string _lastProfileId = string.Empty;

    public TriggerEditorControl(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Build();
        PopulateEnums();
        ConfigureGrids();
        RefreshFromSettings();
        _coordinator.StateChanged += OnCoordinatorStateChanged;
        Disposed += (_, _) => _coordinator.StateChanged -= OnCoordinatorStateChanged;
    }

    public void RefreshFromSettings()
    {
        var settings = _coordinator.Settings;
        _lastProfileId = settings.ActiveProfileId;
        _engineEnabled.Checked = settings.Triggers.Enabled;
        _triggerGrid.Rows.Clear();
        foreach (var trigger in settings.Triggers.CustomTriggers)
        {
            var row = _triggerGrid.Rows.Add(
                trigger.Enabled,
                trigger.Name,
                trigger.TargetEvent,
                trigger.SourceMode,
                trigger.Conditions.Count);
            _triggerGrid.Rows[row].Tag = trigger.Id;
        }

        if (_editingId is not null)
        {
            var selected = settings.Triggers.CustomTriggers.FirstOrDefault(trigger =>
                trigger.Id.Equals(_editingId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                LoadTrigger(selected);
                return;
            }
        }
        BeginNewTrigger();
    }

    private void Build()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1
        };
        split.SizeChanged += (_, _) => ApplyPreferredSplitterDistance(split);
        Controls.Add(split);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(Heading("Profile telemetry triggers"), 0, 0);
        var engineControls = Buttons();
        engineControls.Controls.Add(_engineEnabled);
        var saveEngine = ActionButton("Apply engine state");
        saveEngine.Click += async (_, _) => await SaveEngineStateAsync();
        engineControls.Controls.Add(saveEngine);
        left.Controls.Add(engineControls, 0, 1);
        left.Controls.Add(_triggerGrid, 0, 2);
        var leftButtons = Buttons();
        var create = ActionButton("New");
        create.Click += (_, _) => BeginNewTrigger();
        var duplicate = ActionButton("Duplicate");
        duplicate.Click += async (_, _) => await DuplicateSelectedAsync();
        var delete = ActionButton("Delete");
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        var reset = ActionButton("Remove all");
        reset.Click += async (_, _) => await ResetAllAsync();
        leftButtons.Controls.Add(create);
        leftButtons.Controls.Add(duplicate);
        leftButtons.Controls.Add(delete);
        leftButtons.Controls.Add(reset);
        left.Controls.Add(leftButtons, 0, 3);
        split.Panel1.Controls.Add(left);

        var editorHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var editor = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        editorHost.Controls.Add(editor);
        split.Panel2.Controls.Add(editorHost);

        editor.Controls.Add(Heading("Trigger definition"));
        editor.Controls.Add(Info(
            "A trigger decides when an event exists. Its normal rumble pattern still "
            + "comes from Effects unless the per-trigger override below is enabled. "
            + "Additive keeps the built-in detector, Replace built-in gives this rule "
            + "complete control, and Gate built-in requires both."));
        var general = FormGrid();
        AddRow(general, "Name", _name);
        AddRow(general, "Description", _description);
        AddRow(general, "Output event", _target);
        AddRow(general, "Interaction with built-in detector", _sourceMode);
        AddRow(general, "Condition logic", _matchMode);
        AddRow(general, "State", _enabled);
        AddRow(general, "Minimum matched time (ms)", _holdMs);
        AddRow(general, "Cooldown (ms)", _cooldownMs);
        AddRow(general, "Release behavior", _requireRelease);
        AddRow(general, "Release time (ms)", _releaseMs);
        AddRow(general, "Priority (1–200)", _priority);
        editor.Controls.Add(general);

        editor.Controls.Add(Heading("Conditions"));
        editor.Controls.Add(Info(
            "Raw values such as LatAccel, LongAccel and VertAccel are available "
            + "alongside filtered deltas, jerk, suspension, rotation, incident and "
            + "built-in scores. Enable Absolute for a direction-independent threshold. "
            + "FailCondition is the safe choice for a missing optional channel; use "
            + "PassCondition only when the other conditions are sufficient by themselves."));
        _conditionGrid.Height = 230;
        editor.Controls.Add(_conditionGrid);
        editor.Controls.Add(_signalHelp);
        var conditionButtons = Buttons();
        var addCondition = ActionButton("Add condition");
        addCondition.Click += (_, _) => AddCondition(new TelemetryTriggerCondition());
        var removeCondition = ActionButton("Remove selected");
        removeCondition.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _conditionGrid.SelectedRows)
            {
                if (!row.IsNewRow)
                {
                    _conditionGrid.Rows.Remove(row);
                }
            }
        };
        conditionButtons.Controls.Add(addCondition);
        conditionButtons.Controls.Add(removeCondition);
        editor.Controls.Add(conditionButtons);

        editor.Controls.Add(Heading("Optional per-trigger rumble"));
        var effect = FormGrid();
        AddRow(effect, "Pattern override", _customEffect);
        AddRow(effect, "Frequency (Hz)", _frequency);
        AddRow(effect, "Pulse duration (ms)", _duration);
        AddRow(effect, "Pulse count", _pulses);
        AddRow(effect, "Gap (ms)", _gap);
        AddRow(effect, "Tail frequency (Hz; 0 disables)", _tailFrequency);
        AddRow(effect, "Tail duration (ms)", _tailDuration);
        editor.Controls.Add(effect);

        editor.Controls.Add(Heading("Replay calibration"));
        editor.Controls.Add(Info(
            "Analyze JSONL performs a dry run: no headset command is sent. The report "
            + "shows how often the rule matched/fired and the min, median, p95, p99 and "
            + "marker-window range for every condition. Record live driving or an "
            + "iRacing replay, add markers, then adjust one condition at a time."));
        var replayButtons = Buttons();
        var analyze = ActionButton("Analyze JSONL");
        analyze.Click += async (_, _) => await AnalyzeReplayAsync();
        replayButtons.Controls.Add(analyze);
        editor.Controls.Add(replayButtons);
        editor.Controls.Add(_analysis);

        var saveButtons = Buttons();
        var save = ActionButton("Save trigger", Color.FromArgb(30, 105, 170));
        save.Click += async (_, _) => await SaveAsync();
        saveButtons.Controls.Add(save);
        editor.Controls.Add(saveButtons);
    }

    private static void ApplyPreferredSplitterDistance(SplitContainer split)
    {
        const int preferredListWidth = 330;
        var maximumDistance =
            split.ClientSize.Width - split.SplitterWidth - split.Panel2MinSize;
        if (maximumDistance < split.Panel1MinSize)
        {
            return;
        }

        var distance = Math.Clamp(
            preferredListWidth,
            split.Panel1MinSize,
            maximumDistance);
        if (split.SplitterDistance != distance)
        {
            split.SplitterDistance = distance;
        }
    }

    private void PopulateEnums()
    {
        _target.Items.AddRange(Enum.GetValues<HapticEventKind>()
            .Where(kind => kind != HapticEventKind.None)
            .Cast<object>()
            .ToArray());
        _sourceMode.Items.AddRange(Enum.GetValues<TriggerSourceMode>()
            .Cast<object>()
            .ToArray());
        _matchMode.Items.AddRange(Enum.GetValues<TriggerMatchMode>()
            .Cast<object>()
            .ToArray());
    }

    private void ConfigureGrids()
    {
        _triggerGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _triggerGrid.MultiSelect = false;
        _triggerGrid.ReadOnly = true;
        _triggerGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "On",
            Width = 38
        });
        _triggerGrid.Columns.Add("name", "Name");
        _triggerGrid.Columns.Add("target", "Event");
        _triggerGrid.Columns.Add("mode", "Mode");
        _triggerGrid.Columns.Add("conditions", "#");
        _triggerGrid.Columns["name"]!.AutoSizeMode =
            DataGridViewAutoSizeColumnMode.Fill;
        _triggerGrid.Columns["conditions"]!.Width = 34;
        _triggerGrid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0)
            {
                LoadSelectedRow();
            }
        };
        _triggerGrid.SelectionChanged += (_, _) => LoadSelectedRow();

        var signalChoices = TelemetrySignalCatalog.All
            .Select(descriptor => new SignalChoice(
                descriptor.Signal,
                string.IsNullOrWhiteSpace(descriptor.Unit)
                    ? descriptor.DisplayName
                    : $"{descriptor.DisplayName} [{descriptor.Unit}]"))
            .ToArray();
        _conditionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _conditionGrid.MultiSelect = true;
        _conditionGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "signal",
            HeaderText = "Signal",
            DataSource = signalChoices,
            DisplayMember = nameof(SignalChoice.Label),
            ValueMember = nameof(SignalChoice.Signal),
            Width = 210,
            FlatStyle = FlatStyle.Flat
        });
        _conditionGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "comparison",
            HeaderText = "Comparison",
            DataSource = Enum.GetValues<TriggerComparison>(),
            Width = 145,
            FlatStyle = FlatStyle.Flat
        });
        _conditionGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "absolute",
            HeaderText = "Absolute",
            Width = 66
        });
        _conditionGrid.Columns.Add("value", "Value");
        _conditionGrid.Columns.Add("secondValue", "Second value");
        _conditionGrid.Columns.Add("tolerance", "Equal tolerance");
        _conditionGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "missing",
            HeaderText = "If missing",
            DataSource = Enum.GetValues<MissingSignalBehavior>(),
            Width = 130,
            FlatStyle = FlatStyle.Flat
        });
        _conditionGrid.Columns["value"]!.Width = 80;
        _conditionGrid.Columns["secondValue"]!.Width = 90;
        _conditionGrid.Columns["tolerance"]!.Width = 90;
        _conditionGrid.SelectionChanged += (_, _) => UpdateSignalHelp();
        _conditionGrid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0
                && eventArgs.ColumnIndex
                    == _conditionGrid.Columns["signal"]!.Index)
            {
                UpdateSignalHelp();
            }
        };
    }

    private async Task SaveAsync()
    {
        try
        {
            _conditionGrid.EndEdit();
            var conditions = ReadConditions();
            if (conditions.Count == 0)
            {
                throw new InvalidOperationException(
                    "Add at least one telemetry condition.");
            }

            var trigger = new CustomTelemetryTrigger
            {
                Id = _editingId ?? Guid.NewGuid().ToString("N"),
                Name = _name.Text.Trim(),
                Description = _description.Text.Trim(),
                Enabled = _enabled.Checked,
                TargetEvent = Selected<HapticEventKind>(_target),
                SourceMode = Selected<TriggerSourceMode>(_sourceMode),
                MatchMode = Selected<TriggerMatchMode>(_matchMode),
                Conditions = conditions,
                HoldMilliseconds = (int)_holdMs.Value,
                CooldownMilliseconds = (int)_cooldownMs.Value,
                RequireReleaseBeforeRetrigger = _requireRelease.Checked,
                ReleaseMilliseconds = (int)_releaseMs.Value,
                Priority = (int)_priority.Value,
                UseCustomEffect = _customEffect.Checked,
                CustomEffect = new EffectPatternSettings
                {
                    FrequencyHz = (byte)_frequency.Value,
                    DurationMs = (int)_duration.Value,
                    PulseCount = (int)_pulses.Value,
                    GapMs = (int)_gap.Value,
                    TailFrequencyHz = (byte)_tailFrequency.Value,
                    TailDurationMs = (int)_tailDuration.Value
                }
            };
            if (string.IsNullOrWhiteSpace(trigger.Name))
            {
                throw new InvalidOperationException("Enter a trigger name.");
            }

            await _coordinator.UpsertTelemetryTriggerAsync(trigger);
            _editingId = trigger.Id;
            RefreshFromSettings();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task SaveEngineStateAsync()
    {
        try
        {
            await _coordinator.SetTelemetryTriggerEngineEnabledAsync(
                _engineEnabled.Checked);
            RefreshFromSettings();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private List<TelemetryTriggerCondition> ReadConditions()
    {
        var result = new List<TelemetryTriggerCondition>();
        foreach (DataGridViewRow row in _conditionGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }
            result.Add(new TelemetryTriggerCondition
            {
                Signal = CellEnum<TelemetrySignal>(row, "signal"),
                Comparison = CellEnum<TriggerComparison>(row, "comparison"),
                UseAbsoluteValue = Convert.ToBoolean(
                    row.Cells["absolute"].Value ?? false,
                    CultureInfo.InvariantCulture),
                Value = CellDouble(row, "value"),
                SecondValue = CellDouble(row, "secondValue"),
                EqualityTolerance = Math.Max(0, CellDouble(row, "tolerance")),
                MissingSignalBehavior =
                    CellEnum<MissingSignalBehavior>(row, "missing")
            });
        }
        return result;
    }

    private async Task AnalyzeReplayAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Telemetry recordings (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            InitialDirectory = _coordinator.RecordingsDirectory,
            Title = "Analyze recorded telemetry without sending rumble"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _analysis.Text = "Analyzing replay…";
            var report = await _coordinator.AnalyzeRecordingAsync(dialog.FileName);
            var summary = report.TriggerSummaries.FirstOrDefault(candidate =>
                candidate.TriggerId.Equals(
                    _editingId,
                    StringComparison.OrdinalIgnoreCase));
            _analysis.Text = summary is null
                ? "Save this trigger first, then analyze the replay again."
                : FormatSummary(summary);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private static string FormatSummary(TriggerCalibrationSummary summary)
    {
        var text = new StringBuilder();
        text.AppendLine(
            $"{summary.TriggerName} → {summary.TargetEvent}: "
            + $"{summary.FiredCount} firing(s), "
            + $"{summary.MatchingFrameCount}/{summary.FrameCount} matching frames, "
            + $"{summary.MatchedButSuppressedCount} matched but did not fire "
            + "(gate/hold/cooldown/release).");
        foreach (var condition in summary.Conditions)
        {
            var unit = string.IsNullOrWhiteSpace(condition.Unit)
                ? string.Empty
                : $" {condition.Unit}";
            text.Append($"Condition {condition.ConditionIndex + 1} — {condition.Signal}: ");
            if (!condition.Minimum.HasValue)
            {
                text.AppendLine(
                    $"no samples ({condition.MissingSampleCount} missing frames).");
                continue;
            }
            text.Append(
                $"min {condition.Minimum:F3}{unit}, median {condition.Median:F3}{unit}, "
                + $"p95 {condition.Percentile95:F3}{unit}, "
                + $"p99 {condition.Percentile99:F3}{unit}, "
                + $"max {condition.Maximum:F3}{unit}");
            if (condition.MarkerWindowMinimum.HasValue)
            {
                text.Append(
                    $"; marker window {condition.MarkerWindowMinimum:F3}–"
                    + $"{condition.MarkerWindowMaximum:F3}{unit}");
            }
            text.AppendLine(".");
        }
        text.Append(
            "Use marker-window peaks for short impacts and p95/p99 to understand "
            + "normal driving noise. Change one value, save, and analyze the same file again.");
        return text.ToString();
    }

    private async Task DuplicateSelectedAsync()
    {
        var id = SelectedTriggerId();
        if (id is null)
        {
            return;
        }
        try
        {
            var source = _coordinator.Settings.Triggers.CustomTriggers.First(trigger =>
                trigger.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            await _coordinator.DuplicateTelemetryTriggerAsync(
                id,
                $"{source.Name} copy");
            RefreshFromSettings();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var id = SelectedTriggerId();
        if (id is null)
        {
            return;
        }
        if (MessageBox.Show(
                this,
                "Delete the selected trigger from this profile?",
                "Delete trigger",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            await _coordinator.DeleteTelemetryTriggerAsync(id);
            _editingId = null;
            RefreshFromSettings();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task ResetAllAsync()
    {
        if (MessageBox.Show(
                this,
                "Remove every custom telemetry trigger from the active profile? "
                + "Built-in detection will remain available.",
                "Remove all custom triggers",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            await _coordinator.ResetTelemetryTriggersAsync();
            _editingId = null;
            RefreshFromSettings();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void LoadSelectedRow()
    {
        var id = SelectedTriggerId();
        if (id is null || id.Equals(_editingId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var trigger = _coordinator.Settings.Triggers.CustomTriggers.FirstOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (trigger is not null)
        {
            LoadTrigger(trigger);
        }
    }

    private void LoadTrigger(CustomTelemetryTrigger trigger)
    {
        _editingId = trigger.Id;
        _name.Text = trigger.Name;
        _description.Text = trigger.Description;
        _enabled.Checked = trigger.Enabled;
        _target.SelectedItem = trigger.TargetEvent;
        _sourceMode.SelectedItem = trigger.SourceMode;
        _matchMode.SelectedItem = trigger.MatchMode;
        _holdMs.Value = Bound(_holdMs, trigger.HoldMilliseconds);
        _cooldownMs.Value = Bound(_cooldownMs, trigger.CooldownMilliseconds);
        _requireRelease.Checked = trigger.RequireReleaseBeforeRetrigger;
        _releaseMs.Value = Bound(_releaseMs, trigger.ReleaseMilliseconds);
        _priority.Value = Bound(_priority, trigger.Priority);
        _customEffect.Checked = trigger.UseCustomEffect;
        _frequency.Value = Bound(_frequency, trigger.CustomEffect.FrequencyHz);
        _duration.Value = Bound(_duration, trigger.CustomEffect.DurationMs);
        _pulses.Value = Bound(_pulses, trigger.CustomEffect.PulseCount);
        _gap.Value = Bound(_gap, trigger.CustomEffect.GapMs);
        _tailFrequency.Value = Bound(
            _tailFrequency,
            trigger.CustomEffect.TailFrequencyHz);
        _tailDuration.Value = Bound(
            _tailDuration,
            trigger.CustomEffect.TailDurationMs);
        _conditionGrid.Rows.Clear();
        foreach (var condition in trigger.Conditions)
        {
            AddCondition(condition);
        }
        _analysis.Text =
            "Choose Analyze JSONL to dry-run this saved trigger against a recording.";
    }

    private void BeginNewTrigger()
    {
        _editingId = null;
        LoadTrigger(new CustomTelemetryTrigger
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "New telemetry trigger",
            Conditions =
            [
                new TelemetryTriggerCondition
                {
                    Signal = TelemetrySignal.HorizontalImpulseG,
                    Comparison = TriggerComparison.GreaterThanOrEqual,
                    Value = 1.0,
                    SecondValue = 2.0
                }
            ]
        });
        _editingId = null;
    }

    private void AddCondition(TelemetryTriggerCondition condition)
    {
        var row = _conditionGrid.Rows.Add(
            condition.Signal,
            condition.Comparison,
            condition.UseAbsoluteValue,
            condition.Value.ToString("G", CultureInfo.CurrentCulture),
            condition.SecondValue.ToString("G", CultureInfo.CurrentCulture),
            condition.EqualityTolerance.ToString("G", CultureInfo.CurrentCulture),
            condition.MissingSignalBehavior);
        _conditionGrid.CurrentCell = _conditionGrid.Rows[row].Cells["signal"];
        UpdateSignalHelp();
    }

    private void UpdateSignalHelp()
    {
        var row = _conditionGrid.CurrentRow;
        if (row is null || row.IsNewRow)
        {
            _signalHelp.Text =
                "Select a condition to see the signal's source and unit.";
            return;
        }
        var signal = CellEnum<TelemetrySignal>(row, "signal");
        var descriptor = TelemetrySignalCatalog.Describe(signal);
        var unit = string.IsNullOrWhiteSpace(descriptor.Unit)
            ? "unitless / SDK enum"
            : descriptor.Unit;
        _signalHelp.Text =
            $"{signal} — {descriptor.Description} Unit: {unit}. "
            + "The dry-run report uses the signed or absolute value selected "
            + "for this condition.";
    }

    private string? SelectedTriggerId() =>
        _triggerGrid.SelectedRows.Cast<DataGridViewRow>()
            .FirstOrDefault()?.Tag as string;

    private void OnCoordinatorStateChanged(object? sender, AppRuntimeState state)
    {
        if (state.ActiveProfileId.Equals(
                _lastProfileId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (IsDisposed)
        {
            return;
        }
        BeginInvoke((Action)RefreshFromSettings);
    }

    private static T CellEnum<T>(DataGridViewRow row, string column)
        where T : struct, Enum
    {
        var value = row.Cells[column].Value;
        if (value is T typed)
        {
            return typed;
        }
        return Enum.TryParse<T>(Convert.ToString(value), out var parsed)
            ? parsed
            : default;
    }

    private static double CellDouble(DataGridViewRow row, string column)
    {
        var text = Convert.ToString(
            row.Cells[column].Value,
            CultureInfo.CurrentCulture);
        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var currentCulture))
        {
            return currentCulture;
        }
        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var invariant))
        {
            return invariant;
        }
        throw new FormatException(
            $"'{text}' is not a valid number in column {column}.");
    }

    private static T Selected<T>(ComboBox combo)
        where T : struct, Enum =>
        combo.SelectedItem is T value
            ? value
            : throw new InvalidOperationException($"Select a {typeof(T).Name} value.");

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static ComboBox Combo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill
    };

    private static NumericUpDown Number(
        decimal minimum,
        decimal maximum,
        decimal value,
        decimal increment) => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Increment = increment,
            ThousandsSeparator = true,
            Width = 150
        };

    private static TableLayoutPanel FormGrid()
    {
        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Margin = new Padding(0, 4, 0, 14)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void AddRow(
        TableLayoutPanel grid,
        string label,
        Control control)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 7)
        }, 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        grid.Controls.Add(control, 1, row);
    }

    private static FlowLayoutPanel Buttons() => new()
    {
        AutoSize = true,
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Margin = new Padding(0, 8, 0, 8)
    };

    private static Button ActionButton(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(8, 3, 8, 3),
        BackColor = color ?? Color.FromArgb(226, 232, 240),
        ForeColor = color.HasValue ? Color.White : Color.FromArgb(30, 41, 59),
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(0, 0, 6, 0)
    };

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 11f, FontStyle.Bold),
        ForeColor = Color.FromArgb(24, 39, 58),
        Margin = new Padding(0, 6, 0, 6)
    };

    private static Label Info(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(780, 0),
        ForeColor = Color.FromArgb(72, 84, 99),
        Margin = new Padding(0, 0, 0, 8)
    };

    private static decimal Bound(NumericUpDown control, decimal value) =>
        Math.Clamp(value, control.Minimum, control.Maximum);

    private void ShowError(Exception exception) => MessageBox.Show(
        this,
        exception.Message,
        "Telemetry trigger",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);

    private sealed record SignalChoice(TelemetrySignal Signal, string Label);
}
