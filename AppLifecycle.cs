using System.Windows;

namespace PauseCue;

public partial class App
{
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new ControlWindow();
        _trayIcon = new TrayIcon(window, Shutdown);
        window.Show();
        window.StartBackgroundUpdateChecks();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
