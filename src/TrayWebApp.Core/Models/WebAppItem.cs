namespace TrayWebApp.Core.Models;

/// <summary>
/// Represents a registered web application entry stored in apps.json
/// </summary>
public class WebAppItem
{
    /// <summary>Unique identifier for this web app</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display name of the web app</summary>
    public string Name { get; set; } = "New App";

    /// <summary>URL to load in the WebView</summary>
    public string Url { get; set; } = "https://www.google.com";

    /// <summary>Custom window width for this app (0 = use default)</summary>
    public int Width { get; set; } = 0;

    /// <summary>Custom window height for this app (0 = use default)</summary>
    public int Height { get; set; } = 0;

    /// <summary>Override always-on-top per app</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>User-Agent override: "desktop", "mobile", or custom string</summary>
    public string UserAgent { get; set; } = "desktop";

    /// <summary>Local favicon file path, if one has been cached</summary>
    public string? IconPath { get; set; }

    /// <summary>Last document title reported by the WebView</summary>
    public string? LastKnownTitle { get; set; }

    /// <summary>Last URL visited while this app was active</summary>
    public string? LastVisitedUrl { get; set; }

    /// <summary>Last time this app was opened, in UTC</summary>
    public DateTimeOffset? LastUsedAtUtc { get; set; }

    /// <summary>Last window X position for this app (-1 = use global/default)</summary>
    public double WindowX { get; set; } = -1;

    /// <summary>Last window Y position for this app (-1 = use global/default)</summary>
    public double WindowY { get; set; } = -1;

    /// <summary>Last zoom factor for this app</summary>
    public double ZoomFactor { get; set; } = 1.0;

    /// <summary>Sort order index</summary>
    public int Order { get; set; } = 0;
}
