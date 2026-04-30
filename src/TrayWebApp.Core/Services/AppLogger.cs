namespace TrayWebApp.Core.Services;

/// <summary>
/// Minimal file logger for diagnostics without introducing a logging framework.
/// </summary>
public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var detail = exception == null ? message : $"{message}: {exception}";
        Write("ERROR", detail);
    }

    private static void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                File.AppendAllText(AppPaths.LogFilePath, line);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
