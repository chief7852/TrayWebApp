namespace TrayWebApp.Core.Services;

/// <summary>
/// Centralizes file-system locations used by the app.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "TrayWebApp";

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public static string AppsFilePath => Path.Combine(DataDirectory, "apps.json");

    public static string WebViewDataDirectory => Path.Combine(DataDirectory, "WebView2Data");

    public static string FaviconsDirectory => Path.Combine(DataDirectory, "Favicons");

    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    public static string LogFilePath => Path.Combine(LogsDirectory, "app.log");

    public static string DefaultDownloadsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(WebViewDataDirectory);
        Directory.CreateDirectory(FaviconsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
