namespace Dh360dFeed;

internal sealed class FeedWorker : IDisposable
{
    public event Action<string>? StatusChanged;
    public event Action<string>? TransientError;
    public event Action<PumpMetrics>? FrameSent;

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private AppConfig _config;
    private int _zeroSensorSamples;
    private bool _sensorWarningRaised;

    public FeedWorker(AppConfig config)
    {
        _config = config;
    }

    public bool IsRunning => _task is { IsCompleted: false };

    public void UpdateConfig(AppConfig config)
    {
        lock (_gate)
        {
            _config = config;
        }
    }

    public void Start(bool reset = false)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _zeroSensorSamples = 0;
        _sensorWarningRaised = false;
        _task = Task.Run(() => RunLoop(_cts.Token, reset));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _task?.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // best effort shutdown
        }

        _cts?.Dispose();
        _cts = null;
        _task = null;
    }

    private void RunLoop(CancellationToken cancel, bool reset)
    {
        AppConfig config = SnapshotConfig();
        double interval = Math.Max(0.5, config.Display.IntervalSeconds);
        bool portVerified = false;

        while (!cancel.IsCancellationRequested)
        {
            PumpLink? pump = null;
            try
            {
                config = SnapshotConfig();
                interval = Math.Max(0.5, config.Display.IntervalSeconds);

                string port = PumpLink.ResolvePort(null, config, reset, scanAll: !portVerified);
                Thread.Sleep(350);
                pump = new PumpLink();
                pump.Connect(port, reset: reset || !portVerified);
                portVerified = true;

                config.Port = port;
                config.LastOk = DateTime.UtcNow.ToString("O");
                config.Save();
                RaiseStatus($"Connected on {port}");
                AppLog.Info($"Connected on {port}");

                using var metrics = new HardwareMetrics();
                int consecutiveAckFailures = 0;
                while (!cancel.IsCancellationRequested)
                {
                    config = SnapshotConfig();
                    SensorSnapshot snapshot = metrics.Read();
                    PumpMetrics frame = MetricMapper.Map(snapshot, config.Display);
                    if (pump.TryPush(frame))
                    {
                        consecutiveAckFailures = 0;
                        FrameSent?.Invoke(frame);
                    }
                    else
                    {
                        consecutiveAckFailures++;
                        AppLog.Warn($"pump ACK miss ({consecutiveAckFailures}/8) — keeping connection");
                        if (consecutiveAckFailures >= 8)
                        {
                            throw new IOException("pump stopped ACKing status frames");
                        }

                        cancel.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(250));
                        continue;
                    }

                    if (!MetricMapper.HasLiveSensors(snapshot))
                    {
                        _zeroSensorSamples++;
                        if (!_sensorWarningRaised && _zeroSensorSamples >= 5)
                        {
                            _sensorWarningRaised = true;
                            RaiseTransient(
                                "Temperature/RPM sensors unavailable. Run as Administrator and install PawnIO from the tray menu.");
                        }
                    }
                    else
                    {
                        _zeroSensorSamples = 0;
                    }

                    cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(interval));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception error)
            {
                portVerified = false;
                AppLog.Error($"{error.Message} — reconnecting in 5s");
                RaiseTransient($"{error.Message} — retrying in 5s");
                try
                {
                    Task.Delay(TimeSpan.FromSeconds(5), cancel).Wait(cancel);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                pump?.Dispose();
            }
        }

        RaiseStatus("Stopped");
    }

    private AppConfig SnapshotConfig()
    {
        lock (_gate)
        {
            return _config;
        }
    }

    private void RaiseStatus(string message) => StatusChanged?.Invoke(message);
    private void RaiseTransient(string message) => TransientError?.Invoke(message);

    public void Dispose() => Stop();
}