using Newtonsoft.Json;
using TrayWebApp.Core.Models;

namespace TrayWebApp.Core.Services;

/// <summary>
/// Reads and writes application settings to settings.json
/// </summary>
public class SettingsStore
{
    private readonly string _filePath;
    private AppSettings _settings;

    public SettingsStore(string? basePath = null)
    {
        AppPaths.EnsureDirectories();
        var appDataDir = basePath ?? AppPaths.DataDirectory;
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "settings.json");
        _settings = Load();
    }

    public AppSettings Settings => _settings;
    public string FilePath => _filePath;

    /// <summary>Load settings from disk, returning defaults if file doesn't exist</summary>
    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                Normalize(settings);
                return settings;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("[SettingsStore] Failed to load settings", ex);
            BackupInvalidFile(_filePath);
        }

        var defaults = new AppSettings();
        Normalize(defaults);
        return defaults;
    }

    /// <summary>Persist current settings to disk</summary>
    public void Save()
    {
        try
        {
            Normalize(_settings);
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            WriteAtomic(_filePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[SettingsStore] Failed to save settings", ex);
        }
    }

    /// <summary>Replace settings in-memory and persist</summary>
    public void Update(Action<AppSettings> modifier)
    {
        modifier(_settings);
        Save();
    }

    private static void Normalize(AppSettings settings)
    {
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 240, 3840);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight, 240, 2160);
        settings.WindowOpacity = Math.Clamp(settings.WindowOpacity, 0.2, 1.0);

        if (string.IsNullOrWhiteSpace(settings.DefaultUrl))
        {
            settings.DefaultUrl = "https://www.google.com";
        }

        if (!settings.DefaultUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !settings.DefaultUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultUrl = "https://" + settings.DefaultUrl;
        }

        if (!string.Equals(settings.ThemeMode, "Light", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(settings.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            settings.ThemeMode = "Dark";
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
            AppLogger.Warn($"Backed up invalid settings file to {backupPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to back up invalid settings file", ex);
        }
    }
}
