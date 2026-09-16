using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace Dh360dFeed;

internal sealed class HardwareMetrics : IDisposable
{
    private const int PercentMax = 99;
    private readonly Computer _computer = new()
    {
        IsMotherboardEnabled = true,
        IsCpuEnabled = true,
        IsMemoryEnabled = true,
        IsControllerEnabled = true,
        IsGpuEnabled = false,
        IsStorageEnabled = false,
        IsNetworkEnabled = false,
        IsPsuEnabled = false,
        IsBatteryEnabled = false,
    };
    private readonly UpdateVisitor _visitor = new();

    public HardwareMetrics()
    {
        _computer.Open();
        for (int i = 0; i < 3; i++)
        {
            _computer.Accept(_visitor);
            Thread.Sleep(150);
        }
    }

    public SensorSnapshot Read()
    {
        _computer.Accept(_visitor);
        var readings = new List<Reading>();
        foreach (IHardware hardware in _computer.Hardware)
        {
            Collect(hardware, hardware.Name, readings);
        }

        return Summarize(readings);
    }

    public void PrintDiagnostics()
    {
        SensorSnapshot picked = Read();

        if (!PawnIoBootstrap.IsInstalled)
        {
            Console.WriteLine("PawnIO: not installed (required for CPU temp + fan RPM)");
        }
        else
        {
            Console.WriteLine($"PawnIO: {LibreHardwareMonitor.PawnIo.PawnIo.Version}");
        }

        _computer.Accept(_visitor);
        var readings = new List<Reading>();
        foreach (IHardware hardware in _computer.Hardware)
        {
            Collect(hardware, hardware.Name, readings);
        }

        Console.WriteLine("\nTemperature:");
        foreach (Reading row in readings.Where(static r => r.Kind == "temperature").Take(12))
        {
            Console.WriteLine($"  {row.Hardware} / {row.Name,-20} {row.Value:0}");
        }

        Console.WriteLine("\nLoad:");
        foreach (Reading row in readings.Where(static r => r.Kind == "load").Take(12))
        {
            Console.WriteLine($"  {row.Hardware} / {row.Name,-20} {row.Value:0.#}");
        }

        Console.WriteLine("\nFans:");
        foreach (Reading row in readings.Where(static r => r.Kind == "fan").Take(12))
        {
            Console.WriteLine($"  {row.Hardware} / {row.Name,-20} {row.Value:0}");
        }

        Console.WriteLine("\nSelected for DH360D:");
        Console.WriteLine($"  CPU temp: {picked.CpuTempC} C");
        Console.WriteLine($"  CPU load: {picked.CpuLoadPercent} %");
        Console.WriteLine($"  RAM load: {picked.RamLoadPercent} %");
        Console.WriteLine($"  Pump RPM: {picked.PumpRpm}");
        Console.WriteLine($"  Fan RPM:  {picked.RadiatorFanRpm}");
    }

    public void Dispose() => _computer.Close();

    private static void Collect(IHardware hardware, string hardwareName, List<Reading> sink)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.Value is null or <= 0)
            {
                continue;
            }

            sink.Add(new Reading(sensor.Identifier.ToString(), sensor.Name, hardwareName, KindOf(sensor.SensorType), sensor.Value.Value));
        }

        foreach (IHardware sub in hardware.SubHardware)
        {
            Collect(sub, sub.Name, sink);
        }
    }

    private static SensorSnapshot Summarize(IReadOnlyList<Reading> readings)
    {
        static bool IsCpu(Reading reading) =>
            Regex.IsMatch(reading.Hardware, @"ryzen|core i|intel|amd (?!radeon)", RegexOptions.IgnoreCase);

        float? Pick(Func<Reading, bool> predicate)
        {
            var values = readings.Where(r => predicate(r)).Select(static r => r.Value).ToList();
            return values.Count > 0 ? values.Max() : null;
        }

        int cpuTemp = (int)Math.Round(
            Pick(r => IsCpu(r) && r.Kind == "temperature" && Regex.IsMatch(r.Name, @"tctl|tdie|package|average", RegexOptions.IgnoreCase))
            ?? Pick(r => IsCpu(r) && r.Kind == "temperature")
            ?? 0);

        int cpuLoad = ClampPercent(
            Pick(r => IsCpu(r) && r.Kind == "load" && r.Name.Contains("total", StringComparison.OrdinalIgnoreCase))
            ?? Pick(r => IsCpu(r) && r.Kind == "load")
            ?? 0);

        int ramLoad = ClampPercent(
            Pick(r => r.Id.StartsWith("/ram/", StringComparison.Ordinal) && r.Kind == "load")
            ?? Pick(r => r.Kind == "load" && r.Hardware.Contains("memory", StringComparison.OrdinalIgnoreCase) && !r.Hardware.Contains("virtual", StringComparison.OrdinalIgnoreCase))
            ?? 0);

        int pumpRpm = (int)Math.Round(
            Pick(r => r.Kind == "fan" && Regex.IsMatch(r.Name, @"pump", RegexOptions.IgnoreCase))
            ?? 0);

        int radiatorFanRpm = (int)Math.Round(
            Pick(r => r.Kind == "fan" && !Regex.IsMatch(r.Name, @"pump", RegexOptions.IgnoreCase))
            ?? 0);

        return new SensorSnapshot(cpuTemp, cpuLoad, ramLoad, pumpRpm, radiatorFanRpm);
    }

    private static int ClampPercent(float value) => Math.Clamp((int)Math.Round(value), 0, PercentMax);

    private static string KindOf(SensorType type) =>
        type switch
        {
            SensorType.Temperature => "temperature",
            SensorType.Load => "load",
            SensorType.Fan => "fan",
            _ => type.ToString().ToLowerInvariant(),
        };

    private readonly record struct Reading(string Id, string Name, string Hardware, string Kind, float Value);

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware sub in hardware.SubHardware)
            {
                sub.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}