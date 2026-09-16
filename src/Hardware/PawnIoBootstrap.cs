using System.Diagnostics;
using System.Reflection;
using LibreHardwareMonitor.PawnIo;

namespace Dh360dFeed;

internal static class PawnIoBootstrap
{
    private const string InstallerResource = "PawnIO_setup.exe";
    private const string InstallerFileName = "PawnIO_setup.exe";

    public static bool IsInstalled => PawnIo.IsInstalled;

    public static bool IsReady(out string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            message = "Windows only.";
            return false;
        }

        if (!IsElevated())
        {
            message =
                "Run as Administrator. CPU temperature and fan RPM need kernel access (PawnIO + admin).";
            return false;
        }

        if (!IsInstalled)
        {
            message =
                "PawnIO driver is not installed. Run: dh360d-feed install-driver";
            return false;
        }

        message = $"PawnIO {PawnIo.Version} ready.";
        return true;
    }

    public static int InstallDriver()
    {
        if (!IsElevated())
        {
            return RelaunchElevated("install-driver");
        }

        string? installer = ExtractInstaller();
        if (installer is null)
        {
            Console.Error.WriteLine("Bundled PawnIO installer missing. Rebuild with .\\build.ps1");
            return 1;
        }

        Console.WriteLine("Installing PawnIO driver (one-time, required for temp + fan sensors)...");
        using Process process = Process.Start(
            new ProcessStartInfo
            {
                FileName = installer,
                Arguments = "-install -silent",
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("failed to start PawnIO installer");

        process.WaitForExit();
        try
        {
            File.Delete(installer);
        }
        catch
        {
            // temp cleanup is best-effort
        }

        if (!IsInstalled)
        {
            Console.Error.WriteLine(
                "PawnIO install did not complete. Reboot if prompted, then run: dh360d-feed sensors");
            return 1;
        }

        Console.WriteLine($"PawnIO {PawnIo.Version} installed.");
        return 0;
    }

    private static string? ExtractInstaller()
    {
        Assembly assembly = typeof(PawnIoBootstrap).Assembly;
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(static n => n.EndsWith(InstallerFileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        string path = Path.Combine(Path.GetTempPath(), $"dh360d-{InstallerFileName}");
        using Stream? input = assembly.GetManifestResourceStream(resourceName);
        if (input is null)
        {
            return null;
        }

        using FileStream output = File.Create(path);
        input.CopyTo(output);
        return path;
    }

    public static bool IsElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return new System.Security.Principal.WindowsPrincipal(
            System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static int RelaunchElevated(string arguments)
    {
        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("cannot resolve executable path");

        Console.WriteLine("Requesting Administrator...");
        Process.Start(
            new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
            });
        return 0;
    }
}