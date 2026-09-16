namespace Dh360dFeed;

internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Label _savedLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.ForestGreen,
        Text = string.Empty,
        Visible = false,
    };
    private readonly ComboBox _portBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly NumericUpDown _interval = new() { Minimum = 0.5M, Maximum = 10M, Increment = 0.5M, DecimalPlaces = 1, Width = 80 };
    private readonly ComboBox _rpmSource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _startMinimized = new() { Text = "Start minimized to tray", AutoSize = true };
    private readonly Label _hint = new()
    {
        AutoSize = false,
        Height = 48,
        Width = 440,
        ForeColor = Color.DimGray,
        Text = "LCD shows CPU temp (°C), one RPM gauge, CPU %, and RAM %. Temperature is always Celsius.",
    };

    public event EventHandler<AppConfig>? Saved;

    public SettingsForm(AppConfig config, bool isFirstRun = false)
    {
        _config = config;
        Icon = AppBranding.Icon;
        Text = isFirstRun ? "Welcome to DH360D" : "DH360D Settings";
        if (isFirstRun)
        {
            _hint.Text =
                "First-time setup: pick your COM port, click Install PawnIO (one-time), then Save. "
                + "The app runs as Administrator so CPU temp and fan RPM work. "
                + "LCD shows CPU temp (C), one RPM gauge, CPU %, and RAM %.";
            _hint.Height = 72;
        }
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 340);
        Font = new Font("Segoe UI", 9F);

        _rpmSource.Items.AddRange(["Auto (pump, else fan)", "Radiator / case fan", "Pump tachometer"]);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        AddRow(layout, row++, "COM port", PortPanel());
        AddRow(layout, row++, "Update every", _interval);
        AddRow(layout, row++, "RPM on LCD", _rpmSource);
        AddRow(layout, row++, string.Empty, _startWithWindows);
        AddRow(layout, row++, string.Empty, _startMinimized);
        AddRow(layout, row++, string.Empty, _hint);
        AddRow(layout, row++, string.Empty, _savedLabel);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12, 8, 12, 8) };
        var save = new Button { Text = "Save", Width = 90 };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Width = 90 };
        var logs = new Button { Text = "Live logs", Width = 90 };
        var driver = new Button { Text = "Install PawnIO", Width = 120 };

        save.Click += (_, _) => SaveSettings();
        logs.Click += (_, _) => ShowLiveLogs();
        driver.Click += (_, _) => PawnIoBootstrap.InstallDriver();

        buttons.Controls.Add(close);
        buttons.Controls.Add(save);
        buttons.Controls.Add(logs);
        buttons.Controls.Add(driver);

        CancelButton = close;
        Controls.Add(layout);
        Controls.Add(buttons);

        LoadValues();
        Shown += (_, _) => RefreshPorts();
    }

    public AppConfig BuildConfig()
    {
        _config.Display.IntervalSeconds = (double)_interval.Value;
        _config.Display.RpmSource = (RpmSource)_rpmSource.SelectedIndex;
        _config.Display.StartWithWindows = _startWithWindows.Checked;
        _config.Display.StartMinimized = _startMinimized.Checked;
        _config.Port = _portBox.SelectedItem?.ToString() == "Auto" ? null : _portBox.SelectedItem?.ToString();
        return _config;
    }

    private void SaveSettings()
    {
        Saved?.Invoke(this, BuildConfig());
        _savedLabel.Text = "Saved.";
        _savedLabel.Visible = true;
    }

    private static void ShowLiveLogs()
    {
        foreach (Form open in Application.OpenForms)
        {
            if (open is LiveLogsForm)
            {
                open.BringToFront();
                open.Focus();
                return;
            }
        }

        new LiveLogsForm().Show();
    }

    private void LoadValues()
    {
        _interval.Value = (decimal)Math.Clamp(_config.Display.IntervalSeconds, 0.5, 10);
        _rpmSource.SelectedIndex = Math.Clamp((int)_config.Display.RpmSource, 0, _rpmSource.Items.Count - 1);
        _startWithWindows.Checked = _config.Display.StartWithWindows;
        _startMinimized.Checked = _config.Display.StartMinimized;
    }

    private Control PortPanel()
    {
        var detect = new Button { Text = "Refresh", Width = 80 };
        detect.Click += (_, _) => RefreshPorts();
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(_portBox);
        panel.Controls.Add(detect);
        return panel;
    }

    private void RefreshPorts()
    {
        string? current = _portBox.SelectedItem?.ToString();
        _portBox.Items.Clear();
        _portBox.Items.Add("Auto");
        foreach (string port in PumpLink.CandidatePorts())
        {
            _portBox.Items.Add(port);
        }

        if (!string.IsNullOrWhiteSpace(_config.Port) && _portBox.Items.Contains(_config.Port))
        {
            _portBox.SelectedItem = _config.Port;
        }
        else if (current is not null && _portBox.Items.Contains(current))
        {
            _portBox.SelectedItem = current;
        }
        else
        {
            _portBox.SelectedIndex = 0;
        }
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}