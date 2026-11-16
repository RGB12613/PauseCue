using System.Windows;

namespace PauseCue;

public partial class App
{
    private TrayIcon? _trayIcon;
    private ControlWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = _window = new ControlWindow();
        _trayIcon = new TrayIcon(window, Shutdown);
        window.Show();
        window.StartBackgroundUpdateChecks();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Writes the running session to the work log (no-op if already done by the tray "終了").
        _window?.ExitApplication();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
