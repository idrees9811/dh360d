using System.IO.Ports;

namespace Dh360dFeed;

internal readonly record struct PumpMetrics(
    int CpuTempC,
    int PumpRpm,
    int FanRpm,
    int CpuLoadPercent,
    int RamLoadPercent);

internal sealed class PumpLink : IDisposable
{
    private const int Baud = 115200;
    private static readonly byte[] Handshake = [0x02, 0x01, 0x00];
    private const byte Ack = 0x21;
    private const byte Identify = 0x31;
    private const byte StatusCmd = 0x01;

    private SerialPort? _port;

    public static IEnumerable<string> CandidatePorts()
    {
        foreach (string name in SerialPort.GetPortNames().OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            yield return name;
        }
    }

    public void Connect(string portName, bool reset = false)
    {
        Disconnect();
        _port = new SerialPort(portName, Baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1500,
            WriteTimeout = 1500,
            DtrEnable = true,
            RtsEnable = false,
        };
        _port.Open();

        if (reset)
        {
            PulseReset();
        }

        DoHandshake();
    }

    public bool TryPush(PumpMetrics metrics, int attempts = 3)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            if (TryPushOnce(metrics))
            {
                return true;
            }

            Thread.Sleep(40 * attempt);
        }

        return false;
    }

    public void Push(PumpMetrics metrics)
    {
        if (!TryPush(metrics))
        {
            throw new IOException("pump did not ACK the status frame");
        }
    }

    private bool TryPushOnce(PumpMetrics metrics)
    {
        SerialPort port = _port ?? throw new InvalidOperationException("pump serial port is not open");
        byte[] frame =
        [
            StatusCmd,
            0x0A,
            ..ToU16(metrics.CpuTempC),
            ..ToU16(metrics.PumpRpm),
            ..ToU16(metrics.FanRpm),
            ..ToU16(metrics.CpuLoadPercent),
            ..ToU16(metrics.RamLoadPercent),
        ];
        port.Write(frame, 0, frame.Length);
        Thread.Sleep(20);
        return TryReadAck(port, 3500);
    }

    private static bool TryReadAck(SerialPort port, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (port.BytesToRead > 0)
            {
                int value = port.ReadByte();
                if (value == Ack)
                {
                    while (port.BytesToRead > 0)
                    {
                        port.ReadByte();
                    }

                    return true;
                }

                // Ignore stray bytes (e.g. late 0x31) and keep waiting for ACK.
            }

            Thread.Sleep(15);
        }

        return false;
    }

    public static bool Probe(string portName, bool reset = false)
    {
        using var probe = new PumpLink();
        try
        {
            probe.Connect(portName, reset);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ResolvePort(string? cliPort, AppConfig config, bool reset, bool scanAll = false)
    {
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(cliPort))
        {
            candidates.Add(cliPort);
        }

        string? envPort = Environment.GetEnvironmentVariable("DH360D_PORT");
        if (!string.IsNullOrWhiteSpace(envPort))
        {
            candidates.Add(envPort);
        }

        if (!string.IsNullOrWhiteSpace(config.Port))
        {
            candidates.Add(config.Port);
        }

        foreach (string port in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!tried.Add(port))
            {
                continue;
            }

            if (scanAll)
            {
                if (Probe(port, reset))
                {
                    return port;
                }
            }
            else
            {
                return port;
            }
        }

        foreach (string port in CandidatePorts())
        {
            if (!tried.Add(port))
            {
                continue;
            }

            if (Probe(port, reset))
            {
                return port;
            }
        }

        throw new IOException(
            "no DH360D found on any COM port - close the vendor app, verify the USB header, then try --reset");
    }

    public void Disconnect()
    {
        if (_port is null)
        {
            return;
        }

        if (_port.IsOpen)
        {
            _port.Close();
        }

        _port.Dispose();
        _port = null;
    }

    public void Dispose() => Disconnect();

    private void DoHandshake()
    {
        SerialPort port = _port ?? throw new InvalidOperationException("pump serial port is not open");
        port.DiscardInBuffer();
        port.Write(Handshake, 0, Handshake.Length);

        if (!TryReadByte(port, 3000, out int first))
        {
            throw new IOException("handshake timed out waiting for identify (0x31)");
        }

        if (first != Identify)
        {
            throw new IOException($"handshake failed (got 0x{first:X2}, expected 0x31)");
        }

        if (TryReadByte(port, 500, out int ack) && ack == Ack)
        {
            while (port.BytesToRead > 0)
            {
                port.ReadByte();
            }
        }
    }

    private static bool TryReadByte(SerialPort port, int timeoutMs, out int value)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (port.BytesToRead > 0)
            {
                value = port.ReadByte();
                return true;
            }

            Thread.Sleep(15);
        }

        value = -1;
        return false;
    }

    private void PulseReset()
    {
        SerialPort port = _port ?? throw new InvalidOperationException("pump serial port is not open");
        port.DtrEnable = false;
        port.RtsEnable = false;
        Thread.Sleep(400);
        port.DtrEnable = true;
        port.RtsEnable = true;
        Thread.Sleep(400);
        port.DtrEnable = false;
        port.RtsEnable = false;
        Thread.Sleep(400);
        port.DtrEnable = true;
        port.RtsEnable = false;
        Thread.Sleep(500);
        port.DiscardInBuffer();
    }

    private static byte[] ToU16(int value)
    {
        value = Math.Clamp(value, 0, 0xFFFF);
        return [(byte)(value >> 8), (byte)value];
    }
}