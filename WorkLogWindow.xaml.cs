using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace PauseCue;

public partial class WorkLogWindow : Window
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");
    private static readonly Brush WorkBrush = Hex("#5267C8");
    private static readonly Brush LiveBrush = Hex("#3FAE8C");

    private readonly Func<DateTime?> _currentSessionStart;
    private readonly DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private List<WorkSession> _sessions = new();
    private DateTime _day = DateTime.Today;
    private bool _syncingPicker;

    /// <param name="currentSessionStart">Start of the not-yet-recorded running session, if any.</param>
    internal WorkLogWindow(Func<DateTime?> currentSessionStart)
    {
        InitializeComponent();
        _currentSessionStart = currentSessionStart;
        _liveTimer.Tick += (_, _) => Reload();
        WorkLog.Recorded += OnRecorded;
        IsVisibleChanged += (_, _) => { if (IsVisible) { Reload(); _liveTimer.Start(); } else _liveTimer.Stop(); };
        Closed += (_, _) => { WorkLog.Recorded -= OnRecorded; _liveTimer.Stop(); };
        Reload();
    }

    internal void ShowDay(DateTime day)
    {
        _day = day.Date;
        Render();
    }

    private void OnRecorded() => Dispatcher.BeginInvoke(Reload);

    private void Reload()
    {
        _sessions = WorkLog.ReadAll();
        if (_currentSessionStart() is { } start && DateTime.Now > start)
            _sessions.Add(new WorkSession(start, DateTime.Now, "Active"));
        Render();
    }

    private void Render()
    {
        var today = DateTime.Today;
        if (_day > today) _day = today;
        DayText.Text = _day.ToString("yyyy年M月d日 (ddd)", Japanese);
        NextDayButton.IsEnabled = _day < today;
        TodayButton.IsEnabled = _day != today;
        _syncingPicker = true;
        DayPicker.SelectedDate = _day;
        DayPicker.DisplayDateEnd = today;
        _syncingPicker = false;

        var parts = _sessions
            .Select(s => (Session: s, Part: s.ClipTo(_day)))
            .Where(x => x.Part is not null)
            .Select(x => (x.Session, Start: x.Part!.Value.Start, End: x.Part.Value.End))
            .ToList();

        var dayMinutes = parts.Sum(p => (p.End - p.Start).TotalMinutes);
        DayTotalText.Text = FormatMinutes(dayMinutes);
        LongestText.Text = parts.Count == 0 ? "—" : FormatMinutes(parts.Max(p => (p.End - p.Start).TotalMinutes));
        var weekMinutes = Enumerable.Range(0, 7).Sum(i => WorkLog.MinutesOn(_sessions, _day.AddDays(-i)));
        WeekTotalText.Text = FormatMinutes(weekMinutes);

        RenderTimeline(parts.Select(p => (p.Start, p.End, Live: p.Session.Reason == "Active")).ToList());
        RenderWeekChart();
        RenderList(parts);
    }

    private void RenderList(List<(WorkSession Session, DateTime Start, DateTime End)> parts)
    {
        var showShort = ShowShortBox.IsChecked == true;
        var rows = parts
            .Where(p => showShort || p.Session.Reason == "Active" || (p.End - p.Start).TotalMinutes >= 1)
            .OrderByDescending(p => p.Start)
            .Select(p => new SessionRow(p.Session, p.Start, p.End, _day))
            .ToList();
        SessionList.ItemsSource = rows;
        ListTitle.Text = $"記録一覧 ({rows.Count} 件)";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0 && parts.Count > 0) EmptyText.Text = "1 分以上の記録はありません";
        else EmptyText.Text = "この日の記録はありません";

        var hidden = parts.Count - rows.Count;
        HiddenNote.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        HiddenNote.Text = $"1 分未満の短い記録 {hidden} 件を非表示にしています。";
    }

    private void RenderTimeline(List<(DateTime Start, DateTime End, bool Live)> parts)
    {
        TimelineCanvas.Children.Clear();
        TimelineAxis.Children.Clear();
        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        for (var hour = 0; hour <= 24; hour += 3)
        {
            var x = width * hour / 24.0;
            if (hour is > 0 and < 24)
                TimelineCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = height, Stroke = Hex("#DDE2ED"), StrokeThickness = 1 });
            var label = new TextBlock { Text = hour.ToString(CultureInfo.InvariantCulture), FontSize = 10, Foreground = Hex("#8790A2") };
            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Clamp(x - label.DesiredSize.Width / 2, 0, width - label.DesiredSize.Width));
            Canvas.SetTop(label, 3);
            TimelineAxis.Children.Add(label);
        }

        foreach (var (start, end, live) in parts)
        {
            var left = width * (start - _day).TotalHours / 24.0;
            var blockWidth = Math.Max(2, width * (end - start).TotalHours / 24.0);
            var block = new Rectangle
            {
                Width = blockWidth,
                Height = height - 10,
                RadiusX = 3,
                RadiusY = 3,
                Fill = live ? LiveBrush : WorkBrush,
                ToolTip = $"{start:HH:mm} – {end:HH:mm}  ({FormatMinutes((end - start).TotalMinutes)})"
            };
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, 5);
            TimelineCanvas.Children.Add(block);
        }
    }

    private void RenderWeekChart()
    {
        WeekChart.Children.Clear();
        var days = Enumerable.Range(0, 7).Select(i => _day.AddDays(i - 6)).ToList();
        var minutes = days.Select(d => WorkLog.MinutesOn(_sessions, d)).ToList();
        var max = Math.Max(60, minutes.Max());
        const double barArea = 60;

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var selected = day == _day;
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.Hand, Background = Brushes.Transparent };
            column.Children.Add(new TextBlock
            {
                Text = minutes[i] < 1 ? "" : ShortHours(minutes[i]),
                FontSize = 10,
                Foreground = Hex("#68738A"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });
            column.Children.Add(new Border
            {
                Height = Math.Max(minutes[i] > 0 ? 3 : 2, barArea * minutes[i] / max),
                Width = 18,
                CornerRadius = new CornerRadius(4, 4, 1, 1),
                Background = minutes[i] > 0 ? (selected ? WorkBrush : Hex("#B9C3EC")) : Hex("#E6E9F2"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
            column.Children.Add(new TextBlock
            {
                Text = day.ToString("ddd", Japanese),
                FontSize = 11,
                FontWeight = selected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = selected ? Hex("#20283A") : Hex("#8790A2"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });
            column.Children.Add(new TextBlock
            {
                Text = day.ToString("M/d", CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = Hex("#A0A8B8"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
            column.ToolTip = $"{day.ToString("M月d日 (ddd)", Japanese)}: {FormatMinutes(minutes[i])}";
            column.MouseLeftButtonUp += (_, _) => ShowDay(day);
            WeekChart.Children.Add(column);
        }
    }

    internal static string FormatMinutes(double minutes)
    {
        if (minutes <= 0) return "0 分";
        if (minutes < 1) return "1 分未満";
        var total = (int)Math.Round(minutes);
        return total >= 60 ? $"{total / 60} 時間 {total % 60} 分" : $"{total} 分";
    }

    private static string ShortHours(double minutes) =>
        minutes >= 60 ? $"{minutes / 60:0.#}h" : $"{Math.Round(minutes)}m";

    private static SolidColorBrush Hex(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private void TimelineSizeChanged(object sender, SizeChangedEventArgs e) => Render();
    private void PreviousDayClicked(object sender, RoutedEventArgs e) => ShowDay(_day.AddDays(-1));
    private void NextDayClicked(object sender, RoutedEventArgs e) => ShowDay(_day.AddDays(1));
    private void TodayClicked(object sender, RoutedEventArgs e) => ShowDay(DateTime.Today);
    private void ShowShortChanged(object sender, RoutedEventArgs e) { if (IsLoaded) Render(); }

    private void DayPickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingPicker || DayPicker.SelectedDate is not { } date) return;
        ShowDay(date);
    }

    private void OpenCsvClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            WorkLog.EnsureFileExists();
            Process.Start(new ProcessStartInfo(WorkLog.PathName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSV ファイルを開けませんでした。\n\n{ex.Message}", "作業ログ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>One row in the session list.</summary>
    private sealed class SessionRow
    {
        public SessionRow(WorkSession session, DateTime start, DateTime end, DateTime day)
        {
            // Mark times that belong to the neighbouring day so midnight-crossing sessions read naturally.
            var startText = session.Start < day ? $"前日 {session.Start:HH:mm}" : $"{start:HH:mm}";
            var endText = session.Reason == "Active" ? "現在" : session.End >= day.AddDays(1) ? $"翌日 {session.End:HH:mm}" : $"{end:HH:mm}";
            TimeRange = $"{startText} – {endText}";
            var dayPart = FormatMinutes((end - start).TotalMinutes);
            DurationText = session.Start < day || session.End > day.AddDays(1)
                ? $"{dayPart} (全体 {FormatMinutes(session.Duration.TotalMinutes)})"
                : dayPart;
            ReasonLabel = session.ReasonLabel;
            (BadgeBackground, BadgeForeground) = session.Reason switch
            {
                "Active" => (Hex("#E3F5EE"), Hex("#2A8A6C")),
                "Break" => (Hex("#E9EDFA"), Hex("#5267C8")),
                "Idle" => (Hex("#FFF3E0"), Hex("#B26A00")),
                "Exit" => (Hex("#F1F3F9"), Hex("#68738A")),
                _ => (Hex("#EEF0F6"), Hex("#4C5770"))
            };
        }

        public string TimeRange { get; }
        public string DurationText { get; }
        public string ReasonLabel { get; }
        public Brush BadgeBackground { get; }
        public Brush BadgeForeground { get; }
    }
}
