using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dh360dFeed;

internal sealed class AppConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string? Port { get; set; }
    public string? LastOk { get; set; }
    public DisplaySettings Display { get; set; } = new();

    public static string ConfigDirectory => AppPaths.ConfigDirectory;

    public static string ConfigPath => AppPaths.ConfigFile;

    public static bool IsFirstRun => !File.Exists(AppPaths.ConfigFile);

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(AppPaths.ConfigFile))
            {
                return new AppConfig();
            }

            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile), JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(this, JsonOptions));
    }
}