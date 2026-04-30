using System.Windows;

namespace TrayWebApp.App;

/// <summary>
/// Hidden window used solely as a message pump for global hotkey registration.
/// </summary>
public partial class HiddenWindow : Window
{
    public HiddenWindow()
    {
        InitializeComponent();
    }
}
