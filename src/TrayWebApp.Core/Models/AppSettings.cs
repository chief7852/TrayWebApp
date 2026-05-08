namespace TrayWebApp.Core.Models;

/// <summary>
/// Global application settings stored in settings.json
/// </summary>
public class AppSettings
{
    /// <summary>Default URL to load when no web app is selected</summary>
    public string DefaultUrl { get; set; } = "https://www.google.com";

    /// <summary>Window width in pixels</summary>
    public int WindowWidth { get; set; } = 430;

    /// <summary>Window height in pixels</summary>
    public int WindowHeight { get; set; } = 720;

    /// <summary>Window X position (-1 means auto-position near tray)</summary>
    public double WindowX { get; set; } = -1;

    /// <summary>Window Y position (-1 means auto-position near tray)</summary>
    public double WindowY { get; set; } = -1;

    /// <summary>Keep the WebView window always on top of other windows</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>Start the app automatically when Windows starts</summary>
    public bool RunAtStartup { get; set; } = false;

    /// <summary>Hide the window instead of closing the app when the user clicks close</summary>
    public bool HideOnClose { get; set; } = true;

    /// <summary>Hide the window when it loses focus</summary>
    public bool HideOnDeactivate { get; set; } = false;

    /// <summary>Window opacity (0.0 - 1.0)</summary>
    public double WindowOpacity { get; set; } = 1.0;

    /// <summary>Last used web app ID</summary>
    public string? LastAppId { get; set; }

    /// <summary>Custom download folder path (empty = system Downloads)</summary>
    public string? DownloadFolder { get; set; }

    /// <summary>Open popups/new windows in the system default browser</summary>
    public bool OpenNewWindowsExternally { get; set; } = false;

    /// <summary>Allow notification permission requests without prompting</summary>
    public bool AutoAllowNotifications { get; set; } = true;

    /// <summary>Show the URL entry field in the floating browser window</summary>
    public bool ShowAddressBar { get; set; } = true;

    /// <summary>Application chrome theme: Dark or Light</summary>
    public string ThemeMode { get; set; } = "Dark";
}
