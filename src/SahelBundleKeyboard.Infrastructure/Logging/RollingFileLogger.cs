using System.IO;
using SahelBundleKeyboard.Core.Logging;

namespace SahelBundleKeyboard.Infrastructure.Logging;

/// <summary>
/// Small bounded rolling file logger under Data/logs.
/// - one file per day (app-yyyyMMdd.log),
/// - rotates to .1 when a file exceeds the size cap,
/// - keeps at most MaxFiles files and deletes anything older than RetentionDays.
/// Never logs typed product text or run history — callers pass counts/ids only.
/// </summary>
public sealed class RollingFileLogger : ILog
{
    private const int MaxFileSizeBytes = 512 * 1024;
    private const int MaxFilesPerDay = 3;
    public const int RetentionDays = 7;

    private readonly string _folder;
    private readonly object _lock = new();

    public RollingFileLogger(string folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        Directory.CreateDirectory(_folder);
        EnforceRetention();
    }

    public void Info(string source, string message) => Write("INFO", source, message, null);

    public void Warn(string source, string message) => Write("WARN", source, message, null);

    public void Error(string source, string message, Exception? exception = null) =>
        Write("ERROR", source, message, exception);

    private void Write(string level, string source, string message, Exception? exception)
    {
        try
        {
            lock (_lock)
            {
                var path = CurrentFilePath();
                if (File.Exists(path) && new FileInfo(path).Length >= MaxFileSizeBytes)
                {
                    Rotate(path);
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{source}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception.GetType().FullName + ": " + exception.Message +
                            Environment.NewLine + exception.StackTrace;
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Logging must never crash the app.
        }
    }

    private string CurrentFilePath()
    {
        return Path.Combine(_folder, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    private void Rotate(string path)
    {
        for (var i = MaxFilesPerDay; i >= 2; i--)
        {
            var from = path + "." + (i - 1);
            var to = path + "." + i;
            if (File.Exists(from))
            {
                File.Move(from, to, overwrite: true);
            }
        }

        File.Move(path, path + ".1", overwrite: true);
    }

    private void EnforceRetention()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(_folder, "app-*.log*"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
