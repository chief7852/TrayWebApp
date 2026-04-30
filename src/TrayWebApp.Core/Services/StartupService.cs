using Microsoft.Win32;

namespace TrayWebApp.Core.Services;

/// <summary>
/// Manages Windows startup program registration via the registry.
/// </summary>
public class StartupService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TrayWebApp";

    /// <summary>Check if the app is currently registered to run at startup</summary>
    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Register the app to run at Windows startup</summary>
    public static bool Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            key?.SetValue(AppName, $"\"{exePath}\"");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to register: {ex.Message}");
            return false;
        }
    }

    /// <summary>Remove the app from Windows startup</summary>
    public static bool Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key?.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName);
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupService] Failed to unregister: {ex.Message}");
            return false;
        }
    }

    /// <summary>Toggle startup registration</summary>
    public static bool Toggle()
    {
        if (IsRegistered())
        {
            return Unregister();
        }
        else
        {
            return Register();
        }
    }
}
