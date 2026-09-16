using System.Runtime.InteropServices;

namespace Dh360dFeed;

internal static class ConsoleCommands
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public static bool WantsConsole(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return false;
        }

        return args[0].ToLowerInvariant() is not "--tray" and not "--minimized" and not "--settings";
    }

    public static int Run(string[] args)
    {
        AllocConsole();
        return args[0].ToLowerInvariant() switch
        {
            "run" => RunFeed(args),
            "detect" => Detect(),
            "handshake" => Handshake(args),
            "sensors" => RunSensors(),
            "install-driver" => PawnIoBootstrap.InstallDriver(),
            "install-startup" => InstallStartup(),
            "--help" or "-h" or "help" => PrintHelp(),
            _ when args[0].StartsWith('-') => RunFeed(args),
            _ => PrintHelp(),
        };
    }

    public static int RunSensors()
    {
        Console.WriteLine($"Elevated: {PawnIoBootstrap.IsElevated()}");
        if (!PawnIoBootstrap.IsReady(out string status))
        {
            Console.WriteLine(status);
        }

        using var metrics = new HardwareMetrics();
        metrics.PrintDiagnostics();
        Console.WriteLine("\nPress Enter to close...");
        Console.ReadLine();
        return 0;
    }

    private static int RunFeed(string[] args)
    {
        var config = AppConfig.Load();
        using var worker = new FeedWorker(config);
        worker.StatusChanged += Console.WriteLine;
        worker.TransientError += text => Console.Error.WriteLine(text);
        worker.Start(reset: args.Contains("--reset"));
        Console.WriteLine("Feeding DH360D (Ctrl+C to stop)");
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static int Detect()
    {
        var config = AppConfig.Load();
        foreach (string port in PumpLink.CandidatePorts())
        {
            string marker = string.Equals(port, config.Port, StringComparison.OrdinalIgnoreCase) ? " *" : string.Empty;
            Console.WriteLine($"{port}{marker}");
        }

        return 0;
    }

    private static int Handshake(string[] args)
    {
        var config = AppConfig.Load();
        string? portArg = ArgValue(args, "--port");
        string port = PumpLink.ResolvePort(portArg, config, args.Contains("--reset"), scanAll: true);
        config.Port = port;
        config.Save();
        Console.WriteLine($"OK - DH360D responded on {port}");
        return 0;
    }

    private static int InstallStartup()
    {
        StartupHelper.SetEnabled(true);
        Console.WriteLine("Startup enabled.");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine(
            """
            DH360D - Darkflash pump LCD feeder

              (no args)           Tray app
              sensors             Hardware diagnostics
              install-driver      Install PawnIO (admin)
              handshake           Verify pump connection
              detect              List COM ports
            """);
        return 0;
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}