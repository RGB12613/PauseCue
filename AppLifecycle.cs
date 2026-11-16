using System.Threading;
using System.Windows;

namespace PauseCue;

public partial class App
{
    private TrayIcon? _trayIcon;
    private ControlWindow? _window;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;

    // "Local\" scopes the lock to the current Windows session (per user), so another user's
    // Pause Cue on the same PC is unaffected.
    private const string MutexName = @"Local\PauseCue.SingleInstance";
    private const string ShowSignalName = @"Local\PauseCue.ShowWindow";

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Another Pause Cue is already running: ask it to show its window, then quit.
            try { EventWaitHandle.OpenExisting(ShowSignalName).Set(); }
            catch { /* The running instance may still be starting up; nothing more to do. */ }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.BeginInvoke(() => _window?.BringToFront());
        }) { IsBackground = true, Name = "PauseCue show-window listener" };
        listener.Start();

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
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
