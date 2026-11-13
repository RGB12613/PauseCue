using Microsoft.Win32;
using System.Windows.Threading;

namespace PauseCue;

public sealed class BreakMonitor : IDisposable
{
    private readonly ControlWindow _window;
    private const int NightReminderMinutes = 15;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private DateTime _continuousSince = DateTime.Now;
    private DateTime _lastCheck;
    private DateTime? _nextPrompt;
    private int _postponeCount;
    private bool _overlayVisible;
    private bool _sleeping;

    public UserSettings Settings { get; } = UserSettings.Load();

    public BreakMonitor(ControlWindow window)
    {
        _window = window;
        _timer.Tick += (_, _) => Check();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        _window.Dispatcher.BeginInvoke(() =>
        {
            var now = DateTime.Now;
            if (e.Mode == PowerModes.Suspend)
            {
                WorkLog.Record(_continuousSince, now, "Sleep");
                _sleeping = true;
                _timer.Stop();
                return;
            }
            if (e.Mode == PowerModes.Resume && _sleeping)
            {
                _sleeping = false;
                _continuousSince = now;
                _lastCheck = now;
                _postponeCount = 0;
                _nextPrompt = now.AddMinutes(10);
                _timer.Start();
                Check();
            }
        });
    }

    public void Start() { _timer.Start(); Check(); }
    public void SaveSettings() => Settings.Save();
    public void PauseFor(int minutes) { Settings.PausedUntil = DateTime.Now.AddMinutes(minutes); SaveSettings(); Check(); }
    public void Resume() { Settings.PausedUntil = null; SaveSettings(); Check(); }
    public void TakeBreakNow() { if (!_overlayVisible) ShowPrompt(IsNightPeriod(DateTime.Now), manual: true); }

    private void Check()
    {
        var now = DateTime.Now;
        var previousCheck = _lastCheck;
        var slept = previousCheck != default && (now - previousCheck).TotalMinutes >= Settings.IdleResetMinutes;
        _lastCheck = now;
        var idleMinutes = LastInput.Since().TotalMinutes;
        if (slept || idleMinutes >= Settings.IdleResetMinutes)
        {
            var end = slept ? previousCheck : now - TimeSpan.FromMinutes(idleMinutes);
            WorkLog.Record(_continuousSince, end, slept ? "Sleep/long pause" : "Idle");
            _continuousSince = now;
            _postponeCount = 0;
            // Treat sleep and long idle periods as a break, then wait before showing another reminder.
            _nextPrompt = now.AddMinutes(10);
        }
        if (Settings.PausedUntil > now)
        {
            _window.ShowStatus("一時停止中", $"{Settings.PausedUntil:HH:mm} に通知を再開します。");
            return;
        }

        var work = now - _continuousSince;
        var night = IsNightPeriod(now);
        var due = night || work.TotalMinutes >= Settings.WorkIntervalMinutes;
        var remaining = Math.Max(0, Settings.WorkIntervalMinutes - (int)work.TotalMinutes);
        var title = due ? "休憩のタイミングです" : $"次の休憩まで 約 {remaining} 分";
        var detail = night
            ? "夜間モードです。画面から目を離して、目と体を休めましょう。"
            : $"連続作業 { (int)work.TotalMinutes } 分。{Settings.IdleResetMinutes} 分以上離席すると作業時間をリセットします。";
        _window.ShowStatus(title, detail);

        if (due && (_nextPrompt is null || now >= _nextPrompt) && !_overlayVisible) ShowPrompt(night);
        var liveToday = Math.Max(0, (now - (_continuousSince.Date == now.Date ? _continuousSince : now.Date)).TotalMinutes);
        _window.UpdateWorkLog(WorkLog.MinutesToday() + liveToday, WorkLog.PathName);
    }

    private bool IsNightPeriod(DateTime now)
    {
        if (Settings.NightStartHour < 0) return false;
        const int nightEnd = 5;
        return Settings.NightStartHour < nightEnd
            ? now.Hour >= Settings.NightStartHour && now.Hour < nightEnd
            : now.Hour >= Settings.NightStartHour || now.Hour < nightEnd;
    }

    private void ShowPrompt(bool night, bool manual = false)
    {
        _overlayVisible = true;
        var delay = _postponeCount == 0 ? 5 : _postponeCount == 1 ? 15 : 30;
        var text = manual
            ? "少し席を離れて、軽く体を動かしたり水分をとったりしましょう。"
            : night
                ? $"夜間モード中は作業時間に関係なく、{NightReminderMinutes} 分ごとにお知らせします。画面から目を離して、目と体を休めてみませんか？"
                : "集中を続けるための小さな休憩です。伸びをする、水を飲む、遠くを見るなど、どれか一つで十分です。";
        var overlay = new BreakOverlay(text, delay, tookBreak =>
        {
            var now = DateTime.Now;
            _overlayVisible = false;
            if (tookBreak)
            {
                // A break was taken: restart the work interval and don't nag again until it has elapsed.
                // In night mode "due" is always true, so this is what spaces out night reminders.
                _continuousSince = now;
                _postponeCount = 0;
                _nextPrompt = now.AddMinutes(night ? NightReminderMinutes : Settings.WorkIntervalMinutes);
            }
            else
            {
                _nextPrompt = now.AddMinutes(delay);
                _postponeCount++;
            }
        }, night: night && !manual);
        overlay.Closed += (_, _) => _overlayVisible = false;
        overlay.Show();
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
