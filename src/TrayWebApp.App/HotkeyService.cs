using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrayWebApp.App;

/// <summary>
/// Manages system-wide global hotkeys using Win32 RegisterHotKey/UnregisterHotKey.
/// </summary>
public class HotkeyService : IDisposable
{
    private readonly Window _owner;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private HwndSource? _hwndSource;
    private int _nextId = 9000;
    private bool _disposed;

    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier constants
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Virtual key codes
    public const uint VK_SPACE = 0x20;
    public const uint VK_0 = 0x30;
    public const uint VK_1 = 0x31;
    public const uint VK_2 = 0x32;
    public const uint VK_3 = 0x33;
    public const uint VK_4 = 0x34;
    public const uint VK_5 = 0x35;
    public const uint VK_6 = 0x36;
    public const uint VK_7 = 0x37;
    public const uint VK_8 = 0x38;
    public const uint VK_9 = 0x39;
    public const uint VK_K = 0x4B;

    private const int WM_HOTKEY = 0x0312;

    public HotkeyService(Window owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Initialize the hotkey listener. Must be called after the owner window is loaded.
    /// </summary>
    public void Initialize()
    {
        // Create a hidden message-only window for receiving hotkey messages
        var helper = new WindowInteropHelper(_owner);

        // Ensure the window handle exists
        if (helper.Handle == IntPtr.Zero)
        {
            helper.EnsureHandle();
        }

        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// Register a global hotkey with the given modifiers and virtual key code.
    /// Returns the hotkey ID, or -1 on failure.
    /// </summary>
    public int Register(uint modifiers, uint virtualKey, Action callback)
    {
        if (_hwndSource == null) return -1;

        var id = _nextId++;
        // Add MOD_NOREPEAT to prevent repeated firing while held
        if (RegisterHotKey(_hwndSource.Handle, id, modifiers | MOD_NOREPEAT, virtualKey))
        {
            _hotkeyActions[id] = callback;
            System.Diagnostics.Debug.WriteLine($"[HotkeyService] Registered hotkey {id}: mod=0x{modifiers:X} vk=0x{virtualKey:X}");
            return id;
        }

        System.Diagnostics.Debug.WriteLine($"[HotkeyService] Failed to register hotkey: mod=0x{modifiers:X} vk=0x{virtualKey:X}");
        return -1;
    }

    /// <summary>Unregister a specific hotkey by ID</summary>
    public void Unregister(int id)
    {
        if (_hwndSource == null) return;

        UnregisterHotKey(_hwndSource.Handle, id);
        _hotkeyActions.Remove(id);
    }

    /// <summary>Unregister all hotkeys</summary>
    public void UnregisterAll()
    {
        if (_hwndSource == null) return;

        foreach (var id in _hotkeyActions.Keys.ToList())
        {
            UnregisterHotKey(_hwndSource.Handle, id);
        }
        _hotkeyActions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}
