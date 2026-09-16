namespace Dh360dFeed;

internal readonly record struct SensorSnapshot(
    int CpuTempC,
    int CpuLoadPercent,
    int RamLoadPercent,
    int PumpRpm,
    int RadiatorFanRpm);