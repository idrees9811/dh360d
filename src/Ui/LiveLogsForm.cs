namespace Dh360dFeed;

internal sealed class LiveLogsForm : Form
{
    private const int MaxVisibleLines = 300;

    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9F),
        WordWrap = false,
    };

    private readonly Label _footer = new()
    {
        Dock = DockStyle.Bottom,
        Height = 22,
        ForeColor = Color.DimGray,
        Text = $"Showing last {MaxVisibleLines} lines. Full log: {AppLog.LogFilePath}",
    };

    public LiveLogsForm()
    {
        Icon = AppBranding.Icon;
        Text = "DH360D Live Logs";
        ClientSize = new Size(700, 440);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_logBox);
        Controls.Add(_footer);

        ReloadFromBuffer();
        AppLog.LineAdded += OnLineAdded;
        FormClosed += (_, _) => AppLog.LineAdded -= OnLineAdded;
    }

    private void OnLineAdded(string line) => Append(line);

    private void ReloadFromBuffer()
    {
        _logBox.Clear();
        foreach (string line in AppLog.Snapshot())
        {
            _logBox.AppendText(line + Environment.NewLine);
        }
    }

    private void Append(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(Append, line);
            return;
        }

        _logBox.AppendText(line + Environment.NewLine);
        TrimIfNeeded();
    }

    private void TrimIfNeeded()
    {
        if (_logBox.Lines.Length <= MaxVisibleLines)
        {
            return;
        }

        ReloadFromBuffer();
    }
}