using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TrayWebApp.Core.Models;
using TrayWebApp.Core.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace TrayWebApp.App;

/// <summary>
/// Management window for adding, editing, deleting, and reordering web apps.
/// </summary>
public partial class ManageAppsWindow : Window
{
    private readonly WebAppStore _webAppStore;
    private readonly ObservableCollection<WebAppItem> _apps;
    private readonly ObservableCollection<WebAppItem> _visibleApps;
    private WebAppItem? _editingApp;
    private bool _isNewMode;

    /// <summary>Raised when the app list changes so TrayService can rebuild its menu</summary>
    public event Action? AppsChanged;

    public ManageAppsWindow(WebAppStore webAppStore)
    {
        InitializeComponent();
        _webAppStore = webAppStore;
        _apps = new ObservableCollection<WebAppItem>(_webAppStore.Apps);
        _visibleApps = new ObservableCollection<WebAppItem>(_apps);
        AppListBox.ItemsSource = _visibleApps;

        if (_visibleApps.Count > 0)
            AppListBox.SelectedIndex = 0;
        else
            ClearForm();
    }

    #region Title Bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Search

    private void SearchInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshVisibleApps(_editingApp);
    }

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchInput.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _visibleApps.Count > 0)
        {
            AppListBox.Focus();
            AppListBox.SelectedIndex = Math.Max(0, AppListBox.SelectedIndex);
            e.Handled = true;
        }
    }

    private void RefreshVisibleApps(WebAppItem? preferredSelection = null)
    {
        var query = SearchInput.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _apps
            : _apps.Where(app =>
                app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                app.Url.Contains(query, StringComparison.OrdinalIgnoreCase));

        _visibleApps.Clear();
        foreach (var app in filtered)
        {
            _visibleApps.Add(app);
        }

        if (_visibleApps.Count == 0)
        {
            AppListBox.SelectedItem = null;
            ClearForm();
            return;
        }

        AppListBox.SelectedItem = preferredSelection != null && _visibleApps.Contains(preferredSelection)
            ? preferredSelection
            : _visibleApps[0];
    }

    #endregion

    #region List Selection

    private void AppListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AppListBox.SelectedItem is WebAppItem app)
        {
            LoadAppToForm(app);
            _editingApp = app;
            _isNewMode = false;
            DeleteButton.Visibility = Visibility.Visible;
            ClearSessionButton.Visibility = app.UseIsolatedSession ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Content = "저장";
        }
    }

    #endregion

    #region Form

    private void LoadAppToForm(WebAppItem app)
    {
        NameInput.Text = app.Name;
        UrlInput.Text = app.Url;
        WidthInput.Text = app.Width.ToString();
        HeightInput.Text = app.Height.ToString();
        AlwaysOnTopCheck.IsChecked = app.AlwaysOnTop;
        IsolatedSessionCheck.IsChecked = app.UseIsolatedSession;
        UserAgentInput.Text = app.UserAgent;
    }

    private void ClearForm()
    {
        NameInput.Text = "";
        UrlInput.Text = "https://";
        WidthInput.Text = "430";
        HeightInput.Text = "720";
        AlwaysOnTopCheck.IsChecked = true;
        IsolatedSessionCheck.IsChecked = false;
        UserAgentInput.Text = "desktop";
        _editingApp = null;
        _isNewMode = true;
        DeleteButton.Visibility = Visibility.Collapsed;
        ClearSessionButton.Visibility = Visibility.Collapsed;
        SaveButton.Content = "추가";
    }

    #endregion

    #region Buttons

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        AppListBox.SelectedItem = null;
        ClearForm();
        NameInput.Focus();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        var name = NameInput.Text.Trim();
        var url = UrlInput.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("이름을 입력하세요.", "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameInput.Focus();
            return;
        }

        if (string.IsNullOrEmpty(url) || url == "https://")
        {
            MessageBox.Show("URL을 입력하세요.", "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            UrlInput.Focus();
            return;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;

        int.TryParse(WidthInput.Text, out int width);
        int.TryParse(HeightInput.Text, out int height);

        if (_isNewMode)
        {
            // Add new app
            var newApp = new WebAppItem
            {
                Name = name,
                Url = url,
                Width = width > 0 ? width : 430,
                Height = height > 0 ? height : 720,
                AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true,
                UseIsolatedSession = IsolatedSessionCheck.IsChecked == true,
                UserAgent = UserAgentInput.Text.Trim()
            };
            _webAppStore.Add(newApp);
            _apps.Add(newApp);
            RefreshVisibleApps(newApp);
            AppListBox.SelectedItem = newApp;
            await RefreshFaviconAsync(newApp);
        }
        else if (_editingApp != null)
        {
            // Update existing app
            _editingApp.Name = name;
            _editingApp.Url = url;
            _editingApp.Width = width > 0 ? width : 0;
            _editingApp.Height = height > 0 ? height : 0;
            _editingApp.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
            _editingApp.UseIsolatedSession = IsolatedSessionCheck.IsChecked == true;
            _editingApp.UserAgent = UserAgentInput.Text.Trim();
            _webAppStore.Update(_editingApp);

            // Refresh list display
            var index = _apps.IndexOf(_editingApp);
            if (index >= 0)
            {
                _apps[index] = _editingApp;
            }
            RefreshVisibleApps(_editingApp);
            ClearSessionButton.Visibility = _editingApp.UseIsolatedSession ? Visibility.Visible : Visibility.Collapsed;
            await RefreshFaviconAsync(_editingApp);
        }

        AppsChanged?.Invoke();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingApp == null) return;

        var result = MessageBox.Show(
            $"\"{_editingApp.Name}\" 앱을 삭제할까요?",
            "삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _webAppStore.Remove(_editingApp.Id);
            _apps.Remove(_editingApp);
            AppsChanged?.Invoke();
            RefreshVisibleApps();
        }
    }

    private void ClearSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingApp == null || !_editingApp.UseIsolatedSession)
        {
            return;
        }

        var result = MessageBox.Show(
            $"\"{_editingApp.Name}\" 앱의 독립 세션 데이터를 삭제할까요?\n\n열려 있는 해당 앱 창을 먼저 닫아야 완전히 삭제됩니다.",
            "세션 초기화",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var profilePath = GetIsolatedSessionPath(_editingApp);
        try
        {
            if (Directory.Exists(profilePath))
            {
                Directory.Delete(profilePath, recursive: true);
            }

            MessageBox.Show("독립 세션 데이터를 삭제했습니다.", "세션 초기화", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"세션 데이터를 삭제하지 못했습니다.\n\n앱 창이 열려 있다면 닫은 뒤 다시 시도하세요.\n\n{ex.Message}",
                "세션 초기화 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppListBox.SelectedItem is not WebAppItem item) return;

        var index = _apps.IndexOf(item);
        if (index <= 0) return;

        _apps.RemoveAt(index);
        _apps.Insert(index - 1, item);

        ReorderAndSave();
        RefreshVisibleApps(item);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppListBox.SelectedItem is not WebAppItem item) return;

        var index = _apps.IndexOf(item);
        if (index < 0 || index >= _apps.Count - 1) return;

        _apps.RemoveAt(index);
        _apps.Insert(index + 1, item);

        ReorderAndSave();
        RefreshVisibleApps(item);
    }

    private void ReorderAndSave()
    {
        for (int i = 0; i < _apps.Count; i++)
        {
            _apps[i].Order = i;
        }
        _webAppStore.ReplaceAll(_apps);
        AppsChanged?.Invoke();
    }

    private async Task RefreshFaviconAsync(WebAppItem app)
    {
        var iconPath = await FaviconService.RefreshAsync(app);
        if (!string.IsNullOrEmpty(iconPath))
        {
            app.IconPath = iconPath;
            _webAppStore.SetIconPath(app.Id, iconPath);
        }
    }

    private static string GetIsolatedSessionPath(WebAppItem app)
    {
        var safeId = new string(app.Id
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = app.Id;
        }

        return Path.Combine(AppPaths.DataDirectory, "WebView2Profiles", safeId);
    }

    #endregion
}
