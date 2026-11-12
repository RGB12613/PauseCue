using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PauseCue;

public sealed class TrayIcon : IDisposable
{
    private readonly ControlWindow _window;
    private readonly Action _shutdown;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _startupItem;

    public TrayIcon(ControlWindow window, Action shutdown)
    {
        _window = window;
        _shutdown = shutdown;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Pause Cue を開く", null, (_, _) => ShowWindow());
        menu.Items.Add("\u4ECA\u304B\u3089\u4F11\u61A9\u3059\u308B", null, (_, _) => _window.Dispatcher.Invoke(() => _window.TakeBreakForTray()));
        menu.Items.Add("30 分一時停止", null, (_, _) => _window.Dispatcher.Invoke(() => _window.PauseForTray(30)));
        _startupItem = new Forms.ToolStripMenuItem("Windows 起動時に開始") { CheckOnClick = true, Checked = StartupRegistrar.IsEnabled };
        _startupItem.CheckedChanged += (_, _) => SetStartup(_startupItem.Checked);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => Exit());
        _icon = new Forms.NotifyIcon
        {
            Text = "Pause Cue — 休憩を促しています",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow() => _window.Dispatcher.Invoke(() => { _window.Show(); _window.WindowState = WindowState.Normal; _window.Activate(); });
    private void Exit()
    {
        _window.Dispatcher.Invoke(() => _window.ExitApplication());
        _icon.Visible = false;
        _shutdown();
    }
    private void SetStartup(bool enabled)
    {
        try { StartupRegistrar.SetEnabled(enabled); }
        catch
        {
            _startupItem.CheckedChanged -= (_, _) => SetStartup(_startupItem.Checked);
            _startupItem.Checked = StartupRegistrar.IsEnabled;
        }
    }
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
