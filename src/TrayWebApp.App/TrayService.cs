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
    private readonly Dictionary<string, WebViewWindow> _webAppWindows = new();
    private readonly Dictionary<WebViewWindow, string> _windowAppIds = new();
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

    private enum SnapPresetKind
    {
        LeftHalf,
        RightHalf,
        LeftThird,
        RightThird,
        TopHalf,
        BottomHalf,
        CenterCompact,
        CenterLarge
    }

    private sealed class SnapPreset
    {
        public string Name { get; init; } = "";
        public SnapPresetKind Kind { get; init; }
    }

    private static readonly SnapPreset[] SnapPresets =
    {
        new() { Name = "왼쪽 1/2", Kind = SnapPresetKind.LeftHalf },
        new() { Name = "오른쪽 1/2", Kind = SnapPresetKind.RightHalf },
        new() { Name = "왼쪽 1/3", Kind = SnapPresetKind.LeftThird },
        new() { Name = "오른쪽 1/3", Kind = SnapPresetKind.RightThird },
        new() { Name = "위쪽 1/2", Kind = SnapPresetKind.TopHalf },
        new() { Name = "아래쪽 1/2", Kind = SnapPresetKind.BottomHalf },
        new() { Name = "가운데 소형", Kind = SnapPresetKind.CenterCompact },
        new() { Name = "가운데 대형", Kind = SnapPresetKind.CenterLarge },
    };

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

        // Ctrl+Alt+K: Open app switcher
        _hotkeyService.Register(
            HotkeyService.MOD_CONTROL | HotkeyService.MOD_ALT,
            HotkeyService.VK_K,
            () => ShowQuickSwitch(GetActiveWindow()));

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
        menu.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
        menu.ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush");
        menu.Renderer = new DarkMenuRenderer();
        menu.ShowImageMargin = true;

        // Header
        var header = new ToolStripLabel("TrayWebApp")
        {
            ForeColor = ThemeManager.ToDrawingColor("AccentBrush"),
            Font = new Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
            Padding = new Padding(4, 6, 4, 4)
        };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var activeWindow = GetActiveWindow();
        var openToggle = new ToolStripMenuItem(activeWindow?.IsVisible == true ? "활성 창 숨기기" : "활성 창 열기")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        openToggle.Click += (s, e) => ToggleWindow();
        menu.Items.Add(openToggle);

        var reloadCurrent = new ToolStripMenuItem("활성 페이지 새로고침")
        {
            Enabled = activeWindow?.IsLoaded == true,
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        reloadCurrent.Click += (s, e) => GetActiveWindow()?.WebView.CoreWebView2?.Reload();
        menu.Items.Add(reloadCurrent);

        var openExternal = new ToolStripMenuItem("활성 페이지를 기본 브라우저로 열기")
        {
            Enabled = activeWindow?.IsLoaded == true,
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        openExternal.Click += (s, e) => GetActiveWindow()?.OpenCurrentInExternalBrowser();
        menu.Items.Add(openExternal);

        menu.Items.Add(new ToolStripSeparator());

        // Web App entries
        for (int i = 0; i < _webAppStore.Apps.Count; i++)
        {
            var app = _webAppStore.Apps[i];
            var isActive = app.Id == _activeAppId;
            var isOpen = IsAppWindowOpen(app.Id);
            var prefix = isActive ? "> " : isOpen ? "* " : "  ";
            var shortcutHint = i < 9 ? $"  [Ctrl+Alt+{i + 1}]" : "";
            var item = new ToolStripMenuItem($"{prefix}{app.Name}{shortcutHint}")
            {
                Tag = app,
                Image = LoadMenuImage(app.IconPath),
                ForeColor = isActive ? ThemeManager.ToDrawingColor("AccentBrush") : ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f, isActive ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular)
            };
            item.Click += OnWebAppSelected;
            menu.Items.Add(item);
        }

        if (_webAppStore.Apps.Count == 0)
        {
            var empty = new ToolStripLabel("   등록된 앱 없음")
            {
                ForeColor = ThemeManager.ToDrawingColor("TextMutedBrush"),
                Font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Italic)
            };
            menu.Items.Add(empty);
        }

        menu.Items.Add(new ToolStripSeparator());

        var openWindows = GetOpenWindowApps().ToList();
        if (openWindows.Count > 0)
        {
            var openWindowsMenu = new ToolStripMenuItem($"열린 창 ({openWindows.Count})")
            {
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            openWindowsMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
            openWindowsMenu.DropDown.Renderer = new DarkMenuRenderer();

            foreach (var app in openWindows)
            {
                var item = new ToolStripMenuItem(app.Name)
                {
                    Tag = app,
                    Image = LoadMenuImage(app.IconPath),
                    ForeColor = app.Id == _activeAppId ? ThemeManager.ToDrawingColor("AccentBrush") : ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                    Font = new Font("Segoe UI", 9.5f, app.Id == _activeAppId ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular)
                };
                item.Click += (s, e) => FocusOpenApp(app);
                openWindowsMenu.DropDownItems.Add(item);
            }

            menu.Items.Add(openWindowsMenu);
            menu.Items.Add(new ToolStripSeparator());
        }

        var recentApps = _webAppStore.Apps
            .Where(a => a.LastUsedAtUtc.HasValue)
            .OrderByDescending(a => a.LastUsedAtUtc)
            .Take(5)
            .ToList();

        if (recentApps.Count > 0)
        {
            var recentMenu = new ToolStripMenuItem("최근 사용")
            {
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            recentMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
            recentMenu.DropDown.Renderer = new DarkMenuRenderer();

            foreach (var app in recentApps)
            {
                var item = new ToolStripMenuItem(app.Name)
                {
                    Tag = app,
                    Image = LoadMenuImage(app.IconPath),
                    ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
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
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        presetsMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
        presetsMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var preset in WindowPreset.Defaults)
        {
            var pItem = new ToolStripMenuItem(preset.ToString())
            {
                Tag = preset,
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            pItem.Click += OnPresetSelected;
            presetsMenu.DropDownItems.Add(pItem);
        }
        menu.Items.Add(presetsMenu);

        var snapMenu = new ToolStripMenuItem("창 배치")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        snapMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
        snapMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var preset in SnapPresets)
        {
            var item = new ToolStripMenuItem(preset.Name)
            {
                Tag = preset,
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            item.Click += OnSnapPresetSelected;
            snapMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(snapMenu);

        var opacityMenu = new ToolStripMenuItem("투명도")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        opacityMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
        opacityMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var option in new[] { 1.0, 0.85, 0.7, 0.55, 0.4, 0.3 })
        {
            var percent = (int)(option * 100);
            var item = new ToolStripMenuItem($"{percent}%")
            {
                Checked = Math.Abs(_settingsStore.Settings.WindowOpacity - option) < 0.01,
                Tag = option,
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            item.Click += OnOpacitySelected;
            opacityMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(opacityMenu);

        var themeMenu = new ToolStripMenuItem("테마")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        themeMenu.DropDown.BackColor = ThemeManager.ToDrawingColor("BackgroundBrush");
        themeMenu.DropDown.Renderer = new DarkMenuRenderer();

        foreach (var option in new[] { ("다크 모드", "Dark"), ("라이트 모드", "Light") })
        {
            var item = new ToolStripMenuItem(option.Item1)
            {
                Checked = string.Equals(_settingsStore.Settings.ThemeMode, option.Item2, StringComparison.OrdinalIgnoreCase),
                Tag = option.Item2,
                ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
                Font = new Font("Segoe UI", 9.5f)
            };
            item.Click += OnThemeSelected;
            themeMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(themeMenu);

        var privacyMode = new ToolStripMenuItem("프라이버시 모드")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
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

            foreach (var window in GetLoadedWindows())
            {
                window.SetVisualOpacity(0.45);
                window.SetAlwaysOnTop(true);
            }
            SetAlwaysOnTop(true);

            RebuildMenu();
        };
        menu.Items.Add(privacyMode);

        // Always On Top toggle
        var alwaysOnTop = new ToolStripMenuItem("항상 위")
        {
            Checked = _settingsStore.Settings.AlwaysOnTop,
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
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
        var hideOnBlur = new ToolStripMenuItem("바깥 클릭 시 숨기기")
        {
            Checked = _settingsStore.Settings.HideOnDeactivate,
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
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
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        manage.Click += OnManageAppsClick;
        menu.Items.Add(manage);

        // Settings
        var settings = new ToolStripMenuItem("설정...")
        {
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
            Font = new Font("Segoe UI", 9.5f)
        };
        settings.Click += OnSettingsClick;
        menu.Items.Add(settings);

        // Run at Startup
        var startup = new ToolStripMenuItem("Windows 시작 시 실행")
        {
            Checked = StartupService.IsRegistered(),
            ForeColor = ThemeManager.ToDrawingColor("TextPrimaryBrush"),
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
            ForeColor = ThemeManager.ToDrawingColor("DangerBrush"),
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
            var window = GetOrCreateActiveWindow();
            window.Width = preset.Width;
            window.Height = preset.Height;
            _settingsStore.Update(s =>
            {
                s.WindowWidth = preset.Width;
                s.WindowHeight = preset.Height;
            });
            ShowWindow(window);
        }
    }

    private void OnSnapPresetSelected(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is SnapPreset preset)
        {
            var window = GetOrCreateActiveWindow();
            ApplySnapPreset(window, preset.Kind);
            SaveWindowState(window);
            ShowWindow(window, reposition: false);
        }
    }

    private void OnOpacitySelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is not double opacity)
        {
            return;
        }

        _settingsStore.Update(s => s.WindowOpacity = opacity);

        foreach (var window in GetLoadedWindows())
        {
            window.SetVisualOpacity(opacity);
        }

        RebuildMenu();
    }

    private void OnThemeSelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem menuItem)
        {
            return;
        }

        var themeMode = ThemeManager.NormalizeThemeMode(menuItem.Tag?.ToString());
        _settingsStore.Update(s => s.ThemeMode = themeMode);
        ThemeManager.Apply(themeMode);
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

    private void OnWebViewWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control ||
            e.Key != System.Windows.Input.Key.K)
        {
            return;
        }

        if (sender is WebViewWindow owner)
        {
            ShowQuickSwitch(owner);
            e.Handled = true;
        }
    }

    private void ShowQuickSwitch(WebViewWindow? owner)
    {
        if (_webAppStore.Apps.Count == 0)
        {
            return;
        }

        var quickSwitch = new QuickSwitchWindow(_webAppStore.Apps)
        {
            Topmost = owner?.IsAlwaysOnTop == true || _settingsStore.Settings.AlwaysOnTop
        };

        if (owner != null && owner.IsLoaded)
        {
            quickSwitch.Owner = owner;
        }

        if (quickSwitch.ShowDialog() == true && quickSwitch.SelectedApp != null)
        {
            OpenApp(quickSwitch.SelectedApp);
            RebuildMenu();
        }
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

        var window = GetOrCreateAppWindow(app, out var created);
        if (created)
        {
            NavigateToApp(window, app);
        }
        ShowWindow(window);
        RefreshFaviconIfNeeded(app);
    }

    private void ToggleWindow()
    {
        var window = GetActiveWindow();
        if (window == null || !window.IsLoaded)
        {
            window = GetOrCreateActiveWindow();
            ShowWindow(window);
        }
        else if (window.IsVisible)
        {
            SaveWindowState(window);
            window.Hide();
        }
        else
        {
            ShowWindow(window);
        }
    }

    private void ShowWindow(WebViewWindow window, bool reposition = true)
    {
        _webViewWindow = window;
        _activeAppId = GetAppIdForWindow(window) ?? _activeAppId;

        window.OpenNewWindowsExternally = _settingsStore.Settings.OpenNewWindowsExternally;
        window.SetAddressBarVisible(_settingsStore.Settings.ShowAddressBar);
        window.SetVisualOpacity(_settingsStore.Settings.WindowOpacity);
        window.Show();
        if (reposition)
        {
            PositionWindowNearTray(window);
        }
        window.Activate();
        window.BringToTopIfPinned();
        UpdateTrayTextForActiveWindow();
    }

    private WebViewWindow GetOrCreateActiveWindow()
    {
        var activeWindow = GetActiveWindow();
        if (activeWindow != null && activeWindow.IsLoaded)
        {
            return activeWindow;
        }

        var settings = _settingsStore.Settings;
        var lastApp = settings.LastAppId != null
            ? _webAppStore.GetById(settings.LastAppId)
            : null;
        var targetApp = lastApp ?? _webAppStore.Apps.FirstOrDefault();

        if (targetApp != null)
        {
            var window = GetOrCreateAppWindow(targetApp, out var created);
            if (created)
            {
                NavigateToApp(window, targetApp);
            }
            return window;
        }

        var defaultWindow = GetOrCreateWindow("__default", null);
        _webViewWindow = defaultWindow;
        defaultWindow.NavigateTo(settings.DefaultUrl);
        return defaultWindow;
    }

    private WebViewWindow GetOrCreateAppWindow(WebAppItem app, out bool created)
    {
        if (_webAppWindows.TryGetValue(app.Id, out var existing) && existing.IsLoaded)
        {
            created = false;
            _webViewWindow = existing;
            _activeAppId = app.Id;
            return existing;
        }

        created = true;
        var window = GetOrCreateWindow(app.Id, app);
        _webAppWindows[app.Id] = window;
        _windowAppIds[window] = app.Id;
        _webViewWindow = window;
        _activeAppId = app.Id;
        return window;
    }

    private WebViewWindow GetOrCreateWindow(string windowKey, WebAppItem? app)
    {
        if (_webAppWindows.TryGetValue(windowKey, out var existing) && existing.IsLoaded)
        {
            return existing;
        }

        var settings = _settingsStore.Settings;
        var window = new WebViewWindow(GetUserDataFolder(app))
        {
            Width = app?.Width > 0 ? app.Width : settings.WindowWidth,
            Height = app?.Height > 0 ? app.Height : settings.WindowHeight
        };
        window.SetAlwaysOnTop(settings.AlwaysOnTop || app?.AlwaysOnTop == true);
        window.SetVisualOpacity(settings.WindowOpacity);
        window.OpenNewWindowsExternally = settings.OpenNewWindowsExternally;
        window.SetAddressBarVisible(settings.ShowAddressBar);
        window.BrowserStateChanged += OnBrowserStateChanged;
        window.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        window.PreviewKeyDown += OnWebViewWindowPreviewKeyDown;
        _webAppWindows[windowKey] = window;
        _windowAppIds[window] = windowKey;

        window.Closing += (s, e) =>
        {
            if (_settingsStore.Settings.HideOnClose && !_isExiting)
            {
                e.Cancel = true;
                SaveWindowState(window);
                window.Hide();
            }
        };

        window.Closed += (s, e) =>
        {
            SaveWindowState(window);
            _webAppWindows.Remove(windowKey);
            _windowAppIds.Remove(window);
            if (_webViewWindow == window)
            {
                _webViewWindow = GetLoadedWindows().FirstOrDefault();
                _activeAppId = _webViewWindow != null ? GetAppIdForWindow(_webViewWindow) : null;
            }
            RebuildMenu();
        };

        window.Activated += (s, e) =>
        {
            _webViewWindow = window;
            _activeAppId = GetAppIdForWindow(window);
            UpdateTrayTextForActiveWindow();
        };

        window.Deactivated += (s, e) =>
        {
            if (_settingsStore.Settings.HideOnDeactivate && window.IsVisible && !window.IsAlwaysOnTop)
            {
                SaveWindowState(window);
                window.Hide();
            }
        };

        return window;
    }

    private static string? GetUserDataFolder(WebAppItem? app)
    {
        if (app?.UseIsolatedSession != true)
        {
            return null;
        }

        var safeId = new string(app.Id
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = Guid.NewGuid().ToString("N");
        }

        return Path.Combine(AppPaths.DataDirectory, "WebView2Profiles", safeId);
    }

    private WebViewWindow? GetActiveWindow()
    {
        if (_webViewWindow != null && _webViewWindow.IsLoaded)
        {
            return _webViewWindow;
        }

        if (!string.IsNullOrWhiteSpace(_activeAppId) &&
            _webAppWindows.TryGetValue(_activeAppId, out var activeWindow) &&
            activeWindow.IsLoaded)
        {
            _webViewWindow = activeWindow;
            return activeWindow;
        }

        _webViewWindow = GetLoadedWindows().FirstOrDefault();
        _activeAppId = _webViewWindow != null ? GetAppIdForWindow(_webViewWindow) : null;
        return _webViewWindow;
    }

    private bool IsAppWindowOpen(string appId)
    {
        return _webAppWindows.TryGetValue(appId, out var window) && window.IsLoaded;
    }

    private IEnumerable<WebViewWindow> GetLoadedWindows()
    {
        return _webAppWindows.Values.Where(window => window.IsLoaded).Distinct();
    }

    private IEnumerable<WebAppItem> GetOpenWindowApps()
    {
        foreach (var app in _webAppStore.Apps)
        {
            if (IsAppWindowOpen(app.Id))
            {
                yield return app;
            }
        }
    }

    private string? GetAppIdForWindow(WebViewWindow window)
    {
        return _windowAppIds.TryGetValue(window, out var appId) && appId != "__default"
            ? appId
            : null;
    }

    private void FocusOpenApp(WebAppItem app)
    {
        if (_webAppWindows.TryGetValue(app.Id, out var window) && window.IsLoaded)
        {
            _activeAppId = app.Id;
            _webViewWindow = window;
            _settingsStore.Update(s => s.LastAppId = app.Id);
            ShowWindow(window);
        }
        else
        {
            OpenApp(app);
        }
    }

    private void OnBrowserStateChanged(object? sender, BrowserStateChangedEventArgs e)
    {
        if (sender is not WebViewWindow window)
        {
            return;
        }

        var appId = GetAppIdForWindow(window);
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        _webAppStore.UpdateRuntimeState(
            appId,
            e.Title,
            e.Url,
            window.Width,
            window.Height,
            window.Left,
            window.Top,
            e.ZoomFactor,
            e.Tabs,
            e.ActiveTabIndex);
    }

    private void OnAlwaysOnTopChanged(object? sender, AlwaysOnTopChangedEventArgs e)
    {
        _settingsStore.Update(s => s.AlwaysOnTop = e.IsAlwaysOnTop);

        if (sender is WebViewWindow window)
        {
            var appId = GetAppIdForWindow(window);
            var app = !string.IsNullOrWhiteSpace(appId) ? _webAppStore.GetById(appId) : null;
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

        GetActiveWindow()?.SetAlwaysOnTop(enabled);
    }

    private void NavigateToApp(WebViewWindow window, WebAppItem app)
    {
        _activeAppId = app.Id;
        _webViewWindow = window;

        // Apply per-app window size if specified
        if (app.Width > 0) window.Width = app.Width;
        if (app.Height > 0) window.Height = app.Height;
        window.SetAlwaysOnTop(_settingsStore.Settings.AlwaysOnTop || app.AlwaysOnTop);
        window.Title = $"{app.Name} - TrayWebApp";
        UpdateTrayTextForActiveWindow();
        window.SetHomeUrl(app.Url);
        window.SetZoomFactor(app.ZoomFactor);
        window.SetVisualOpacity(_settingsStore.Settings.WindowOpacity);
        window.OpenNewWindowsExternally = _settingsStore.Settings.OpenNewWindowsExternally;
        window.SetAddressBarVisible(_settingsStore.Settings.ShowAddressBar);

        // Apply User-Agent override
        var userAgent = app.UserAgent?.ToLowerInvariant() switch
        {
            "mobile" => MobileUserAgent,
            "desktop" or "" or null => DesktopUserAgent,
            _ => app.UserAgent  // custom UA string
        };
        window.SetUserAgent(userAgent);
        if (app.Tabs.Count > 0)
        {
            window.RestoreTabs(app.Tabs, app.LastActiveTabIndex, app.Url);
        }
        else
        {
            window.NavigateTo(string.IsNullOrWhiteSpace(app.LastVisitedUrl) ? app.Url : app.LastVisitedUrl);
        }
    }

    private static string TruncateTrayText(string text)
    {
        return text.Length <= 63 ? text : text[..60] + "...";
    }

    private void UpdateTrayTextForActiveWindow()
    {
        if (_notifyIcon == null)
        {
            return;
        }

        var activeApp = !string.IsNullOrWhiteSpace(_activeAppId)
            ? _webAppStore.GetById(_activeAppId)
            : null;
        _notifyIcon.Text = TruncateTrayText(activeApp != null
            ? $"TrayWebApp - {activeApp.Name}"
            : "TrayWebApp");
    }

    private static void ApplySnapPreset(WebViewWindow window, SnapPresetKind kind)
    {
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        const int gap = 12;

        double left;
        double top;
        double width;
        double height;

        switch (kind)
        {
            case SnapPresetKind.LeftHalf:
                left = workArea.Left + gap;
                top = workArea.Top + gap;
                width = (workArea.Width / 2.0) - (gap * 1.5);
                height = workArea.Height - (gap * 2);
                break;
            case SnapPresetKind.RightHalf:
                width = (workArea.Width / 2.0) - (gap * 1.5);
                height = workArea.Height - (gap * 2);
                left = workArea.Right - width - gap;
                top = workArea.Top + gap;
                break;
            case SnapPresetKind.LeftThird:
                left = workArea.Left + gap;
                top = workArea.Top + gap;
                width = (workArea.Width / 3.0) - (gap * 1.33);
                height = workArea.Height - (gap * 2);
                break;
            case SnapPresetKind.RightThird:
                width = (workArea.Width / 3.0) - (gap * 1.33);
                height = workArea.Height - (gap * 2);
                left = workArea.Right - width - gap;
                top = workArea.Top + gap;
                break;
            case SnapPresetKind.TopHalf:
                left = workArea.Left + gap;
                top = workArea.Top + gap;
                width = workArea.Width - (gap * 2);
                height = (workArea.Height / 2.0) - (gap * 1.5);
                break;
            case SnapPresetKind.BottomHalf:
                width = workArea.Width - (gap * 2);
                height = (workArea.Height / 2.0) - (gap * 1.5);
                left = workArea.Left + gap;
                top = workArea.Bottom - height - gap;
                break;
            case SnapPresetKind.CenterLarge:
                width = Math.Min(1100, workArea.Width - (gap * 2));
                height = Math.Min(820, workArea.Height - (gap * 2));
                left = workArea.Left + (workArea.Width - width) / 2.0;
                top = workArea.Top + (workArea.Height - height) / 2.0;
                break;
            case SnapPresetKind.CenterCompact:
            default:
                width = Math.Min(430, workArea.Width - (gap * 2));
                height = Math.Min(720, workArea.Height - (gap * 2));
                left = workArea.Left + (workArea.Width - width) / 2.0;
                top = workArea.Top + (workArea.Height - height) / 2.0;
                break;
        }

        window.Left = Math.Round(left);
        window.Top = Math.Round(top);
        window.Width = Math.Round(Math.Max(320, width));
        window.Height = Math.Round(Math.Max(360, height));
    }

    private void PositionWindowNearTray(WebViewWindow window)
    {
        if (window == null) return;

        var settings = _settingsStore.Settings;
        var activeApp = !string.IsNullOrWhiteSpace(_activeAppId)
            ? _webAppStore.GetById(_activeAppId)
            : null;

        if (activeApp?.WindowX >= 0 && activeApp.WindowY >= 0 &&
            IsWindowPositionVisible(activeApp.WindowX, activeApp.WindowY, window.Width, window.Height))
        {
            window.Left = activeApp.WindowX;
            window.Top = activeApp.WindowY;
            return;
        }

        // Use saved global position if valid
        if (settings.WindowX >= 0 && settings.WindowY >= 0 &&
            IsWindowPositionVisible(settings.WindowX, settings.WindowY, window.Width, window.Height))
        {
            window.Left = settings.WindowX;
            window.Top = settings.WindowY;
            return;
        }

        // Position near the taskbar on the monitor where the cursor currently is.
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        window.Left = workArea.Right - window.Width - 12;
        window.Top = workArea.Bottom - window.Height - 12;
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
        foreach (var window in GetLoadedWindows().ToList())
        {
            SaveWindowState(window);
        }
    }

    private void SaveWindowState(WebViewWindow window)
    {
        if (!window.IsLoaded) return;

        _settingsStore.Update(s =>
        {
            s.WindowWidth = (int)window.Width;
            s.WindowHeight = (int)window.Height;
            s.WindowX = window.Left;
            s.WindowY = window.Top;
        });

        var appId = GetAppIdForWindow(window);
        if (!string.IsNullOrWhiteSpace(appId))
        {
            _webAppStore.UpdateRuntimeState(
                appId,
                window.CurrentTitle,
                window.CurrentUrl,
                window.Width,
                window.Height,
                window.Left,
                window.Top,
                window.CurrentZoomFactor,
                window.CurrentTabs,
                window.CurrentActiveTabIndex);
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
        foreach (var window in GetLoadedWindows().ToList())
        {
            window.Close();
        }
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
            using var brush = new SolidBrush(ThemeManager.ToDrawingColor("SurfaceHoverBrush"));
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
        using var pen = new Pen(ThemeManager.ToDrawingColor("BorderBrush"));
        var y = bounds.Top + bounds.Height / 2;
        e.Graphics.DrawLine(pen, bounds.Left + 8, y, bounds.Right - 8, y);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(ThemeManager.ToDrawingColor("BackgroundBrush"));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(ThemeManager.ToDrawingColor("BorderBrush"));
        var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => ThemeManager.ToDrawingColor("BorderBrush");
    public override Color MenuItemBorder => ThemeManager.ToDrawingColor("BorderBrush");
    public override Color MenuItemSelected => ThemeManager.ToDrawingColor("SurfaceHoverBrush");
    public override Color MenuStripGradientBegin => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color MenuStripGradientEnd => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color MenuItemSelectedGradientBegin => ThemeManager.ToDrawingColor("SurfaceAltBrush");
    public override Color MenuItemSelectedGradientEnd => ThemeManager.ToDrawingColor("SurfaceHoverBrush");
    public override Color MenuItemPressedGradientBegin => ThemeManager.ToDrawingColor("SurfaceBrush");
    public override Color MenuItemPressedGradientEnd => ThemeManager.ToDrawingColor("SurfaceAltBrush");
    public override Color ImageMarginGradientBegin => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color ImageMarginGradientMiddle => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color ImageMarginGradientEnd => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color SeparatorDark => ThemeManager.ToDrawingColor("BorderBrush");
    public override Color SeparatorLight => ThemeManager.ToDrawingColor("BackgroundBrush");
    public override Color ToolStripDropDownBackground => ThemeManager.ToDrawingColor("BackgroundBrush");
}

