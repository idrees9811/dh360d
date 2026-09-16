namespace Dh360dFeed;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly FeedWorker _worker;
    private AppConfig _config;

    public TrayAppContext(bool startMinimized, bool openSettings = false, bool firstRun = false)
    {
        _config = AppConfig.Load();
        _worker = new FeedWorker(_config);
        _worker.StatusChanged += OnStatus;
        _worker.TransientError += OnTransientError;

        _tray = new NotifyIcon
        {
            Icon = (Icon)AppBranding.Icon.Clone(),
            Visible = true,
            Text = "DH360D",
        };

        _tray.ContextMenuStrip = BuildMenu();
        _tray.DoubleClick += (_, _) => OpenSettings(firstRun: false);

        ApplyStartupPreference();
        _worker.Start();

        if (openSettings)
        {
            OpenSettings(firstRun);
        }

        if (!startMinimized && !PawnIoBootstrap.IsReady(out string status))
        {
            _tray.ShowBalloonTip(8000, "DH360D", status, ToolTipIcon.Warning);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings(firstRun: false));
        menu.Items.Add("Live logs", null, (_, _) => OpenLiveLogs());
        menu.Items.Add("Restart feed", null, (_, _) => _worker.Start());
        menu.Items.Add("Install PawnIO driver", null, (_, _) => PawnIoBootstrap.InstallDriver());
        menu.Items.Add("Diagnostics", null, (_, _) => ConsoleCommands.RunSensors());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        return menu;
    }

    private void OpenSettings(bool firstRun = false)
    {
        using var form = new SettingsForm(_config, firstRun);
        form.Saved += (_, cfg) =>
        {
            _config = cfg;
            _config.Save();
            ApplyStartupPreference();
            _worker.UpdateConfig(_config);
            AppLog.Info("Settings saved");
            if (!_worker.IsRunning)
            {
                _worker.Start();
            }
        };
        form.ShowDialog();
    }

    private static void OpenLiveLogs()
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

    private void ApplyStartupPreference()
    {
        bool startupEnabled = StartupHelper.IsEnabled();
        if (_config.Display.StartWithWindows != startupEnabled)
        {
            StartupHelper.SetEnabled(_config.Display.StartWithWindows, _config.Display.StartMinimized);
        }
        else if (_config.Display.StartWithWindows)
        {
            StartupHelper.SetEnabled(true, _config.Display.StartMinimized);
        }
    }

    private void OnStatus(string message)
    {
        _tray.Text = message.Length > 63 ? message[..63] : message;
    }

    private void OnTransientError(string message)
    {
        // ACK misses are retried inline; balloons are only for reconnect-level failures.
        if (message.Contains("reconnect", StringComparison.OrdinalIgnoreCase)
            || message.Contains("retrying", StringComparison.OrdinalIgnoreCase))
        {
            _tray.ShowBalloonTip(5000, "DH360D", message, ToolTipIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _worker.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}