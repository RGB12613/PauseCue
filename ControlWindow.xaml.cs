using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;

namespace PauseCue;

public partial class ControlWindow : Window
{
    private readonly BreakMonitor _monitor;
    private readonly GitHubReleaseUpdater _updater = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private GitHubReleaseUpdater.ReleaseInfo? _availableRelease;
    private bool _updateInProgress;

    /// <summary>Raised with the status title whenever the reminder status changes.</summary>
    internal event Action<string>? StatusChanged;
    /// <summary>Raised once per newly found release tag.</summary>
    internal event Action<string>? UpdateAvailable;

    public ControlWindow()
    {
        InitializeComponent();
        _monitor = new BreakMonitor(this);
        SelectByTag(WorkIntervalBox, _monitor.Settings.WorkIntervalMinutes);
        SelectByTag(NightStartBox, _monitor.Settings.NightStartHour);
        VersionText.Text = $"v{GitHubReleaseUpdater.CurrentVersion.ToString(3)}";
        Loaded += (_, _) => _monitor.Start();
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesInBackgroundAsync();
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    internal void ShowStatus(string title, string detail)
    {
        StatusText.Text = title;
        DetailText.Text = detail;
        StatusChanged?.Invoke(title);
    }

    /// <summary>Checks GitHub shortly after startup and then periodically, without showing errors.</summary>
    internal async void StartBackgroundUpdateChecks()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));
        await CheckForUpdatesInBackgroundAsync();
        _updateTimer.Start();
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        if (_updateInProgress) return;
        try
        {
            var release = await _updater.CheckAsync();
            if (release is null || release.Version <= GitHubReleaseUpdater.CurrentVersion) return;
            var isNew = _availableRelease?.Tag != release.Tag;
            _availableRelease = release;
            ShowUpdateButtonState();
            if (isNew) UpdateAvailable?.Invoke(release.Tag);
        }
        catch
        {
            // Offline or GitHub unavailable: stay quiet; the manual button still reports errors.
        }
    }

    private void ShowUpdateButtonState()
    {
        if (_availableRelease is null)
        {
            UpdateButton.Content = "更新を確認";
            UpdateButton.ClearValue(StyleProperty);
            UpdateButton.ToolTip = null;
            return;
        }
        UpdateButton.Content = $"{_availableRelease.Tag} に更新";
        UpdateButton.Style = (Style)FindResource("PrimaryAction");
        UpdateButton.ToolTip = "新しいバージョンがあります";
    }

    internal void UpdateWorkLog(double minutes, string path)
    {
        WorkLogText.Text = $"本日の記録: {(int)minutes / 60} 時間 {(int)minutes % 60} 分";
        WorkLogText.ToolTip = path;
    }

    private void OpenWorkLogClicked(object sender, RoutedEventArgs e)
    {
        var path = WorkLog.PathName;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) File.WriteAllText(path, "Start,End,Minutes,Reason\r\n", new System.Text.UTF8Encoding(true));
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void CheckForUpdatesClicked(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        _updateInProgress = true;
        try
        {
            var release = await _updater.CheckAsync();
            if (release is null || release.Version <= GitHubReleaseUpdater.CurrentVersion)
            {
                _availableRelease = null;
                MessageBox.Show("Pause Cueは最新です。", "更新を確認", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var choice = MessageBox.Show(
                $"新しいバージョン {release.Tag} があります。ダウンロードして再起動しますか？",
                "Pause Cue 更新", MessageBoxButton.YesNo, MessageBoxImage.Question);
            _availableRelease = release;
            if (choice != MessageBoxResult.Yes) return;

            UpdateButton.Content = "ダウンロード中…";
            var package = await _updater.DownloadAndVerifyAsync(release);
            GitHubReleaseUpdater.StartInstaller(package, release.Tag);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新を確認できませんでした。\n\n{ex.Message}", "Pause Cue 更新", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _updateInProgress = false;
            ShowUpdateButtonState();
            UpdateButton.IsEnabled = true;
        }
    }

    private static void SelectByTag(ComboBox box, int value)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem { Tag: not null } candidate && candidate.Tag.ToString() == value.ToString())
            {
                box.SelectedItem = candidate;
                return;
            }
        }
    }

    private void SettingsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_monitor is null || sender is not ComboBox box || box.SelectedItem is not ComboBoxItem item) return;
        if (box == WorkIntervalBox) _monitor.Settings.WorkIntervalMinutes = int.Parse(item.Tag.ToString()!);
        if (box == NightStartBox) _monitor.Settings.NightStartHour = int.Parse(item.Tag.ToString()!);
        _monitor.SaveSettings();
    }

    private void PauseClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string minutes }) _monitor.PauseFor(int.Parse(minutes));
    }

    private void ResumeClicked(object sender, RoutedEventArgs e) => _monitor.Resume();
    private void TakeBreakClicked(object sender, RoutedEventArgs e) => _monitor.TakeBreakNow();
    internal void PauseForTray(int minutes) => _monitor.PauseFor(minutes);
    internal void TakeBreakForTray() => _monitor.TakeBreakNow();
    internal void ExitApplication() { _monitor.Dispose(); Closing -= (_, _) => { }; }
}
