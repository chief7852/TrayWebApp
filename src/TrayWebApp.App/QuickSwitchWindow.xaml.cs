using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TrayWebApp.Core.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TrayWebApp.App;

public partial class QuickSwitchWindow : Window
{
    private readonly IReadOnlyList<WebAppItem> _apps;
    private readonly ObservableCollection<WebAppItem> _filteredApps = new();

    public WebAppItem? SelectedApp { get; private set; }

    public QuickSwitchWindow(IReadOnlyList<WebAppItem> apps)
    {
        InitializeComponent();
        _apps = apps;
        ResultsList.ItemsSource = _filteredApps;
        RefreshResults();
        Loaded += (s, e) =>
        {
            SearchInput.Focus();
            SearchInput.SelectAll();
        };
    }

    private void SearchInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshResults();
    }

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                AcceptSelected();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AcceptSelected();
            e.Handled = true;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AcceptSelected();
    }

    private void RefreshResults()
    {
        var query = SearchInput.Text.Trim();
        var results = string.IsNullOrWhiteSpace(query)
            ? _apps
            : _apps.Where(app =>
                app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                app.Url.Contains(query, StringComparison.OrdinalIgnoreCase));

        _filteredApps.Clear();
        foreach (var app in results.Take(30))
        {
            _filteredApps.Add(app);
        }

        if (_filteredApps.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_filteredApps.Count == 0)
        {
            return;
        }

        var next = ResultsList.SelectedIndex < 0
            ? 0
            : Math.Clamp(ResultsList.SelectedIndex + delta, 0, _filteredApps.Count - 1);
        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void AcceptSelected()
    {
        if (ResultsList.SelectedItem is not WebAppItem app)
        {
            return;
        }

        SelectedApp = app;
        DialogResult = true;
        Close();
    }
}
