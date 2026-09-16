using System.Diagnostics;
using Microsoft.Win32;

namespace Dh360dFeed;

internal static class StartupHelper
{
    private const string TaskName = "DH360D";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "DH360D";

    public static bool IsEnabled() => TaskExists() || RegistryEntryExists();

    public static void SetEnabled(bool enabled, bool startMinimized = true)
    {
        if (!enabled)
        {
            DeleteTask();
            RemoveRegistryEntry();
            return;
        }

        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("cannot resolve executable path");
        string args = startMinimized ? "--minimized" : string.Empty;
        CreateLogonTask(exe, args);
        RemoveRegistryEntry();
    }

    private static bool RegistryEntryExists()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RegistryValueName) is string;
    }

    private static void RemoveRegistryEntry()
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("cannot open Run registry key");
        key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
    }

    private static bool TaskExists()
    {
        using Process process = StartSchtasks($"/Query /TN \"{TaskName}\" /FO LIST");
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void CreateLogonTask(string exe, string args)
    {
        string command = string.IsNullOrEmpty(args) ? $"\"{exe}\"" : $"\"{exe}\" {args}";
        string tr = command.Replace("\"", "\\\"");
        using Process process = StartSchtasks(
            $"/Create /TN \"{TaskName}\" /TR \"{tr}\" /SC ONLOGON /RL HIGHEST /F");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"failed to create startup task (exit {process.ExitCode})");
        }
    }

    private static void DeleteTask()
    {
        if (!TaskExists())
        {
            return;
        }

        using Process process = StartSchtasks($"/Delete /TN \"{TaskName}\" /F");
        process.WaitForExit();
    }

    private static Process StartSchtasks(string arguments) =>
        Process.Start(
            new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("failed to start schtasks.exe");
}
