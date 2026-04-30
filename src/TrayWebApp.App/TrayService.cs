using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using TrayWebApp.Core.Models;
using TrayWebApp.Core.Services;
using Application = System.Windows.Application;

namespace TrayWebApp.App;

/// <summary>
/// Manages the system tray icon, context menu, and WebView window lifecycle.
/// </summary>
public class TrayService : IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly WebAppStore _webAppStore;
    private NotifyIcon? _notifyIcon;
    private WebViewWindow? _webViewWindow;
    private ManageAppsWindow? _manageWindow;
    private SettingsWindow? _settingsWindow;
    private HiddenWindow? _hiddenWindow;
    private HotkeyService? _hotkeyService;
    private readonly List<int> _appHotkeyIds = new();
    private string? _activeAppId;
    private bool _isExiting;
    private bool _disposed;

    // Common User-Agent strings
    private const string MobileUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";
    private const string DesktopUserAgent = "";

    public TrayService(SettingsStore settingsStore, WebAppStore webAppStore)
    {
        _settingsStore = settingsStore;
        _webAppStore = webAppStore;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "TrayWebApp - 클릭해서 열기",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.MouseClick += OnTrayClick;

        // Initialize global hotkeys via a hidden helper window
        InitializeHotkeys();
    }

    private void InitializeHotkeys()
    {
        _hiddenWindow = new HiddenWindow();
        _hiddenWindow.Show();
        _hiddenWindow.Hide();

        _hotkeyService = new HotkeyService(_hiddenWindow);
        _hotkeyService.Initialize();

        // Ctrl+Alt+Space: Toggle main window
        _hotkeyService.Register(
            HotkeyService.MOD_CONTROL | HotkeyService.MOD_ALT,
            HotkeyService.VK_SPACE,
            () => ToggleWindow());

        // Ctrl+Alt+1~9: Quick-launch apps by position
        RegisterAppHotkeys();
    }

    private void RegisterAppHotkeys()
    {
        if (_hotkeyService == null) return;

        foreach (var id in _appHotkeyIds)
        {
            _hotkeyService.Unregister(id);
        }
        _appHotkeyIds.Clear();

        var vkKeys = new[] {
            HotkeyService.VK_1, HotkeyService.VK_2, HotkeyService.VK_3,
            HotkeyService.VK_4, HotkeyService.VK_5, HotkeyService.VK_6,
            HotkeyService.VK_7, HotkeyService.VK_8, HotkeyService.VK_9
        };

        for (int i = 0; i < Math.Min(vkKeys.Length, _webAppStore.Apps.Count); i++)
        {
            var appIndex = i;
            var hotkeyId = _hotkeyService.Register(
                HotkeyService.MOD_CONTROL | HotkeyService.MOD_ALT,
                vkKeys[i],
                () =>
                {
                    if (appIndex < _webAppStore.Apps.Count)
                    {
                        var app = _webAppStore.Apps[appIndex];
                        OpenApp(app);
                    }
                });

            if (hotkeyId >= 0)
            {
                _appHotkeyIds.Add(hotkeyId);
            }
        }
    }

    private Icon LoadAppIcon()
    {
        try
        {
            // Try to load from embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("icon.ico"));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                    return new Icon(stream);
            }

            // Try to load from file next to exe
            var exeDir = AppContext.BaseDirectory;
            var iconPath = Path.Combine(exeDir, "icon.ico");
            if (File.Exists(iconPath))
                return new Icon(iconPath);

            // Try the assets folder relative to project
            var assetsPath = Path.Combine(exeDir, "..", "..", "..", "..", "..", "assets", "icon.ico");
            if (File.Exists(assetsPath))
                return new Icon(assetsPath);
        }
        catch
        {
            // Fall through to default
        }

        return SystemIcons.Application;
    }

    private static System.Drawing.Image? LoadMenuImage(string? iconPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                return null;
            }

            using var source = System.Drawing.Image.FromFile(iconPath);
            return new Bitmap(source, new System.Drawing.Size(16, 16));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Rebuild the context menu (called after apps list changes)</summary>
    public void RebuildMenu()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        RegisterAppHotkeys();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.BackColor = Color.FromArgb(30, 30, 46);
        menu.ForeColor = Color.White;
        menu.Renderer = new DarkMenuRenderer();
        menu.ShowImageMargin = true;

        // Header
        var header = new ToolStripLabel("TrayWebApp")
        {
            ForeColor = Color.FromArgb(0, 210, 255),
            Font = new Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
            Padding = new Padding(4, 6, 4, 4)
        };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var openToggle = new ToolStripMenuItem(_webViewWindow?.IsVisible == true ? "창 숨기기" : "창 열기")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        openToggle.Click += (s, e) => ToggleWindow();
        menu.Items.Add(openToggle);

        var reloadCurrent = new ToolStripMenuItem("현재 페이지 새로고침")
        {
            Enabled = _webViewWindow?.IsLoaded == true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        reloadCurrent.Click += (s, e) => _webViewWindow?.WebView.CoreWebView2?.Reload();
        menu.Items.Add(reloadCurrent);

        var openExternal = new ToolStripMenuItem("기본 브라우저로 열기")
        {
            Enabled = _webViewWindow?.IsLoaded == true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        openExternal.Click += (s, e) => _webViewWindow?.OpenCurrentInExternalBrowser();
        menu.Items.Add(openExternal);

        menu.Items.Add(new ToolStripSeparator());

        // Web App entries
        var lastAppId = _settingsStore.Settings.LastAppId;
        for (int i = 0; i < _webAppStore.Apps.Count; i++)
        {
            var app = _webAppStore.Apps[i];
            var isActive = app.Id == lastAppId;
            var prefix = isActive ? "> " : "  ";
            var shortcutHint = i < 9 ? $"  [Ctrl+Alt+{i + 1}]" : "";
            var item = new ToolStripMenuItem($"{prefix}{app.Name}{shortcutHint}")
            {
                Tag = app,
                Image = LoadMenuImage(app.IconPath),
                ForeColor = isActive ? Color.FromArgb(0, 210, 255) : Color.White,
                Font = new Font("Segoe UI", 9.5f, isActive ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular)
            };
            item.Click += OnWebAppSelected;
            menu.Items.Add(item);
        }

        if (_webAppStore.Apps.Count == 0)
        {
            var empty = new ToolStripLabel("   등록된 앱 없음")
            {
                ForeColor = Color.FromArgb(112, 112, 136),
                Font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Italic)
            };
            menu.Items.Add(empty);
        }

        menu.Items.Add(new ToolStripSeparator());

        var recentApps = _webAppStore.Apps
            .Where(a => a.LastUsedAtUtc.HasValue)
            .OrderByDescending(a => a.LastUsedAtUtc)
            .Take(5)
            .ToList();

        if (recentApps.Count > 0)
        {
            var recentMenu = new ToolStripMenuItem("최근 사용")
            {
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            recentMenu.DropDown.BackColor = Color.FromArgb(30, 30, 46);
            recentMenu.DropDown.Renderer = new DarkMenuRenderer();

            foreach (var app in recentApps)
            {
                var item = new ToolStripMenuItem(app.Name)
                {
                    Tag = app,
                    Image = LoadMenuImage(app.IconPath),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9.5f)
                };
                item.Click += OnWebAppSelected;
                recentMenu.DropDownItems.Add(item);
            }

            menu.Items.Add(recentMenu);
            menu.Items.Add(new ToolStripSeparator());
        }

        // Window Size Presets
        var presetsMenu = new ToolStripMenuItem("창 크기")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        presetsMenu.DropDown.BackColor = Color.FromArgb(30, 30, 46);
        presetsMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var preset in WindowPreset.Defaults)
        {
            var pItem = new ToolStripMenuItem(preset.ToString())
            {
                Tag = preset,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            pItem.Click += OnPresetSelected;
            presetsMenu.DropDownItems.Add(pItem);
        }
        menu.Items.Add(presetsMenu);

        var opacityMenu = new ToolStripMenuItem("투명도")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        opacityMenu.DropDown.BackColor = Color.FromArgb(30, 30, 46);
        opacityMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var option in new[] { 1.0, 0.85, 0.7, 0.55, 0.4, 0.3 })
        {
            var percent = (int)(option * 100);
            var item = new ToolStripMenuItem($"{percent}%")
            {
                Checked = Math.Abs(_settingsStore.Settings.WindowOpacity - option) < 0.01,
                Tag = option,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            item.Click += OnOpacitySelected;
            opacityMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(opacityMenu);

        var privacyMode = new ToolStripMenuItem("프라이버시 모드")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        privacyMode.Click += (s, e) =>
        {
            _settingsStore.Update(settings =>
            {
                settings.WindowOpacity = 0.45;
                settings.AlwaysOnTop = true;
                settings.HideOnDeactivate = true;
            });

            if (_webViewWindow != null)
            {
                _webViewWindow.SetVisualOpacity(0.45);
                SetAlwaysOnTop(true);
            }

            RebuildMenu();
        };
        menu.Items.Add(privacyMode);

        // Always On Top toggle
        var alwaysOnTop = new ToolStripMenuItem("항상 위")
        {
            Checked = _settingsStore.Settings.AlwaysOnTop,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        alwaysOnTop.Click += (s, e) =>
        {
            _settingsStore.Update(settings =>
            {
                settings.AlwaysOnTop = !settings.AlwaysOnTop;
            });
            alwaysOnTop.Checked = _settingsStore.Settings.AlwaysOnTop;
            SetAlwaysOnTop(_settingsStore.Settings.AlwaysOnTop);
        };
        menu.Items.Add(alwaysOnTop);

        // Hide on Deactivate
        var hideOnBlur = new ToolStripMenuItem("바깥 클릭 시 숨김")
        {
            Checked = _settingsStore.Settings.HideOnDeactivate,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        hideOnBlur.Click += (s, e) =>
        {
            _settingsStore.Update(s2 => s2.HideOnDeactivate = !s2.HideOnDeactivate);
            hideOnBlur.Checked = _settingsStore.Settings.HideOnDeactivate;
        };
        menu.Items.Add(hideOnBlur);

        menu.Items.Add(new ToolStripSeparator());

        // Manage Apps
        var manage = new ToolStripMenuItem("앱 관리...")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        manage.Click += OnManageAppsClick;
        menu.Items.Add(manage);

        // Settings
        var settings = new ToolStripMenuItem("설정...")
        {
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        settings.Click += OnSettingsClick;
        menu.Items.Add(settings);

        // Run at Startup
        var startup = new ToolStripMenuItem("Windows 시작 시 실행")
        {
            Checked = StartupService.IsRegistered(),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        startup.Click += (s, e) =>
        {
            StartupService.Toggle();
            startup.Checked = StartupService.IsRegistered();
            _settingsStore.Update(s2 => s2.RunAtStartup = startup.Checked);
        };
        menu.Items.Add(startup);

        menu.Items.Add(new ToolStripSeparator());

        // Exit
        var exit = new ToolStripMenuItem("종료")
        {
            ForeColor = Color.FromArgb(255, 100, 100),
            Font = new Font("Segoe UI", 9.5f)
        };
        exit.Click += (s, e) =>
        {
            _isExiting = true;
            SaveWindowState();
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        return menu;
    }

    #region Event Handlers

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleWindow();
        }
    }

    private void OnWebAppSelected(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is WebAppItem app)
        {
            OpenApp(app);
            RebuildMenu(); // Update the active indicator
        }
    }

    private void OnPresetSelected(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is WindowPreset preset)
        {
            EnsureWindowCreated();
            _webViewWindow!.Width = preset.Width;
            _webViewWindow.Height = preset.Height;
            _settingsStore.Update(s =>
            {
                s.WindowWidth = preset.Width;
                s.WindowHeight = preset.Height;
            });
            ShowWindow();
        }
    }

    private void OnOpacitySelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is not double opacity)
        {
            return;
        }

        _settingsStore.Update(s => s.WindowOpacity = opacity);

        if (_webViewWindow != null)
        {
            _webViewWindow.SetVisualOpacity(opacity);
        }

        RebuildMenu();
    }

    private void OnManageAppsClick(object? sender, EventArgs e)
    {
        if (_manageWindow != null && _manageWindow.IsLoaded)
        {
            _manageWindow.Activate();
            return;
        }

        _manageWindow = new ManageAppsWindow(_webAppStore);
        _manageWindow.AppsChanged += () =>
        {
            RebuildMenu();
        };
        _manageWindow.Closed += (s2, e2) => _manageWindow = null;
        _manageWindow.Show();
    }

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        if (_settingsWindow != null && _settingsWindow.IsLoaded)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsStore);
        _settingsWindow.Closed += (s2, e2) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async void RefreshFaviconIfNeeded(WebAppItem app)
    {
        if (!string.IsNullOrWhiteSpace(app.IconPath) && File.Exists(app.IconPath))
        {
            return;
        }

        var iconPath = await FaviconService.RefreshAsync(app);
        if (!string.IsNullOrEmpty(iconPath))
        {
            _webAppStore.SetIconPath(app.Id, iconPath);
            RebuildMenu();
        }
    }

    #endregion

    #region Window Management

    private void OpenApp(WebAppItem app)
    {
        _activeAppId = app.Id;
        _settingsStore.Update(s => s.LastAppId = app.Id);
        _webAppStore.MarkUsed(app.Id);
        EnsureWindowCreated();
        NavigateToApp(app);
        ShowWindow();
        RefreshFaviconIfNeeded(app);
    }

    private void ToggleWindow()
    {
        if (_webViewWindow == null || !_webViewWindow.IsLoaded)
        {
            ShowWindow();
        }
        else if (_webViewWindow.IsVisible)
        {
            _webViewWindow.Hide();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        EnsureWindowCreated();
        _webViewWindow!.OpenNewWindowsExternally = _settingsStore.Settings.OpenNewWindowsExternally;
        _webViewWindow.SetAddressBarVisible(_settingsStore.Settings.ShowAddressBar);
        _webViewWindow.SetVisualOpacity(_settingsStore.Settings.WindowOpacity);
        _webViewWindow!.Show();
        PositionWindowNearTray();
        _webViewWindow.Activate();
        _webViewWindow.BringToTopIfPinned();
    }

    private void EnsureWindowCreated()
    {
        if (_webViewWindow != null && _webViewWindow.IsLoaded) return;

        var settings = _settingsStore.Settings;
        _webViewWindow = new WebViewWindow();
        _webViewWindow.Width = settings.WindowWidth;
        _webViewWindow.Height = settings.WindowHeight;
        _webViewWindow.SetAlwaysOnTop(settings.AlwaysOnTop);
        _webViewWindow.SetVisualOpacity(settings.WindowOpacity);
        _webViewWindow.OpenNewWindowsExternally = settings.OpenNewWindowsExternally;
        _webViewWindow.SetAddressBarVisible(settings.ShowAddressBar);
        _webViewWindow.BrowserStateChanged += OnBrowserStateChanged;
        _webViewWindow.AlwaysOnTopChanged += OnAlwaysOnTopChanged;

        // Load last used app or first app or default URL
        var lastApp = settings.LastAppId != null
            ? _webAppStore.GetById(settings.LastAppId)
            : null;
        var targetApp = lastApp ?? _webAppStore.Apps.FirstOrDefault();

        if (targetApp != null)
        {
            NavigateToApp(targetApp);
        }
        else
        {
            _webViewWindow.NavigateTo(settings.DefaultUrl);
        }

        _webViewWindow.Closing += (s, e) =>
        {
            if (_settingsStore.Settings.HideOnClose && !_isExiting)
            {
                e.Cancel = true;
                _webViewWindow.Hide();
                SaveWindowState();
            }
        };

        _webViewWindow.Deactivated += (s, e) =>
        {
            if (_settingsStore.Settings.HideOnDeactivate && _webViewWindow.IsVisible && !_webViewWindow.IsAlwaysOnTop)
            {
                _webViewWindow.Hide();
            }
        };
    }

    private void OnBrowserStateChanged(object? sender, BrowserStateChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeAppId) || _webViewWindow == null)
        {
            return;
        }

        _webAppStore.UpdateRuntimeState(
            _activeAppId,
            e.Title,
            e.Url,
            _webViewWindow.Width,
            _webViewWindow.Height,
            _webViewWindow.Left,
            _webViewWindow.Top,
            e.ZoomFactor);
    }

    private void OnAlwaysOnTopChanged(object? sender, AlwaysOnTopChangedEventArgs e)
    {
        _settingsStore.Update(s => s.AlwaysOnTop = e.IsAlwaysOnTop);

        if (!string.IsNullOrWhiteSpace(_activeAppId))
        {
            var app = _webAppStore.GetById(_activeAppId);
            if (app != null)
            {
                app.AlwaysOnTop = e.IsAlwaysOnTop;
                _webAppStore.Update(app);
            }
        }

        RebuildMenu();
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        _settingsStore.Update(s => s.AlwaysOnTop = enabled);

        if (!string.IsNullOrWhiteSpace(_activeAppId))
        {
            var app = _webAppStore.GetById(_activeAppId);
            if (app != null)
            {
                app.AlwaysOnTop = enabled;
                _webAppStore.Update(app);
            }
        }

        _webViewWindow?.SetAlwaysOnTop(enabled);
    }

    private void NavigateToApp(WebAppItem app)
    {
        if (_webViewWindow == null) return;

        _activeAppId = app.Id;

        // Apply per-app window size if specified
        if (app.Width > 0) _webViewWindow.Width = app.Width;
        if (app.Height > 0) _webViewWindow.Height = app.Height;
        _webViewWindow.SetAlwaysOnTop(_settingsStore.Settings.AlwaysOnTop || app.AlwaysOnTop);
        _webViewWindow.Title = $"{app.Name} - TrayWebApp";
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = TruncateTrayText($"TrayWebApp - {app.Name}");
        }
        _webViewWindow.SetHomeUrl(app.Url);
        _webViewWindow.SetZoomFactor(app.ZoomFactor);
        _webViewWindow.SetVisualOpacity(_settingsStore.Settings.WindowOpacity);
        _webViewWindow.OpenNewWindowsExternally = _settingsStore.Settings.OpenNewWindowsExternally;
        _webViewWindow.SetAddressBarVisible(_settingsStore.Settings.ShowAddressBar);

        // Apply User-Agent override
        var userAgent = app.UserAgent?.ToLowerInvariant() switch
        {
            "mobile" => MobileUserAgent,
            "desktop" or "" or null => DesktopUserAgent,
            _ => app.UserAgent  // custom UA string
        };
        _webViewWindow.SetUserAgent(userAgent);
        _webViewWindow.NavigateTo(string.IsNullOrWhiteSpace(app.LastVisitedUrl) ? app.Url : app.LastVisitedUrl);
    }

    private static string TruncateTrayText(string text)
    {
        return text.Length <= 63 ? text : text[..60] + "...";
    }

    private void PositionWindowNearTray()
    {
        if (_webViewWindow == null) return;

        var settings = _settingsStore.Settings;
        var activeApp = !string.IsNullOrWhiteSpace(_activeAppId)
            ? _webAppStore.GetById(_activeAppId)
            : null;

        if (activeApp?.WindowX >= 0 && activeApp.WindowY >= 0 &&
            IsWindowPositionVisible(activeApp.WindowX, activeApp.WindowY, _webViewWindow.Width, _webViewWindow.Height))
        {
            _webViewWindow.Left = activeApp.WindowX;
            _webViewWindow.Top = activeApp.WindowY;
            return;
        }

        // Use saved global position if valid
        if (settings.WindowX >= 0 && settings.WindowY >= 0 &&
            IsWindowPositionVisible(settings.WindowX, settings.WindowY, _webViewWindow.Width, _webViewWindow.Height))
        {
            _webViewWindow.Left = settings.WindowX;
            _webViewWindow.Top = settings.WindowY;
            return;
        }

        // Position near the taskbar on the monitor where the cursor currently is.
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        _webViewWindow.Left = workArea.Right - _webViewWindow.Width - 12;
        _webViewWindow.Top = workArea.Bottom - _webViewWindow.Height - 12;
    }

    private static bool IsWindowPositionVisible(double left, double top, double width, double height)
    {
        var rect = new Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            Math.Max(1, (int)Math.Round(width)),
            Math.Max(1, (int)Math.Round(height)));

        return Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(rect));
    }

    private void SaveWindowState()
    {
        if (_webViewWindow == null) return;

        _settingsStore.Update(s =>
        {
            s.WindowWidth = (int)_webViewWindow.Width;
            s.WindowHeight = (int)_webViewWindow.Height;
            s.WindowX = _webViewWindow.Left;
            s.WindowY = _webViewWindow.Top;
        });

        if (!string.IsNullOrWhiteSpace(_activeAppId))
        {
            _webAppStore.UpdateRuntimeState(
                _activeAppId,
                _webViewWindow.CurrentTitle,
                _webViewWindow.CurrentUrl,
                _webViewWindow.Width,
                _webViewWindow.Height,
                _webViewWindow.Left,
                _webViewWindow.Top,
                _webViewWindow.CurrentZoomFactor);
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isExiting = true;

        SaveWindowState();
        _hotkeyService?.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _manageWindow?.Close();
        _settingsWindow?.Close();
        _webViewWindow?.Close();
        _hiddenWindow?.Close();
    }
}

/// <summary>
/// Custom renderer for dark-themed context menus
/// </summary>
internal class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(Color.FromArgb(60, 60, 80));
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
        else
        {
            base.OnRenderMenuItemBackground(e);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = e.Item.ContentRectangle;
        using var pen = new Pen(Color.FromArgb(64, 64, 96));
        var y = bounds.Top + bounds.Height / 2;
        e.Graphics.DrawLine(pen, bounds.Left + 8, y, bounds.Right - 8, y);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Color.FromArgb(30, 30, 46));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(64, 64, 96));
        var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(64, 64, 96);
    public override Color MenuItemBorder => Color.FromArgb(64, 64, 96);
    public override Color MenuItemSelected => Color.FromArgb(60, 60, 80);
    public override Color MenuStripGradientBegin => Color.FromArgb(30, 30, 46);
    public override Color MenuStripGradientEnd => Color.FromArgb(30, 30, 46);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(50, 50, 70);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 80);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(40, 40, 60);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(50, 50, 70);
    public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 46);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 46);
    public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 46);
    public override Color SeparatorDark => Color.FromArgb(64, 64, 96);
    public override Color SeparatorLight => Color.FromArgb(30, 30, 46);
    public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 46);
}
