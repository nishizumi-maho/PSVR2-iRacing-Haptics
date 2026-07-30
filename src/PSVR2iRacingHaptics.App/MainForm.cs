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
    private readonly NumericUpDown _manualDuration = Number(20, 1000, 80);
    private readonly NumericUpDown _manualPulses = Number(1, 8, 1);
    private readonly NumericUpDown _manualGap = Number(0, 1000, 30);
    private readonly CheckBox _impactEnabled = Check("Ativar batidas");
    private readonly NumericUpDown _impactSensitivity = Number(0.2m, 3, 1, 0.05m, 2);
    private readonly NumericUpDown _impactLight = Number(0.2m, 20, 1.45m, 0.05m, 2);
    private readonly NumericUpDown _impactMedium = Number(0.25m, 25, 2.85m, 0.05m, 2);
    private readonly NumericUpDown _impactStrong = Number(0.3m, 30, 5, 0.05m, 2);
    private readonly NumericUpDown _impactCooldown = Number(50, 5000, 260, 10);
    private readonly NumericUpDown _impactMinSpeed = Number(0, 100, 2.5m, 0.5m, 1);
    private readonly NumericUpDown _lightFreq = Number(0, 25, 12);
    private readonly NumericUpDown _lightDuration = Number(10, 1000, 75, 5);
    private readonly NumericUpDown _mediumFreq = Number(0, 25, 18);
    private readonly NumericUpDown _mediumDuration = Number(10, 1000, 125, 5);
    private readonly NumericUpDown _strongFreq = Number(0, 25, 24);
    private readonly NumericUpDown _strongDuration = Number(10, 1000, 145, 5);
    private readonly CheckBox _strongKerbsEnabled = Check("Ativar zebras fortes");
    private readonly CheckBox _lightKerbsEnabled = Check("Permitir zebras leves (desativado por padrão)");
    private readonly CheckBox _landingsEnabled = Check("Ativar pousos");
    private readonly CheckBox _wheelDropsEnabled = Check("Ativar quedas de roda");
    private readonly CheckBox _compressionEnabled = Check("Ativar compressões severas");
    private readonly NumericUpDown _verticalSensitivity = Number(0.2m, 3, 1, 0.05m, 2);
    private readonly NumericUpDown _kerbThreshold = Number(0.2m, 30, 2.05m, 0.05m, 2);
    private readonly NumericUpDown _landingThreshold = Number(0.2m, 30, 2.25m, 0.05m, 2);
    private readonly NumericUpDown _compressionThreshold = Number(0.2m, 40, 3.25m, 0.05m, 2);
    private readonly NumericUpDown _verticalCooldown = Number(50, 5000, 360, 10);
    private readonly NumericUpDown _kerbFreq = Number(0, 25, 13);
    private readonly NumericUpDown _kerbDuration = Number(10, 1000, 60, 5);
    private readonly NumericUpDown _landingFreq = Number(0, 25, 18);
    private readonly NumericUpDown _landingDuration = Number(10, 1000, 60, 5);
    private readonly CheckBox _landingDoublePulse = Check("Segundo pulso no pouso");
    private readonly NumericUpDown _landingGap = Number(0, 1000, 30, 5);
    private readonly ComboBox _scenarioCombo = new();
    private readonly CheckBox _telemetrySimulatorCheck = Check("Usar telemetria simulada");
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
        tabs.TabPages.Add(Tab("Estado", BuildStateTab()));
        tabs.TabPages.Add(Tab("Teste manual", BuildManualTab()));
        tabs.TabPages.Add(Tab("Batidas", BuildImpactTab()));
        tabs.TabPages.Add(Tab("Zebras e pousos", BuildVerticalTab()));
        tabs.TabPages.Add(Tab("Diagnóstico", BuildDiagnosticsTab()));
        tabs.TabPages.Add(Tab("Calibração e simulador", BuildCalibrationTab()));
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
                "A vibração do headset exige o PSVR2 Toolkit ativo e, na versão analisada, "
                + "é um recurso que requer jailbreak. O jailbreak não é executado por este "
                + "aplicativo e pode danificar o headset. Use por sua conta e risco.",
            ForeColor = Color.FromArgb(108, 69, 0),
            Font = new Font(Font, FontStyle.Bold)
        };
        var stop = Button("PARAR VIBRAÇÃO AGORA", Color.FromArgb(177, 32, 37));
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
            ("toolkit", "PSVR2 Toolkit encontrado"),
            ("dll", "DLL carregada"),
            ("api", "API inicializada"),
            ("driver", "Driver ativo"),
            ("headset", "Headset disponível"),
            ("iracing", "iRacing conectado"),
            ("incar", "Usuário no carro"),
            ("haptics", "Haptics habilitado"),
            ("rumble", "Dispositivo de vibração"),
            ("telemetry", "Fonte de telemetria")
        })
        {
            var value = StatusLabel("Aguardando");
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
        var refresh = Button("Verificar novamente");
        refresh.Click += async (_, _) => await SafeUiAction(
            async () =>
            {
                await _coordinator.SetSimulatedRumbleAsync(
                    _coordinator.Settings.UseSimulatedRumbleDevice);
            });
        var openData = Button("Abrir pasta de dados");
        openData.Click += (_, _) => OpenDirectory(_coordinator.DataDirectory);
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(openData);
        panel.Controls.Add(buttons);
        panel.Controls.Add(grid);
        panel.Controls.Add(SectionTitle("Estado das conexões"));
        return panel;
    }

    private Control BuildManualTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        _rumbleModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _rumbleModeCombo.Items.AddRange(new object[]
        {
            "PSVR2 Toolkit (hardware real)",
            "Dispositivo simulado"
        });
        AddRow(grid, "Dispositivo", _rumbleModeCombo);
        AddRow(grid, "Frequência (0–25 Hz)", _manualFrequency);
        AddRow(grid, "Duração do pulso (ms)", _manualDuration);
        AddRow(grid, "Quantidade de pulsos", _manualPulses);
        AddRow(grid, "Intervalo (ms)", _manualGap);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var applyDevice = Button("Aplicar dispositivo");
        applyDevice.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.SetSimulatedRumbleAsync(_rumbleModeCombo.SelectedIndex == 1));
        var start = Button("Iniciar teste", Color.FromArgb(25, 112, 71));
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
                    "O efeito não foi aceito. Verifique se o dispositivo está disponível "
                    + "e se haptics está habilitado.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        });
        var stop = Button("Parar imediatamente", Color.FromArgb(177, 32, 37));
        stop.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.EmergencyStopAsync("parada do teste manual"));
        buttons.Controls.Add(applyDevice);
        buttons.Controls.Add(start);
        buttons.Controls.Add(stop);

        panel.Controls.Add(buttons);
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "O teste manual independe do iRacing. Frequência é o único parâmetro "
            + "exposto pela C API; duração e pulsos são produzidos por comandos 0 Hz."));
        panel.Controls.Add(SectionTitle("Teste de vibração"));
        return panel;
    }

    private Control BuildImpactTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        AddRow(grid, "Detecção", _impactEnabled);
        AddRow(grid, "Sensibilidade", _impactSensitivity);
        AddRow(grid, "Limiar leve", _impactLight);
        AddRow(grid, "Limiar médio", _impactMedium);
        AddRow(grid, "Limiar forte", _impactStrong);
        AddRow(grid, "Cooldown (ms)", _impactCooldown);
        AddRow(grid, "Velocidade mínima (m/s)", _impactMinSpeed);
        AddRow(grid, "Batida leve: frequência (Hz)", _lightFreq);
        AddRow(grid, "Batida leve: duração (ms)", _lightDuration);
        AddRow(grid, "Batida média: frequência (Hz)", _mediumFreq);
        AddRow(grid, "Batida média: duração (ms)", _mediumDuration);
        AddRow(grid, "Batida forte: frequência (Hz)", _strongFreq);
        AddRow(grid, "Batida forte: duração inicial (ms)", _strongDuration);
        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(SectionTitle("Batidas e colisões"));
        return panel;
    }

    private Control BuildVerticalTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        AddRow(grid, "Zebras fortes", _strongKerbsEnabled);
        AddRow(grid, "Zebras leves", _lightKerbsEnabled);
        AddRow(grid, "Pousos", _landingsEnabled);
        AddRow(grid, "Quedas de roda", _wheelDropsEnabled);
        AddRow(grid, "Compressões severas", _compressionEnabled);
        AddRow(grid, "Sensibilidade vertical", _verticalSensitivity);
        AddRow(grid, "Limiar de zebra forte", _kerbThreshold);
        AddRow(grid, "Limiar de pouso", _landingThreshold);
        AddRow(grid, "Limiar de compressão severa", _compressionThreshold);
        AddRow(grid, "Cooldown vertical (ms)", _verticalCooldown);
        AddRow(grid, "Zebra: frequência (Hz)", _kerbFreq);
        AddRow(grid, "Zebra: duração (ms)", _kerbDuration);
        AddRow(grid, "Pouso: frequência (Hz)", _landingFreq);
        AddRow(grid, "Pouso: primeiro pulso (ms)", _landingDuration);
        AddRow(grid, "Pouso: padrão", _landingDoublePulse);
        AddRow(grid, "Pouso: intervalo (ms)", _landingGap);
        panel.Controls.Add(SaveButton());
        panel.Controls.Add(grid);
        panel.Controls.Add(Info(
            "As variáveis TireLF/RF/LR/RR_RumblePitch e a suspensão são usadas quando "
            + "o carro as fornece. Na ausência delas, o detector usa aceleração, jerk, "
            + "velocidade vertical e rotação."));
        panel.Controls.Add(SectionTitle("Impactos verticais"));
        return panel;
    }

    private Control BuildDiagnosticsTab()
    {
        var panel = ContentPanel();
        var grid = SettingsGrid();
        foreach (var (key, title) in new[]
        {
            ("lat", "Aceleração lateral"),
            ("long", "Aceleração longitudinal"),
            ("vert", "Aceleração vertical"),
            ("latjerk", "Jerk lateral"),
            ("longjerk", "Jerk longitudinal"),
            ("vertjerk", "Jerk vertical"),
            ("impact", "Intensidade de batida"),
            ("verticalscore", "Intensidade vertical"),
            ("event", "Evento identificado"),
            ("reason", "Motivo da classificação"),
            ("rumble", "Comando enviado")
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
        panel.Controls.Add(SectionTitle("Diagnóstico em tempo real"));
        return panel;
    }

    private Control BuildCalibrationTab()
    {
        var panel = ContentPanel();

        var simulation = SettingsGrid();
        _scenarioCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _scenarioCombo.DataSource = Enum.GetValues<TelemetryScenario>();
        AddRow(simulation, "Fonte", _telemetrySimulatorCheck);
        AddRow(simulation, "Cenário", _scenarioCombo);
        var playScenario = Button("Executar cenário");
        playScenario.Click += async (_, _) => await SafeUiAction(async () =>
        {
            if (!_telemetrySimulatorCheck.Checked)
            {
                _telemetrySimulatorCheck.Checked = true;
                await _coordinator.UseTelemetrySimulatorAsync(true);
            }
            await _coordinator.PlayScenarioAsync((TelemetryScenario)_scenarioCombo.SelectedItem!);
        });
        AddRow(simulation, "Execução", playScenario);
        _telemetrySimulatorCheck.CheckedChanged += async (_, _) => await SafeUiAction(
            () => _coordinator.UseTelemetrySimulatorAsync(_telemetrySimulatorCheck.Checked));

        var recorder = SettingsGrid();
        _recordingPath.AutoSize = true;
        _recordingPath.Text = "Nenhuma gravação ativa";
        AddRow(recorder, "Arquivo", _recordingPath);
        var recordButtons = new FlowLayoutPanel { AutoSize = true };
        var startRecord = Button("Iniciar gravação");
        startRecord.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.StartRecordingAsync();
            _recordingPath.Text = "Gravando em " + _coordinator.RecordingsDirectory;
        });
        var stopRecord = Button("Encerrar gravação");
        stopRecord.Click += async (_, _) => await SafeUiAction(async () =>
        {
            await _coordinator.StopRecordingAsync();
            _recordingPath.Text = "Gravação encerrada";
        });
        recordButtons.Controls.Add(startRecord);
        recordButtons.Controls.Add(stopRecord);
        AddRow(recorder, "Gravação JSONL", recordButtons);

        var markers = new FlowLayoutPanel { AutoSize = true };
        foreach (var marker in new[] { "Isto foi uma batida", "Isto foi uma zebra forte", "Isto foi um pouso" })
        {
            var button = Button(marker);
            button.Click += async (_, _) => await SafeUiAction(
                () => _coordinator.MarkAsync(marker));
            markers.Controls.Add(button);
        }
        AddRow(recorder, "Marcação manual", markers);

        var files = new FlowLayoutPanel { AutoSize = true };
        var analyze = Button("Comparar marcações");
        analyze.Click += async (_, _) => await ChooseAndAnalyzeAsync();
        var replay = Button("Reproduzir JSONL");
        replay.Click += async (_, _) => await ChooseAndReplayAsync();
        var stopReplay = Button("Parar replay");
        stopReplay.Click += async (_, _) => await SafeUiAction(
            () => _coordinator.StopReplayAsync());
        var openFolder = Button("Abrir gravações");
        openFolder.Click += (_, _) => OpenDirectory(_coordinator.RecordingsDirectory);
        files.Controls.Add(analyze);
        files.Controls.Add(replay);
        files.Controls.Add(stopReplay);
        files.Controls.Add(openFolder);
        AddRow(recorder, "Arquivo gravado", files);

        panel.Controls.Add(recorder);
        panel.Controls.Add(SectionTitle("Calibração e replay"));
        panel.Controls.Add(simulation);
        panel.Controls.Add(SectionTitle("Simulador de telemetria"));
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
        var applyProfile = Button("Aplicar perfil");
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
            Text = "Perfil:",
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
        var button = Button("Salvar configurações", Color.FromArgb(38, 92, 154));
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
            "A C API atual não expõe presença separada; valide pelo teste manual.");
        SetState("iracing", state.IRacingConnected, state.TelemetryStatus);
        SetState("incar", state.DriverInCar, state.DriverInCar ? "Piloto no carro" : "Fora do carro");
        SetState("haptics", state.HapticsEnabled, state.HapticsEnabled ? "Habilitado" : "Desabilitado");
        _stateValues["rumble"].Text = state.RumbleDeviceStatus;
        _stateValues["telemetry"].Text = state.TelemetryStatus;

        var diagnostics = state.Diagnostics;
        if (diagnostics is not null)
        {
            _diagnosticValues["lat"].Text =
                $"{diagnostics.Frame.LatAccelMps2:F2} m/s² (suave {diagnostics.SmoothedLatAccel:F2})";
            _diagnosticValues["long"].Text =
                $"{diagnostics.Frame.LongAccelMps2:F2} m/s² (suave {diagnostics.SmoothedLongAccel:F2})";
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
            _profileCombo.SelectedItem = "Personalizado";
        }
        _rumbleModeCombo.SelectedIndex = settings.UseSimulatedRumbleDevice ? 1 : 0;
        _impactEnabled.Checked = settings.Impacts.Enabled;
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
    }

    private AppSettings ReadSettings()
    {
        var settings = _coordinator.Settings;
        settings.ActiveProfile = "Personalizado";
        settings.UseSimulatedRumbleDevice = _rumbleModeCombo.SelectedIndex == 1;
        settings.Impacts.Enabled = _impactEnabled.Checked;
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
            _landingDoublePulse.Checked ? (byte)14 : (byte)0;
        settings.Effects.Landing.TailDurationMs =
            _landingDoublePulse.Checked ? 50 : 0;
        return settings;
    }

    private async Task ChooseAndAnalyzeAsync()
    {
        using var dialog = JsonlDialog("Selecionar gravação para comparar");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        await SafeUiAction(async () =>
        {
            var report = await _coordinator.AnalyzeRecordingAsync(dialog.FileName);
            MessageBox.Show(
                $"Marcações: {report.MarkerCount}\n"
                + $"Detectadas: {report.MatchedCount}\n"
                + $"Não detectadas: {report.MissedCount}\n"
                + $"Detecções sem marcação: {report.UnmarkedDetectionCount}",
                "Resultado da calibração",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private async Task ChooseAndReplayAsync()
    {
        using var dialog = JsonlDialog("Selecionar gravação para reproduzir");
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
            SetState(key, value.Value, value.Value ? "Disponível" : "Indisponível");
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
        Filter = "Telemetria JSONL (*.jsonl)|*.jsonl|Todos os arquivos (*.*)|*.*",
        CheckFileExists = true,
        Multiselect = false
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
