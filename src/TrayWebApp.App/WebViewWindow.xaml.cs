using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
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
}

public sealed class AlwaysOnTopChangedEventArgs : EventArgs
{
    public bool IsAlwaysOnTop { get; init; }
}

/// <summary>
/// WebView2-based floating browser window with custom chrome.
/// </summary>
public partial class WebViewWindow : Window
{
    private bool _webViewInitialized;
    private string _pendingUrl = "";
    private string? _pendingUserAgent;
    private double? _pendingZoomFactor;
    private string _homeUrl = "https://www.google.com";
    private double _zoomFactor = 1.0;
    private double _visualOpacity = 1.0;
    private bool _suppressOpacitySliderEvent;
    private bool _isAlwaysOnTop;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

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

    public string? CurrentUrl => WebView.CoreWebView2?.Source;
    public string? CurrentTitle => WebView.CoreWebView2?.DocumentTitle;
    public double CurrentZoomFactor => WebView.ZoomFactor;
    public bool IsAlwaysOnTop => _isAlwaysOnTop;
    public bool OpenNewWindowsExternally { get; set; }

    public WebViewWindow()
    {
        InitializeComponent();
        OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
        UpdateOpacitySlider(_visualOpacity);
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        try
        {
            // Use a persistent user data folder so cookies/sessions survive restarts
            AppPaths.EnsureDirectories();
            var userDataFolder = AppPaths.WebViewDataDirectory;

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            await WebView.EnsureCoreWebView2Async(env);
            _webViewInitialized = true;

            // Configure WebView2 settings
            var settings = WebView.CoreWebView2.Settings;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsZoomControlEnabled = true;
            settings.AreDevToolsEnabled = true;

            // Event handlers
            WebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            WebView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
            WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            WebView.CoreWebView2.SourceChanged += OnSourceChanged;
            WebView.ZoomFactorChanged += OnZoomFactorChanged;
            WebView.CoreWebView2.DownloadStarting += OnDownloadStarting;
            WebView.CoreWebView2.PermissionRequested += OnPermissionRequested;

            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildOpacityScript());

            // Apply pending User-Agent if set
            if (!string.IsNullOrEmpty(_pendingUserAgent))
            {
                WebView.CoreWebView2.Settings.UserAgent = _pendingUserAgent;
                _pendingUserAgent = null;
            }

            if (_pendingZoomFactor.HasValue)
            {
                SetZoomFactor(_pendingZoomFactor.Value);
                _pendingZoomFactor = null;
            }

            // Navigate to pending URL if any
            if (!string.IsNullOrEmpty(_pendingUrl))
            {
                WebView.CoreWebView2.Navigate(_pendingUrl);
                _pendingUrl = "";
            }

            await ApplyWebContentOpacityAsync();

            StatusText.Text = "준비됨";
        }
        catch (Exception ex)
        {
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

        if (_webViewInitialized && WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.Navigate(url);
        }
        else
        {
            _pendingUrl = url;
        }
    }

    public void SetHomeUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _homeUrl = NormalizeUrl(url);
        }
    }

    public void SetZoomFactor(double zoomFactor)
    {
        zoomFactor = Math.Clamp(zoomFactor, 0.25, 3.0);

        if (_webViewInitialized)
        {
            WebView.ZoomFactor = zoomFactor;
        }
        else
        {
            _pendingZoomFactor = zoomFactor;
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
        if (_webViewInitialized && WebView.CoreWebView2 != null)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                // Reset to default by setting empty string
                // WebView2 treats empty as "use default"
                WebView.CoreWebView2.Settings.UserAgent = "";
            }
            else
            {
                WebView.CoreWebView2.Settings.UserAgent = userAgent;
            }
        }
        else
        {
            _pendingUserAgent = userAgent;
        }
    }

    #region WebView2 Events

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        AddressInput.Text = e.Uri;
        StatusText.Text = $"불러오는 중: {TruncateUrl(e.Uri)}";
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        StatusText.Text = e.IsSuccess ? "준비됨" : $"오류: {e.WebErrorStatus}";
        _ = ApplyWebContentOpacityAsync();
        RaiseBrowserStateChanged();
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        var docTitle = WebView.CoreWebView2?.DocumentTitle ?? "";
        Title = string.IsNullOrEmpty(docTitle) ? "TrayWebApp" : docTitle;
        RaiseBrowserStateChanged();
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

        WebView.CoreWebView2.Navigate(e.Uri);
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        var source = WebView.CoreWebView2?.Source;
        if (!string.IsNullOrWhiteSpace(source))
        {
            AddressInput.Text = source;
        }
        RaiseBrowserStateChanged();
    }

    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
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

    private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCurrentInExternalBrowser();
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
    }

    #endregion

    private static string TruncateUrl(string url, int maxLength = 50)
    {
        if (url.Length <= maxLength) return url;
        return url[..maxLength] + "...";
    }

    private void RaiseBrowserStateChanged()
    {
        BrowserStateChanged?.Invoke(this, new BrowserStateChangedEventArgs
        {
            Title = CurrentTitle,
            Url = CurrentUrl,
            ZoomFactor = CurrentZoomFactor
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
        if (!_webViewInitialized || WebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await WebView.CoreWebView2.ExecuteScriptAsync(BuildOpacityScript());
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
