namespace Dh360dFeed;

internal enum RpmSource
{
    Auto,
    RadiatorFan,
    Pump,
}

internal sealed class DisplaySettings
{
    public RpmSource RpmSource { get; set; } = RpmSource.RadiatorFan;
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public double IntervalSeconds { get; set; } = 1.0;
}