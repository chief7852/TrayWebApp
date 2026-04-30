using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TrayWebApp.Core.Services;
using MessageBox = System.Windows.MessageBox;

namespace TrayWebApp.App;

/// <summary>
/// Application settings window with toggle controls, data management, and shortcut reference.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private bool _loading = true;

    public SettingsWindow(SettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        LoadSettings();
        _loading = false;
    }

    private void LoadSettings()
    {
        var s = _settingsStore.Settings;
        RunAtStartupCheck.IsChecked = StartupService.IsRegistered();
        AlwaysOnTopCheck.IsChecked = s.AlwaysOnTop;
        HideOnCloseCheck.IsChecked = s.HideOnClose;
        HideOnDeactivateCheck.IsChecked = s.HideOnDeactivate;
        ShowAddressBarCheck.IsChecked = s.ShowAddressBar;
        OpenPopupsExternallyCheck.IsChecked = s.OpenNewWindowsExternally;
        AutoAllowNotificationsCheck.IsChecked = s.AutoAllowNotifications;
        DefaultWidthInput.Text = s.WindowWidth.ToString();
        DefaultHeightInput.Text = s.WindowHeight.ToString();
        OpacitySlider.Value = s.WindowOpacity;
        OpacityLabel.Text = $"{(int)(s.WindowOpacity * 100)}%";

        var downloadPath = s.DownloadFolder;
        if (string.IsNullOrEmpty(downloadPath))
            downloadPath = AppPaths.DefaultDownloadsDirectory;
        DownloadFolderInput.Text = downloadPath;
    }

    #region Title Bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Save size settings on close
        if (int.TryParse(DefaultWidthInput.Text, out int w) && w > 0)
            _settingsStore.Settings.WindowWidth = w;
        if (int.TryParse(DefaultHeightInput.Text, out int h) && h > 0)
            _settingsStore.Settings.WindowHeight = h;
        _settingsStore.Save();
        Close();
    }

    #endregion

    #region Toggle Handlers

    private void RunAtStartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (RunAtStartupCheck.IsChecked == true)
            StartupService.Register();
        else
            StartupService.Unregister();
        _settingsStore.Update(s => s.RunAtStartup = RunAtStartupCheck.IsChecked == true);
    }

    private void AlwaysOnTopCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true);
    }

    private void HideOnCloseCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.HideOnClose = HideOnCloseCheck.IsChecked == true);
    }

    private void HideOnDeactivateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.HideOnDeactivate = HideOnDeactivateCheck.IsChecked == true);
    }

    private void ShowAddressBarCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.ShowAddressBar = ShowAddressBarCheck.IsChecked == true);
    }

    private void OpenPopupsExternallyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.OpenNewWindowsExternally = OpenPopupsExternallyCheck.IsChecked == true);
    }

    private void AutoAllowNotificationsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settingsStore.Update(s => s.AutoAllowNotifications = AutoAllowNotificationsCheck.IsChecked == true);
    }

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = Math.Round(OpacitySlider.Value, 1);
        OpacityLabel.Text = $"{(int)(value * 100)}%";
        _settingsStore.Update(s => s.WindowOpacity = value);
    }

    #endregion

    #region Downloads

    private void BrowseDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "다운로드 폴더 선택",
            ShowNewFolderButton = true,
            SelectedPath = DownloadFolderInput.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            DownloadFolderInput.Text = dialog.SelectedPath;
            _settingsStore.Update(s => s.DownloadFolder = dialog.SelectedPath);
        }
    }

    #endregion

    #region Data Management

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "WebView 브라우징 캐시를 삭제할까요?\n\n쿠키와 로그인 세션은 유지됩니다.",
            "캐시 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var cachePath = Path.Combine(
                    AppPaths.WebViewDataDirectory, "EBWebView", "Default", "Cache");

                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    MessageBox.Show("캐시를 삭제했습니다. 변경 사항은 TrayWebApp 재시작 후 적용됩니다.",
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("삭제할 캐시가 없습니다.", "정보", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"캐시 삭제에 실패했습니다.\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "모든 설정을 기본값으로 되돌릴까요?\n\n웹앱 목록은 유지됩니다.",
            "설정 초기화",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _settingsStore.Update(s =>
            {
                var defaults = new Core.Models.AppSettings();
                s.DefaultUrl = defaults.DefaultUrl;
                s.WindowWidth = defaults.WindowWidth;
                s.WindowHeight = defaults.WindowHeight;
                s.WindowX = defaults.WindowX;
                s.WindowY = defaults.WindowY;
                s.AlwaysOnTop = defaults.AlwaysOnTop;
                s.RunAtStartup = defaults.RunAtStartup;
                s.HideOnClose = defaults.HideOnClose;
                s.HideOnDeactivate = defaults.HideOnDeactivate;
                s.WindowOpacity = defaults.WindowOpacity;
                s.OpenNewWindowsExternally = defaults.OpenNewWindowsExternally;
                s.AutoAllowNotifications = defaults.AutoAllowNotifications;
                s.ShowAddressBar = defaults.ShowAddressBar;
            });
            LoadSettings();
            _loading = false;
            MessageBox.Show("설정을 기본값으로 되돌렸습니다.", "완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        var dataDir = AppPaths.DataDirectory;
        if (Directory.Exists(dataDir))
            Process.Start("explorer.exe", dataDir);
    }

    #endregion
}
