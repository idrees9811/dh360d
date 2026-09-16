using System.Reflection;

namespace Dh360dFeed;

internal static class AppBranding
{
    private static Icon? _icon;

    public static Icon Icon => _icon ??= LoadIcon();

    private static Icon LoadIcon()
    {
        Assembly assembly = typeof(AppBranding).Assembly;
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(static name => name.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                return new Icon(stream);
            }
        }

        string? exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            Icon? associated = Icon.ExtractAssociatedIcon(exePath);
            if (associated is not null)
            {
                return associated;
            }
        }

        return SystemIcons.Application;
    }
}
