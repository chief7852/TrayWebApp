using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TrayWebApp.Core.Models;
using TrayWebApp.Core.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using WindowInteropHelper = System.Windows.Interop.WindowInteropHelper;

namespace TrayWebApp.App;

public sealed class BrowserStateChangedEventArgs : EventArgs
{
    public string? Title { get; init; }
    public string? Url { get; init; }
    public double ZoomFactor { get; init; }
    public IReadOnlyList<WebAppTabState> Tabs { get; init; } = Array.Empty<WebAppTabState>();
    public int ActiveTabIndex { get; init; }
}

public sealed class AlwaysOnTopChangedEventArgs : EventArgs
{
    public bool IsAlwaysOnTop { get; init; }
}

internal sealed class BrowserTab
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public WebView2 WebView { get; } = new()
    {
        DefaultBackgroundColor = System.Drawing.Color.Transparent
    };
    public System.Windows.Controls.Button HeaderButton { get; } = new();
    public TextBlock TitleText { get; } = new();
    public TextBlock CloseText { get; } = new();
    public string Title { get; set; } = "New tab";
    public string? PendingUrl { get; set; }
    public string? PendingUserAgent { get; set; }
    public double? PendingZoomFactor { get; set; }
    public bool IsInitialized { get; set; }
    public bool IsClosed { get; set; }
}

internal sealed class ClosedTabState
{
    public string Url { get; init; } = "";
    public string? Title { get; init; }
}

/// <summary>
/// WebView2-based floating browser window with custom chrome.
/// </summary>
public partial class WebViewWindow : Window
{
    private readonly List<BrowserTab> _tabs = new();
    private readonly Stack<ClosedTabState> _closedTabs = new();
    private BrowserTab? _activeTab;
    private CoreWebView2Environment? _webViewEnvironment;
    private readonly string _userDataFolder;
    private string _pendingUrl = "";
    private string? _pendingUserAgent;
    private double? _pendingZoomFactor;
    private string _homeUrl = "https://www.google.com";
    private string? _baseUserAgent;
    private double _zoomFactor = 1.0;
    private double _visualOpacity = 1.0;
    private bool _suppressOpacitySliderEvent;
    private bool _isAlwaysOnTop;
    private bool _isMobileView;
    private double? _desktopWidthBeforeMobile;
    private double? _desktopHeightBeforeMobile;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int ResizeBorderThickness = 8;
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int MobileViewWidth = 390;
    private const int MobileViewHeight = 844;
    private const string MobileUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    public event EventHandler<BrowserStateChangedEventArgs>? BrowserStateChanged;
    public event EventHandler<AlwaysOnTopChangedEventArgs>? AlwaysOnTopChanged;

    public WebView2 WebView => _activeTab?.WebView ?? throw new InvalidOperationException("No active browser tab.");
    public string? CurrentUrl => _activeTab?.WebView.CoreWebView2?.Source;
    public string? CurrentTitle => _activeTab?.WebView.CoreWebView2?.DocumentTitle;
    public double CurrentZoomFactor => _activeTab?.WebView.ZoomFactor ?? 1.0;
    public IReadOnlyList<WebAppTabState> CurrentTabs => GetCurrentTabs();
    public int CurrentActiveTabIndex => _activeTab == null ? 0 : Math.Max(0, _tabs.IndexOf(_activeTab));
    public bool IsAlwaysOnTop => _isAlwaysOnTop;
    public bool IsMobileView => _isMobileView;
    public bool OpenNewWindowsExternally { get; set; }

    public WebViewWindow(string? userDataFolder = null)
    {
        _userDataFolder = string.IsNullOrWhiteSpace(userDataFolder)
            ? AppPaths.WebViewDataDirectory
            : userDataFolder;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
        UpdateOpacitySlider(_visualOpacity);
        UpdateMobileViewButton();
        UpdateThemeToggleButton();
        InitializeWebView();
    }

