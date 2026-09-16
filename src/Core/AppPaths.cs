namespace Dh360dFeed;

internal static class AppPaths
{
    public static string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DH360DFeed");

    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");

    public static string LogDirectory => Path.Combine(ConfigDirectory, "logs");

    public static string LogFile => Path.Combine(LogDirectory, "dh360d.log");
}
