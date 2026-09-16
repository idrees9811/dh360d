namespace Dh360dFeed;

internal static class MetricMapper
{
    private const int DisplayMax = 99;
    private const int PercentMax = 99;

    public static PumpMetrics Map(SensorSnapshot snapshot, DisplaySettings settings)
    {
        int pumpWire = PickRpm(snapshot, settings.RpmSource);
        return new PumpMetrics(
            ClampDisplay(snapshot.CpuTempC),
            pumpWire,
            snapshot.RadiatorFanRpm,
            ClampPercent(snapshot.CpuLoadPercent),
            ClampPercent(snapshot.RamLoadPercent));
    }

    public static bool HasLiveSensors(SensorSnapshot snapshot) =>
        snapshot.CpuTempC > 0 || PickRpm(snapshot, RpmSource.Auto) > 0;

    private static int PickRpm(SensorSnapshot snapshot, RpmSource source) =>
        source switch
        {
            RpmSource.Pump => snapshot.PumpRpm,
            RpmSource.RadiatorFan => snapshot.RadiatorFanRpm,
            _ => snapshot.PumpRpm > 0 ? snapshot.PumpRpm : snapshot.RadiatorFanRpm,
        };

    private static int ClampDisplay(int value) => Math.Clamp(value, 0, DisplayMax);
    private static int ClampPercent(int value) => Math.Clamp(value, 0, PercentMax);
}