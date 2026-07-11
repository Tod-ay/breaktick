using System.Windows;

namespace BreakTick.App;

public partial class App : System.Windows.Application
{
    private BreakCoordinator? _coordinator;
    private TrayService? _tray;
    private MainWindow? _mainWindow;
    private BreakOverlay? _overlay;
    private GlobalHotkeyService? _hotkey;
    private SessionMonitor? _sessionMonitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsStore = new SettingsStore();
        var database = new AppDatabase();
        _coordinator = new BreakCoordinator(settingsStore, database);
        _mainWindow = new MainWindow(_coordinator, settingsStore);
        _overlay = new BreakOverlay(_coordinator);
        _tray = new TrayService(_coordinator, ShowDashboard, Shutdown);
        _hotkey = new GlobalHotkeyService(_coordinator.TogglePause);
        _sessionMonitor = new SessionMonitor();
        _sessionMonitor.UnlockedAfterLock += (_, _) => Dispatcher.BeginInvoke(_coordinator.HandleSessionUnlock);

        _coordinator.BreakStarted += (_, _) => _overlay.ShowForBreak();
        _coordinator.ReturnRequested += (_, _) => _overlay.ShowReturnPrompt();
        _coordinator.WorkStarted += (_, _) => _overlay.Hide();
        _coordinator.StateChanged += (_, _) => _tray.Refresh();
        _coordinator.Start();
    }

    private void ShowDashboard()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlay?.Close();
        _sessionMonitor?.Dispose();
        _hotkey?.Dispose();
        _coordinator?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
