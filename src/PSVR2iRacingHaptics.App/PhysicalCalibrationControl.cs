using PSVR2iRacingHaptics.Core.Telemetry;

namespace PSVR2iRacingHaptics.App;

public sealed class PhysicalCalibrationControl : UserControl
{
    private readonly AppCoordinator _coordinator;
    private readonly PhysicalCalibrationSession _session = new();
    private readonly Label _step = TextLabel();
    private readonly Label _saved = TextLabel();
    private readonly Label _result = TextLabel();
    private readonly Button _play = ActionButton("Play current test");
    private readonly Button _notFelt = ActionButton("Not felt");
    private readonly Button _clear = ActionButton("Clear");
    private readonly Button _uncomfortable = ActionButton("Uncomfortable");

    public PhysicalCalibrationControl(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Controls.Add(Build());
        RefreshSavedResult();
        RefreshStep();
    }

    private Control Build()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18),
            BackColor = Color.White
        };
        panel.Controls.Add(Heading("Guided headset perception and comfort test"));
        panel.Controls.Add(Info(
            "This test measures what you can actually perceive on your headset. It "
            + "does not assume that a higher Hz value is stronger: the Toolkit exposes "
            + "frequency only, not a separate intensity control. Stop immediately if "
            + "the sensation is unpleasant."));
        panel.Controls.Add(Info(
            "Use the same seated position and headset fit used for racing. Play each "
            + "step once, then choose Not felt, Clear or Uncomfortable. Nothing advances "
            + "or plays automatically."));
        panel.Controls.Add(_saved);
        panel.Controls.Add(Heading("Current step"));
        panel.Controls.Add(_step);

        var testButtons = Buttons();
        _play.Click += async (_, _) => await PlayAsync();
        var stop = ActionButton("STOP NOW", Color.FromArgb(177, 32, 37));
        stop.Click += async (_, _) => await SafeAsync(
            () => _coordinator.EmergencyStopAsync("comfort calibration stop"));
        testButtons.Controls.Add(_play);
        testButtons.Controls.Add(stop);
        panel.Controls.Add(testButtons);

        var ratingButtons = Buttons();
        _notFelt.Click += async (_, _) =>
            await RateAsync(RumblePerceptionRating.NotFelt);
        _clear.Click += async (_, _) =>
            await RateAsync(RumblePerceptionRating.Clear);
        _uncomfortable.Click += async (_, _) =>
            await RateAsync(RumblePerceptionRating.Uncomfortable);
        ratingButtons.Controls.Add(_notFelt);
        ratingButtons.Controls.Add(_clear);
        ratingButtons.Controls.Add(_uncomfortable);
        panel.Controls.Add(ratingButtons);
        panel.Controls.Add(_result);

        var restart = ActionButton("Restart calibration");
        restart.Click += (_, _) =>
        {
            _session.Reset();
            _result.Text = string.Empty;
            RefreshStep();
        };
        panel.Controls.Add(restart);
        panel.Controls.Add(Info(
            "The saved result is guidance, not an automatic power boost. Event profiles "
            + "remain independently editable. A clear but conservative value is normally "
            + "better than a value that becomes tiring during an endurance race."));
        return panel;
    }

    private async Task PlayAsync()
    {
        if (_session.Phase == PhysicalCalibrationPhase.Completed)
        {
            return;
        }
        var current = _session.CurrentStep;
        await SafeAsync(async () =>
        {
            await _coordinator.PlayManualTestAsync(
                current.FrequencyHz,
                current.DurationMs,
                pulseCount: 1,
                gapMs: 0);
        });
    }

    private async Task RateAsync(RumblePerceptionRating rating)
    {
        if (_session.Phase == PhysicalCalibrationPhase.Completed)
        {
            return;
        }
        _session.Record(rating);
        if (_session.Phase == PhysicalCalibrationPhase.Completed)
        {
            await SafeAsync(async () =>
            {
                var calibration = _session.ToSettings();
                await _coordinator.SavePhysicalCalibrationAsync(calibration);
                _result.Text = calibration.UsableRangeFound
                    ? "Calibration saved.\n"
                        + $"Clearly perceptible from: "
                        + $"{calibration.MinimumClearlyPerceptibleFrequencyHz} Hz and "
                        + $"{calibration.MinimumClearlyPerceptibleDurationMs} ms\n"
                        + $"Preferred reference: {calibration.PreferredFrequencyHz} Hz / "
                        + $"{calibration.PreferredDurationMs} ms\n"
                        + $"Highest tested comfortable frequency: "
                        + $"{calibration.MaximumComfortableFrequencyHz} Hz"
                    : "No clearly perceptible and comfortable range was found. "
                        + "The app saved that result without recommending a waveform. "
                        + "Keep haptics disabled and verify the Toolkit/headset setup "
                        + "before testing again.";
                RefreshSavedResult();
            });
        }
        RefreshStep();
    }

    private void RefreshStep()
    {
        var completed = _session.Phase == PhysicalCalibrationPhase.Completed;
        _play.Enabled = !completed;
        _notFelt.Enabled = !completed;
        _clear.Enabled = !completed;
        _uncomfortable.Enabled = !completed;
        if (completed)
        {
            _step.Text = "Complete. Restart to perform a new controlled test.";
            return;
        }
        var current = _session.CurrentStep;
        _step.Text =
            $"Step {current.StepNumber} of up to {current.EstimatedTotalSteps} — "
            + $"{current.Phase}\n"
            + $"{current.FrequencyHz} Hz for {current.DurationMs} ms\n"
            + current.Instruction;
    }

    private void RefreshSavedResult()
    {
        var calibration = _coordinator.Settings.PhysicalCalibration;
        _saved.Text = calibration.Completed && calibration.UsableRangeFound
            ? "Saved reference: "
                + $"{calibration.PreferredFrequencyHz} Hz / "
                + $"{calibration.PreferredDurationMs} ms; "
                + $"comfortable tested ceiling "
                + $"{calibration.MaximumComfortableFrequencyHz} Hz."
            : calibration.Completed
                ? "The last test found no clearly perceptible and comfortable range."
            : "No physical comfort calibration has been saved yet.";
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Comfort calibration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static FlowLayoutPanel Buttons() => new()
    {
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Margin = new Padding(0, 8, 0, 8)
    };

    private static Button ActionButton(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(9, 4, 9, 4),
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
        MaximumSize = new Size(850, 0),
        ForeColor = Color.FromArgb(72, 84, 99),
        Margin = new Padding(0, 0, 0, 8)
    };

    private static Label TextLabel() => new()
    {
        AutoSize = true,
        MaximumSize = new Size(850, 0),
        ForeColor = Color.FromArgb(30, 41, 59),
        Margin = new Padding(0, 4, 0, 8)
    };
}
