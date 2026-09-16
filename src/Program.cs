namespace Dh360dFeed;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (ConsoleCommands.WantsConsole(args))
        {
            return ConsoleCommands.Run(args);
        }

        using var mutex = new Mutex(true, "Global\\DH360DFeed", out bool created);
        if (!created)
        {
            MessageBox.Show("DH360D is already running in the tray.", "DH360D", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        bool firstRun = AppConfig.IsFirstRun;
        bool openSettings = args.Contains("--settings") || firstRun;
        bool minimized = !openSettings
            && (args.Contains("--minimized") || AppConfig.Load().Display.StartMinimized);
        Application.Run(new TrayAppContext(minimized, openSettings, firstRun));
        return 0;
    }
}