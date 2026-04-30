using Newtonsoft.Json;
using TrayWebApp.Core.Models;

namespace TrayWebApp.Core.Services;

/// <summary>
/// Reads and writes the list of registered web apps to apps.json
/// </summary>
public class WebAppStore
{
    private readonly string _filePath;
    private List<WebAppItem> _apps;

    public WebAppStore(string? basePath = null)
    {
        AppPaths.EnsureDirectories();
        var appDataDir = basePath ?? AppPaths.DataDirectory;
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "apps.json");
        _apps = Load();
        NormalizeAll();

        // Seed with a default entry if the list is empty
        if (_apps.Count == 0)
        {
            _apps.Add(new WebAppItem
            {
                Id = "google",
                Name = "Google",
                Url = "https://www.google.com",
                Order = 0
            });
            Save();
        }
    }

    public IReadOnlyList<WebAppItem> Apps => _apps.AsReadOnly();
    public string FilePath => _filePath;

    private List<WebAppItem> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<List<WebAppItem>>(json) ?? new List<WebAppItem>();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("[WebAppStore] Failed to load apps", ex);
            BackupInvalidFile(_filePath);
        }

        return new List<WebAppItem>();
    }

    public void Save()
    {
        try
        {
            NormalizeAll();
            var json = JsonConvert.SerializeObject(_apps, Formatting.Indented);
            WriteAtomic(_filePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[WebAppStore] Failed to save apps", ex);
        }
    }

    public void Add(WebAppItem item)
    {
        Normalize(item);
        if (_apps.Any(a => a.Id == item.Id))
        {
            item.Id = Guid.NewGuid().ToString("N")[..8];
        }

        item.Order = _apps.Count;
        _apps.Add(item);
        Save();
    }

    public void Remove(string id)
    {
        _apps.RemoveAll(a => a.Id == id);
        Reindex();
        Save();
    }

    public void Update(WebAppItem item)
    {
        var index = _apps.FindIndex(a => a.Id == item.Id);
        if (index >= 0)
        {
            Normalize(item);
            _apps[index] = item;
            Save();
        }
    }

    public WebAppItem? GetById(string id)
    {
        return _apps.FirstOrDefault(a => a.Id == id);
    }

    public void ReplaceAll(IEnumerable<WebAppItem> apps)
    {
        _apps = apps.ToList();
        NormalizeAll();
        Save();
    }

    public void MarkUsed(string id)
    {
        var app = GetById(id);
        if (app == null) return;
        app.LastUsedAtUtc = DateTimeOffset.UtcNow;
        Save();
    }

    public void UpdateRuntimeState(
        string id,
        string? title,
        string? url,
        double width,
        double height,
        double x,
        double y,
        double zoomFactor)
    {
        var app = GetById(id);
        if (app == null) return;

        app.LastKnownTitle = string.IsNullOrWhiteSpace(title) ? app.LastKnownTitle : title;
        app.LastVisitedUrl = string.IsNullOrWhiteSpace(url) ? app.LastVisitedUrl : url;
        app.Width = width > 0 ? (int)Math.Round(width) : app.Width;
        app.Height = height > 0 ? (int)Math.Round(height) : app.Height;
        app.WindowX = x;
        app.WindowY = y;
        app.ZoomFactor = Math.Clamp(zoomFactor, 0.25, 3.0);
        app.LastUsedAtUtc = DateTimeOffset.UtcNow;
        Save();
    }

    public void SetIconPath(string id, string iconPath)
    {
        var app = GetById(id);
        if (app == null) return;
        app.IconPath = iconPath;
        Save();
    }

    private void NormalizeAll()
    {
        foreach (var app in _apps)
        {
            Normalize(app);
        }

        _apps = _apps
            .OrderBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in _apps)
        {
            if (string.IsNullOrWhiteSpace(app.Id) || !seenIds.Add(app.Id))
            {
                app.Id = Guid.NewGuid().ToString("N")[..8];
                seenIds.Add(app.Id);
            }
        }

        Reindex();
    }

    private static void Normalize(WebAppItem app)
    {
        if (string.IsNullOrWhiteSpace(app.Id))
        {
            app.Id = Guid.NewGuid().ToString("N")[..8];
        }

        app.Name = string.IsNullOrWhiteSpace(app.Name) ? "New App" : app.Name.Trim();
        app.Url = string.IsNullOrWhiteSpace(app.Url) ? "https://www.google.com" : app.Url.Trim();

        if (!app.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !app.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            app.Url = "https://" + app.Url;
        }

        app.Width = Math.Clamp(app.Width, 0, 3840);
        app.Height = Math.Clamp(app.Height, 0, 2160);
        app.ZoomFactor = Math.Clamp(app.ZoomFactor <= 0 ? 1.0 : app.ZoomFactor, 0.25, 3.0);
        app.UserAgent = string.IsNullOrWhiteSpace(app.UserAgent) ? "desktop" : app.UserAgent.Trim();
    }

    private void Reindex()
    {
        for (var i = 0; i < _apps.Count; i++)
        {
            _apps[i].Order = i;
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static void BackupInvalidFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backupPath = $"{path}.invalid-{DateTimeOffset.Now:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, overwrite: false);
            AppLogger.Warn($"Backed up invalid apps file to {backupPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to back up invalid apps file", ex);
        }
    }
}
