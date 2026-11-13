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
            Icon = LoadAppIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowWindow();
        _icon.BalloonTipClicked += (_, _) => ShowWindow();
        _window.StatusChanged += SetStatus;
        _window.UpdateAvailable += ShowUpdateNotice;
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PauseCue.ico"));
            if (resource is not null)
                using (var stream = resource.Stream) return new Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch
        {
            // Fall back to the stock icon if the resource cannot be read.
        }
        return SystemIcons.Information;
    }

    private void SetStatus(string status)
    {
        // NotifyIcon.Text is limited to 127 characters.
        var text = $"Pause Cue — {status}";
        _icon.Text = text.Length > 127 ? text[..127] : text;
    }

    private void ShowUpdateNotice(string tag)
    {
        _icon.ShowBalloonTip(8000, "Pause Cue の更新", $"新しいバージョン {tag} があります。画面の「{tag} に更新」から更新できます。", Forms.ToolTipIcon.Info);
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
    public void Dispose()
    {
        _window.StatusChanged -= SetStatus;
        _window.UpdateAvailable -= ShowUpdateNotice;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