    private void InitializeWebView()
    {
        CreateNewTab(_homeUrl);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var screenPoint = GetScreenPoint(lParam);
        var localPoint = PointFromScreen(screenPoint);
        var width = ActualWidth;
        var height = ActualHeight;
        var border = ResizeBorderThickness;

        var onLeft = localPoint.X >= 0 && localPoint.X < border;
        var onRight = localPoint.X <= width && localPoint.X > width - border;
        var onTop = localPoint.Y >= 0 && localPoint.Y < border;
        var onBottom = localPoint.Y <= height && localPoint.Y > height - border;

        var hitTest = (onLeft, onRight, onTop, onBottom) switch
        {
            (true, false, true, false) => HtTopLeft,
            (false, true, true, false) => HtTopRight,
            (true, false, false, true) => HtBottomLeft,
            (false, true, false, true) => HtBottomRight,
            (true, false, false, false) => HtLeft,
            (false, true, false, false) => HtRight,
            (false, false, true, false) => HtTop,
            (false, false, false, true) => HtBottom,
            _ => 0
        };

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static System.Windows.Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = (short)(value & 0xFFFF);
        var y = (short)((value >> 16) & 0xFFFF);
        return new System.Windows.Point(x, y);
    }

    private BrowserTab CreateNewTab(string? initialUrl = null)
    {
        var tab = new BrowserTab
        {
            PendingUrl = NormalizeUrl(string.IsNullOrWhiteSpace(initialUrl) ? _homeUrl : initialUrl)
        };

        tab.HeaderButton.Style = (Style)FindResource("TabButton");
        tab.HeaderButton.Content = CreateTabHeader(tab);
        tab.HeaderButton.Tag = tab;
        tab.HeaderButton.Click += (s, e) => ActivateTab(tab);
        tab.HeaderButton.PreviewMouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                CloseTab(tab);
                e.Handled = true;
            }
        };
        tab.HeaderButton.ContextMenu = CreateTabContextMenu(tab);

        _tabs.Add(tab);
        TabsHost.Children.Add(tab.HeaderButton);
        ActivateTab(tab);
        _ = InitializeTabAsync(tab);
        return tab;
    }

    private StackPanel CreateTabHeader(BrowserTab tab)
    {
        tab.TitleText.Text = tab.Title;
        tab.TitleText.TextTrimming = TextTrimming.CharacterEllipsis;
        tab.TitleText.VerticalAlignment = VerticalAlignment.Center;
        tab.TitleText.Width = 82;

        tab.CloseText.Text = "x";
        tab.CloseText.FontSize = 12;
        tab.CloseText.FontWeight = FontWeights.Bold;
        tab.CloseText.Margin = new Thickness(6, 0, 0, 0);
        tab.CloseText.VerticalAlignment = VerticalAlignment.Center;
        tab.CloseText.Opacity = 0.7;
        tab.CloseText.Cursor = System.Windows.Input.Cursors.Hand;
        tab.CloseText.MouseLeftButtonDown += (s, e) =>
        {
            CloseTab(tab);
            e.Handled = true;
        };

        return new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Children =
            {
                tab.TitleText,
                tab.CloseText
            }
        };
    }

    private ContextMenu CreateTabContextMenu(BrowserTab tab)
    {
        var menu = new ContextMenu
        {
            Background = ThemeManager.GetBrush("BackgroundBrush"),
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush")
        };

        var duplicate = CreateTabMenuItem("탭 복제", () => DuplicateTab(tab));
        var close = CreateTabMenuItem("탭 닫기", () => CloseTab(tab));
        var closeOthers = CreateTabMenuItem("다른 탭 닫기", () => CloseOtherTabs(tab));
        var closeRight = CreateTabMenuItem("오른쪽 탭 닫기", () => CloseTabsToRight(tab));
        var reopen = CreateTabMenuItem("닫은 탭 다시 열기", ReopenClosedTab);

        menu.Opened += (s, e) =>
        {
            closeOthers.IsEnabled = _tabs.Count > 1;
            closeRight.IsEnabled = _tabs.IndexOf(tab) >= 0 && _tabs.IndexOf(tab) < _tabs.Count - 1;
            reopen.IsEnabled = _closedTabs.Count > 0;
        };

        menu.Items.Add(duplicate);
        menu.Items.Add(new Separator());
        menu.Items.Add(close);
        menu.Items.Add(closeOthers);
        menu.Items.Add(closeRight);
        menu.Items.Add(new Separator());
        menu.Items.Add(reopen);
        return menu;
    }

    private static MenuItem CreateTabMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, e) => action();
        return item;
    }

    private async Task InitializeTabAsync(BrowserTab tab)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var userDataFolder = _userDataFolder;
            Directory.CreateDirectory(userDataFolder);

            _webViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            if (tab.IsClosed)
            {
                return;
            }

            await tab.WebView.EnsureCoreWebView2Async(_webViewEnvironment);
            if (tab.IsClosed)
            {
                return;
            }

            tab.IsInitialized = true;

            var settings = tab.WebView.CoreWebView2.Settings;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsZoomControlEnabled = true;
            settings.AreDevToolsEnabled = true;

            tab.WebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            tab.WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            tab.WebView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
            tab.WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            tab.WebView.CoreWebView2.SourceChanged += OnSourceChanged;
            tab.WebView.ZoomFactorChanged += OnZoomFactorChanged;
            tab.WebView.CoreWebView2.DownloadStarting += OnDownloadStarting;
            tab.WebView.CoreWebView2.PermissionRequested += OnPermissionRequested;

            await tab.WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildOpacityScript());

            ApplyEffectiveUserAgent(tab);
            tab.PendingUserAgent = null;
            if (tab == _activeTab)
            {
                _pendingUserAgent = null;
            }

            var pendingZoomFactor = tab.PendingZoomFactor ?? _pendingZoomFactor;
            if (pendingZoomFactor.HasValue)
            {
                tab.WebView.ZoomFactor = Math.Clamp(pendingZoomFactor.Value, 0.25, 3.0);
                tab.PendingZoomFactor = null;
                if (tab == _activeTab)
                {
                    _pendingZoomFactor = null;
                }
            }

            var pendingUrl = tab.PendingUrl ?? _pendingUrl;
            if (!string.IsNullOrEmpty(pendingUrl))
            {
                tab.WebView.CoreWebView2.Navigate(pendingUrl);
                tab.PendingUrl = null;
                if (tab == _activeTab)
                {
                    _pendingUrl = "";
                }
            }

            await ApplyWebContentOpacityAsync(tab);

            if (tab == _activeTab)
            {
                StatusText.Text = "준비됨";
                RefreshActiveTabUi();
            }
        }
        catch (Exception ex)
        {
            if (tab.IsClosed)
            {
                return;
            }

            AppLogger.Error("Failed to initialize WebView2", ex);
            StatusText.Text = $"WebView2 오류: {ex.Message}";
            MessageBox.Show(
                $"WebView2를 초기화하지 못했습니다.\n\n{ex.Message}\n\nMicrosoft Edge WebView2 Runtime이 설치되어 있는지 확인하세요.",
                "TrayWebApp - 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    /// <summary>Navigate the WebView to the specified URL</summary>
    public void NavigateTo(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        url = NormalizeUrl(url);
        AddressInput.Text = url;

        var tab = _activeTab;
        if (tab?.IsInitialized == true && tab.WebView.CoreWebView2 != null)
        {
            tab.WebView.CoreWebView2.Navigate(url);
        }
        else
        {
            _pendingUrl = url;
            if (tab != null)
            {
                tab.PendingUrl = url;
            }
        }
    }

    public void SetHomeUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _homeUrl = NormalizeUrl(url);
        }
    }

    public void RestoreTabs(IReadOnlyList<WebAppTabState>? tabs, int activeTabIndex, string fallbackUrl)
    {
        var validTabs = tabs?
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Url))
            .Take(5)
            .ToList();

        if (validTabs == null || validTabs.Count == 0)
        {
            NavigateTo(fallbackUrl);
            return;
        }

        foreach (var tab in _tabs.ToList())
        {
            tab.IsClosed = true;
            TabsHost.Children.Remove(tab.HeaderButton);
            try
            {
                tab.WebView.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to dispose tab during restore: {ex.Message}");
            }
        }

        _tabs.Clear();
        _activeTab = null;
        WebViewHost.Content = null;

        foreach (var savedTab in validTabs)
        {
            var tab = CreateNewTab(savedTab.Url);
            tab.Title = string.IsNullOrWhiteSpace(savedTab.Title) ? "New tab" : savedTab.Title;
            tab.TitleText.Text = TruncateUrl(tab.Title, 18);
        }

        ActivateTab(_tabs[Math.Clamp(activeTabIndex, 0, _tabs.Count - 1)]);
    }

    public void SetZoomFactor(double zoomFactor)
    {
        zoomFactor = Math.Clamp(zoomFactor, 0.25, 3.0);

        var tab = _activeTab;
        if (tab?.IsInitialized == true)
        {
            tab.WebView.ZoomFactor = zoomFactor;
        }
        else
        {
            _pendingZoomFactor = zoomFactor;
            if (tab != null)
            {
                tab.PendingZoomFactor = zoomFactor;
            }
        }
    }

    public void SetAddressBarVisible(bool visible)
    {
        AddressInput.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        AddressBarRow.Height = visible ? new GridLength(36) : new GridLength(0);
    }

    public void SetVisualOpacity(double opacity)
    {
        _visualOpacity = Math.Clamp(opacity, 0.2, 1.0);
        Opacity = _visualOpacity;
        UpdateOpacitySlider(_visualOpacity);
        _ = ApplyWebContentOpacityAsync();
    }

    public void SetAlwaysOnTop(bool enabled)
    {
        _isAlwaysOnTop = enabled;

        // WPF Topmost can be lost after WebView2 focus, move, or restore operations.
        // Re-apply both WPF and native z-order state so pinned windows remain above normal windows.
        Topmost = false;
        Topmost = enabled;
        ApplyNativeTopMost(enabled);

        if (enabled)
        {
            Topmost = true;
        }

        PinButton.Opacity = enabled ? 1.0 : 0.5;
    }

    public void BringToTopIfPinned()
    {
        if (_isAlwaysOnTop)
        {
            SetAlwaysOnTop(true);
        }
    }

    public void OpenCurrentInExternalBrowser()
    {
        var url = CurrentUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open URL in external browser", ex);
            StatusText.Text = "기본 브라우저를 열 수 없습니다";
        }
    }

    /// <summary>Set the User-Agent string. Empty string restores the default.</summary>
    public void SetUserAgent(string? userAgent)
    {
        _baseUserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent;
        _pendingUserAgent = _baseUserAgent;

        foreach (var tab in _tabs)
        {
            tab.PendingUserAgent = _baseUserAgent;
            ApplyEffectiveUserAgent(tab);
        }
    }

    public void SetMobileView(bool enabled, bool reloadActiveTab = true)
    {
        if (_isMobileView == enabled)
        {
            return;
        }

        _isMobileView = enabled;

        if (enabled)
        {
            if (WindowState == WindowState.Normal)
            {
                _desktopWidthBeforeMobile = Width;
                _desktopHeightBeforeMobile = Height;
                Width = MobileViewWidth;
                Height = Math.Min(MobileViewHeight, SystemParameters.WorkArea.Height);
            }
        }
        else if (WindowState == WindowState.Normal)
        {
            if (_desktopWidthBeforeMobile is > 0)
            {
                Width = _desktopWidthBeforeMobile.Value;
            }

            if (_desktopHeightBeforeMobile is > 0)
            {
                Height = _desktopHeightBeforeMobile.Value;
            }
        }

        foreach (var tab in _tabs)
        {
            ApplyEffectiveUserAgent(tab);
        }

        UpdateMobileViewButton();

        if (reloadActiveTab && _activeTab?.IsInitialized == true)
        {
            _activeTab.WebView.CoreWebView2?.Reload();
        }
    }

    private void ApplyEffectiveUserAgent(BrowserTab tab)
    {
        var userAgent = _isMobileView
            ? MobileUserAgent
            : tab.PendingUserAgent ?? _pendingUserAgent ?? _baseUserAgent;

        if (tab.IsInitialized && tab.WebView.CoreWebView2 != null)
        {
            tab.WebView.CoreWebView2.Settings.UserAgent = string.IsNullOrEmpty(userAgent) ? "" : userAgent;
            return;
        }

        tab.PendingUserAgent = userAgent;
    }

    private void UpdateMobileViewButton()
    {
        MobileViewButton.Foreground = _isMobileView
            ? ThemeManager.GetBrush("AccentBrush")
            : ThemeManager.GetBrush("TextSecondaryBrush");
        MobileViewButton.ToolTip = _isMobileView ? "데스크톱 보기로 전환" : "모바일 보기";
    }

    private void ToggleThemeMode()
    {
        var currentMode = ThemeManager.NormalizeThemeMode(App.SettingsStore.Settings.ThemeMode);
        var nextMode = ThemeManager.IsLight(currentMode) ? "Dark" : "Light";

        App.SettingsStore.Update(settings => settings.ThemeMode = nextMode);
        ThemeManager.Apply(nextMode);
        UpdateThemeToggleButton();
        RefreshActiveTabUi();
    }

    private void UpdateThemeToggleButton()
    {
        var isLight = ThemeManager.IsLight(App.SettingsStore.Settings.ThemeMode);
        ThemeToggleButton.Foreground = isLight
            ? ThemeManager.GetBrush("AccentBrush")
            : ThemeManager.GetBrush("TextSecondaryBrush");
        ThemeToggleButton.ToolTip = isLight ? "다크 모드로 전환" : "라이트 모드로 전환";
    }

    #region WebView2 Events

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsActiveWebView(sender))
        {
            return;
        }

        AddressInput.Text = e.Uri;
        StatusText.Text = $"불러오는 중: {TruncateUrl(e.Uri)}";
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!IsActiveWebView(sender))
        {
            return;
        }

        StatusText.Text = e.IsSuccess ? "준비됨" : $"오류: {e.WebErrorStatus}";
        _ = ApplyWebContentOpacityAsync();
        RaiseBrowserStateChanged();
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        var tab = GetTabFromCoreWebView(sender);
        if (tab == null)
        {
            return;
        }

        var docTitle = tab.WebView.CoreWebView2?.DocumentTitle ?? "";
        tab.Title = string.IsNullOrWhiteSpace(docTitle) ? "New tab" : docTitle;
        tab.TitleText.Text = TruncateUrl(tab.Title, 18);

        if (tab == _activeTab)
        {
            Title = string.IsNullOrEmpty(docTitle) ? "TrayWebApp" : docTitle;
            RaiseBrowserStateChanged();
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;

        if (OpenNewWindowsExternally)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to open popup in external browser", ex);
            }
            return;
        }

        CreateNewTab(e.Uri);
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (!IsActiveWebView(sender))
        {
            return;
        }

        var source = WebView.CoreWebView2?.Source;
        if (!string.IsNullOrWhiteSpace(source))
        {
            AddressInput.Text = source;
        }
        RaiseBrowserStateChanged();
    }

    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        if (sender is not WebView2 webView || _activeTab?.WebView != webView)
        {
            return;
        }

        _zoomFactor = WebView.ZoomFactor;
        ZoomText.Text = $"{(int)(_zoomFactor * 100)}%";
        RaiseBrowserStateChanged();
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        // Route downloads to configured folder
        var downloadFolder = App.SettingsStore.Settings.DownloadFolder;
        if (string.IsNullOrEmpty(downloadFolder))
            downloadFolder = AppPaths.DefaultDownloadsDirectory;

        try
        {
            Directory.CreateDirectory(downloadFolder);
            var fileName = System.IO.Path.GetFileName(e.ResultFilePath);
            var targetPath = MakeUniquePath(System.IO.Path.Combine(downloadFolder, fileName));
            e.ResultFilePath = targetPath;
            e.Handled = true;

            StatusText.Text = $"다운로드 중: {fileName}";

            e.DownloadOperation.StateChanged += (s2, e2) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var op = e.DownloadOperation;
                    switch (op.State)
                    {
                        case CoreWebView2DownloadState.InProgress:
                            var total = (long)(op.TotalBytesToReceive ?? 1);
                            var pct = total > 0 ? op.BytesReceived * 100 / total : 0;
                            StatusText.Text = $"다운로드 중: {fileName} ({pct}%)";
                            break;
                        case CoreWebView2DownloadState.Completed:
                            StatusText.Text = $"다운로드 완료: {fileName}";
                            break;
                        case CoreWebView2DownloadState.Interrupted:
                            StatusText.Text = $"다운로드 실패: {fileName}";
                            break;
                    }
                });
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Download] Error", ex);
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        // Map permission kind to human-readable name
        var permName = e.PermissionKind switch
        {
            CoreWebView2PermissionKind.Camera => "카메라",
            CoreWebView2PermissionKind.Microphone => "마이크",
            CoreWebView2PermissionKind.Geolocation => "위치",
            CoreWebView2PermissionKind.Notifications => "알림",
            CoreWebView2PermissionKind.ClipboardRead => "클립보드 읽기",
            _ => e.PermissionKind.ToString()
        };

        // Auto-allow notifications when enabled; prompt for everything else.
        if (e.PermissionKind == CoreWebView2PermissionKind.Notifications &&
            App.SettingsStore.Settings.AutoAllowNotifications)
        {
            e.State = CoreWebView2PermissionState.Allow;
            return;
        }

        var result = MessageBox.Show(
            $"{e.Uri}에서 다음 권한을 요청합니다.\n\n{permName}\n\n허용할까요?",
            "권한 요청 - TrayWebApp",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        e.State = result == MessageBoxResult.Yes
            ? CoreWebView2PermissionState.Allow
            : CoreWebView2PermissionState.Deny;
    }

    #endregion

    #region Title Bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click title bar to toggle maximize
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    #endregion

    #region Toolbar Buttons

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
            WebView.CoreWebView2.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
            WebView.CoreWebView2.GoForward();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.Reload();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(_homeUrl);
    }

    private void MobileViewButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobileView(!_isMobileView);
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleThemeMode();
    }

    private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCurrentInExternalBrowser();
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        CreateNewTab(_homeUrl);
    }

    private void ScrollTabsLeftButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollTabsBy(-140);
    }

    private void ScrollTabsRightButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollTabsBy(140);
    }

    private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollTabsBy(e.Delta > 0 ? -120 : 120);
        e.Handled = true;
    }

    private void ScrollTabsBy(double offset)
    {
        var targetOffset = Math.Clamp(
            TabScrollViewer.HorizontalOffset + offset,
            0,
            TabScrollViewer.ScrollableWidth);
        TabScrollViewer.ScrollToHorizontalOffset(targetOffset);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressOpacitySliderEvent)
        {
            return;
        }

        var opacity = Math.Round(e.NewValue, 2);
        SetVisualOpacity(opacity);
        App.SettingsStore.Update(s => s.WindowOpacity = opacity);
    }

    private void OpacitySlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        if (IsFromSliderThumb(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var clickX = e.GetPosition(slider).X;
        var ratio = slider.ActualWidth <= 0 ? 0 : Math.Clamp(clickX / slider.ActualWidth, 0, 1);
        var value = slider.Minimum + ((slider.Maximum - slider.Minimum) * ratio);

        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
        {
            value = Math.Round(value / slider.TickFrequency) * slider.TickFrequency;
        }

        value = Math.Round(Math.Clamp(value, slider.Minimum, slider.Maximum), 2);
        SetVisualOpacity(value);
        App.SettingsStore.Update(settings => settings.WindowOpacity = value);

        slider.Focus();
        e.Handled = true;
    }

    private static bool IsFromSliderThumb(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Thumb)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        SetAlwaysOnTop(!_isAlwaysOnTop);
        AlwaysOnTopChanged?.Invoke(this, new AlwaysOnTopChangedEventArgs
        {
            IsAlwaysOnTop = _isAlwaysOnTop
        });
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Address Bar

    private void AddressInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(AddressInput.Text);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AddressInput.Text = CurrentUrl ?? "";
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    #endregion

    #region Keyboard Shortcuts

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            WebView.CoreWebView2?.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.F12)
        {
            WebView.CoreWebView2?.OpenDevToolsWindow();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
        {
            if (WebView.CoreWebView2?.CanGoBack == true)
                WebView.CoreWebView2.GoBack();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right)
        {
            if (WebView.CoreWebView2?.CanGoForward == true)
                WebView.CoreWebView2.GoForward();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.T:
                    CreateNewTab(_homeUrl);
                    e.Handled = true;
                    break;
                case Key.W:
                    if (_activeTab != null)
                    {
                        CloseTab(_activeTab);
                    }
                    e.Handled = true;
                    break;
                case Key.Tab:
                    ActivateNextTab(reverse: false);
                    e.Handled = true;
                    break;
                case Key.L:
                    AddressInput.Focus();
                    AddressInput.SelectAll();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    WebView.ZoomFactor = Math.Min(WebView.ZoomFactor + 0.1, 3.0);
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    WebView.ZoomFactor = Math.Max(WebView.ZoomFactor - 0.1, 0.25);
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    WebView.ZoomFactor = 1.0;
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Tab)
        {
            ActivateNextTab(reverse: true);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T)
        {
            ReopenClosedTab();
            e.Handled = true;
        }
    }

    #endregion

    private static string TruncateUrl(string url, int maxLength = 50)
    {
        if (url.Length <= maxLength) return url;
        return url[..maxLength] + "...";
    }

    private void ActivateTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab))
        {
            return;
        }

        _activeTab = tab;
        WebViewHost.Content = tab.WebView;
        RefreshActiveTabUi();
    }

    private void CloseTab(BrowserTab tab)
    {
        CloseTab(tab, rememberClosed: true);
    }

    private void CloseTab(BrowserTab tab, bool rememberClosed)
    {
        if (!_tabs.Contains(tab))
        {
            return;
        }

        var closedIndex = _tabs.IndexOf(tab);
        var wasActive = tab == _activeTab;
        if (rememberClosed)
        {
            RememberClosedTab(tab);
        }
        tab.IsClosed = true;

        TabsHost.Children.Remove(tab.HeaderButton);
        _tabs.Remove(tab);

        if (WebViewHost.Content == tab.WebView)
        {
            WebViewHost.Content = null;
        }

        try
        {
            tab.WebView.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to dispose closed tab WebView: {ex.Message}");
        }

        if (_tabs.Count == 0)
        {
            CreateNewTab(_homeUrl);
            return;
        }

        if (wasActive)
        {
            var nextIndex = Math.Min(closedIndex, _tabs.Count - 1);
            ActivateTab(_tabs[nextIndex]);
        }
        else
        {
            RefreshActiveTabUi();
        }
    }

    private void RememberClosedTab(BrowserTab tab)
    {
        var url = GetPersistableTabUrl(tab);
        if (!IsHttpUrl(url))
        {
            return;
        }

        _closedTabs.Push(new ClosedTabState
        {
            Url = url,
            Title = string.IsNullOrWhiteSpace(tab.Title) ? null : tab.Title
        });

        while (_closedTabs.Count > 10)
        {
            var retained = _closedTabs.Take(10).Reverse().ToArray();
            _closedTabs.Clear();
            foreach (var closedTab in retained)
            {
                _closedTabs.Push(closedTab);
            }
        }
    }

    private void DuplicateTab(BrowserTab tab)
    {
        var newTab = CreateNewTab(GetPersistableTabUrl(tab));
        newTab.Title = tab.Title;
        newTab.TitleText.Text = TruncateUrl(tab.Title, 18);
    }

    private void CloseOtherTabs(BrowserTab tab)
    {
        foreach (var otherTab in _tabs.Where(candidate => candidate != tab).ToList())
        {
            CloseTab(otherTab);
        }

        ActivateTab(tab);
    }

    private void CloseTabsToRight(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        foreach (var rightTab in _tabs.Skip(index + 1).ToList())
        {
            CloseTab(rightTab);
        }
    }

    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return;
        }

        var closedTab = _closedTabs.Pop();
        var tab = CreateNewTab(closedTab.Url);
        if (!string.IsNullOrWhiteSpace(closedTab.Title))
        {
            tab.Title = closedTab.Title;
            tab.TitleText.Text = TruncateUrl(closedTab.Title, 18);
        }
    }

    private void ActivateNextTab(bool reverse)
    {
        if (_tabs.Count <= 1 || _activeTab == null)
        {
            return;
        }

        var currentIndex = _tabs.IndexOf(_activeTab);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = reverse
            ? (currentIndex - 1 + _tabs.Count) % _tabs.Count
            : (currentIndex + 1) % _tabs.Count;
        ActivateTab(_tabs[nextIndex]);
    }

    private void RefreshActiveTabUi()
    {
        foreach (var tab in _tabs)
        {
            var isActive = tab == _activeTab;
            tab.HeaderButton.Background = isActive
                ? ThemeManager.GetBrush("SurfaceBrush")
                : ThemeManager.GetBrush("SurfaceAltBrush");
            tab.HeaderButton.Foreground = isActive
                ? ThemeManager.GetBrush("TextPrimaryBrush")
                : ThemeManager.GetBrush("TextSecondaryBrush");
            tab.HeaderButton.BorderBrush = isActive
                ? ThemeManager.GetBrush("AccentBrush")
                : ThemeManager.GetBrush("BorderBrush");
        }

        if (_activeTab == null)
        {
            return;
        }

        AddressInput.Text = _activeTab.WebView.CoreWebView2?.Source ?? _activeTab.PendingUrl ?? "";
        ZoomText.Text = $"{(int)(_activeTab.WebView.ZoomFactor * 100)}%";
        Title = string.IsNullOrWhiteSpace(_activeTab.Title) ? "TrayWebApp" : _activeTab.Title;
        UpdateMobileViewButton();
        UpdateThemeToggleButton();
        _activeTab.HeaderButton.Dispatcher.BeginInvoke(() => _activeTab?.HeaderButton.BringIntoView());
        _ = ApplyWebContentOpacityAsync(_activeTab);
        RaiseBrowserStateChanged();
    }

    private BrowserTab? GetTabFromCoreWebView(object? sender)
    {
        return sender is CoreWebView2 coreWebView
            ? _tabs.FirstOrDefault(tab => tab.WebView.CoreWebView2 == coreWebView)
            : null;
    }

    private bool IsActiveWebView(object? sender)
    {
        return GetTabFromCoreWebView(sender) == _activeTab;
    }

    private IReadOnlyList<WebAppTabState> GetCurrentTabs()
    {
        return _tabs
            .Select(tab => new WebAppTabState
            {
                Title = string.IsNullOrWhiteSpace(tab.Title) ? null : tab.Title,
                Url = GetPersistableTabUrl(tab)
            })
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Url))
            .Take(5)
            .ToList();
    }

    private string GetPersistableTabUrl(BrowserTab tab)
    {
        var source = tab.WebView.CoreWebView2?.Source;
        if (IsHttpUrl(source))
        {
            return source!;
        }

        if (IsHttpUrl(tab.PendingUrl))
        {
            return tab.PendingUrl!;
        }

        return _homeUrl;
    }

    private static bool IsHttpUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
            (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private void RaiseBrowserStateChanged()
    {
        BrowserStateChanged?.Invoke(this, new BrowserStateChangedEventArgs
        {
            Title = CurrentTitle,
            Url = CurrentUrl,
            ZoomFactor = CurrentZoomFactor,
            Tabs = CurrentTabs,
            ActiveTabIndex = CurrentActiveTabIndex
        });
    }

    private void UpdateOpacitySlider(double opacity)
    {
        var percent = (int)Math.Round(opacity * 100);
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{percent}%";
        }

        if (OpacitySlider == null)
        {
            return;
        }

        if (Math.Abs(OpacitySlider.Value - opacity) < 0.001)
        {
            return;
        }

        _suppressOpacitySliderEvent = true;
        OpacitySlider.Value = opacity;
        _suppressOpacitySliderEvent = false;
    }

    private void ApplyNativeTopMost(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var insertAfter = enabled ? HwndTopMost : HwndNoTopMost;
        var flags = SwpNoMove | SwpNoSize | SwpShowWindow;
        if (!enabled)
        {
            flags |= SwpNoActivate;
        }

        if (!SetWindowPos(handle, insertAfter, 0, 0, 0, 0, flags))
        {
            var error = Marshal.GetLastWin32Error();
            AppLogger.Warn($"SetWindowPos failed while applying AlwaysOnTop={enabled}. Win32Error={error}");
        }
    }

    private async Task ApplyWebContentOpacityAsync()
    {
        if (_activeTab == null)
        {
            return;
        }

        await ApplyWebContentOpacityAsync(_activeTab);
    }

    private async Task ApplyWebContentOpacityAsync(BrowserTab tab)
    {
        if (!tab.IsInitialized || tab.WebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await tab.WebView.CoreWebView2.ExecuteScriptAsync(BuildOpacityScript());
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to apply web content opacity: {ex.Message}");
        }
    }

    private string BuildOpacityScript()
    {
        var opacity = _visualOpacity.ToString("0.##", CultureInfo.InvariantCulture);

        return $$"""
            (() => {
                const opacity = {{opacity}};
                const apply = () => {
                    const root = document.documentElement;
                    if (!root) return;

                    root.style.setProperty('background', 'transparent', 'important');
                    root.style.setProperty('opacity', String(opacity), 'important');
                    root.style.setProperty('transition', 'opacity 80ms linear', 'important');

                    if (document.body) {
                        document.body.style.setProperty('background', 'transparent', 'important');
                    }
                };

                apply();

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', apply, { once: true });
                }
            })();
            """;
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();

        if (url.Contains(' ') && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{extension}");
    }
}
