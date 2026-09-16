namespace Dh360dFeed;

internal static class AppLog
{
    private const int MaxMemoryLines = 300;
    private const long MaxLogFileBytes = 1 * 1024 * 1024;
    private const int MaxBackupFiles = 2;

    private static readonly Queue<string> Recent = new();
    private static readonly object Gate = new();

    public static string LogFilePath => AppPaths.LogFile;

    public static event Action<string>? LineAdded;

    public static void Info(string message) => Add("INFO", message);
    public static void Warn(string message) => Add("WARN", message);
    public static void Error(string message) => Add("ERROR", message);

    public static IReadOnlyList<string> Snapshot()
    {
        lock (Gate)
        {
            return Recent.ToArray();
        }
    }

    private static void Add(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}";
        lock (Gate)
        {
            Recent.Enqueue(line);
            while (Recent.Count > MaxMemoryLines)
            {
                Recent.Dequeue();
            }

            WriteToFile(line);
        }

        LineAdded?.Invoke(line);
    }

    private static void WriteToFile(string line)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            RotateIfNeeded();

            using var stream = new FileStream(AppPaths.LogFile, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(line);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.LogFile))
        {
            return;
        }

        var info = new FileInfo(AppPaths.LogFile);
        if (info.Length < MaxLogFileBytes)
        {
            return;
        }

        for (int i = MaxBackupFiles; i >= 1; i--)
        {
            string older = AppPaths.LogFile + "." + i;
            string newer = i == 1 ? AppPaths.LogFile : AppPaths.LogFile + "." + (i - 1);
            if (File.Exists(newer))
            {
                if (File.Exists(older))
                {
                    File.Delete(older);
                }

                File.Move(newer, older);
            }
        }
    }
}
