using System.Windows;
using System.Windows.Threading;
using TrayWebApp.Core.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TrayWebApp.App;

/// <summary>
/// Application entry point. Sets up tray service and manages lifecycle.
/// </summary>
public partial class App : Application
{
    private System.Threading.Mutex? _singleInstanceMutex;
    private TrayService? _trayService;
    private SettingsStore? _settingsStore;
    private WebAppStore? _webAppStore;

    public static SettingsStore SettingsStore { get; private set; } = null!;
    public static WebAppStore WebAppStore { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterExceptionHandlers();

        // Ensure single instance
        _singleInstanceMutex = new System.Threading.Mutex(true, "TrayWebApp_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("TrayWebApp이 이미 실행 중입니다.", "TrayWebApp",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        // Initialize stores
        _settingsStore = new SettingsStore();
        _webAppStore = new WebAppStore();
        SettingsStore = _settingsStore;
        WebAppStore = _webAppStore;

        // Initialize tray service
        _trayService = new TrayService(_settingsStore, _webAppStore);
        _trayService.Initialize();
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppLogger.Error("Unhandled application exception", exception);
            }
            else
            {
                AppLogger.Error($"Unhandled application exception: {args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            "예상하지 못한 오류가 발생했습니다. 앱은 계속 실행되며, 자세한 내용은 로그 파일에 기록했습니다.\n\n" +
            AppPaths.LogFilePath,
            "TrayWebApp 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
            // The mutex may not have been acquired if startup exited early.
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
