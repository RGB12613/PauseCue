using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PauseCue;

public partial class BreakOverlay : Window
{
    private readonly Action<int> _complete;
    private readonly int _delay;
    private readonly DispatcherTimer _holdTimer;
    private readonly Stopwatch _holdWatch = new();
    private Button? _pressedButton;
    private const double HoldDurationMs = 850;

    public BreakOverlay(string message, int delay, Action<int> complete)
    {
        InitializeComponent();
        MessageText.Text = message;
        _delay = delay;
        _complete = complete;
        LaterButton.Content = $"あと {_delay} 分だけつづける";
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _holdTimer.Tick += HoldTimerElapsed;
    }

    private void ActionButtonPressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button) return;
        _pressedButton = button;
        Mouse.Capture(button);
        _holdWatch.Restart();
        UpdateButtonProgress(button, 0);
        HoldHintText.Text = "押し続けてください…";
        _holdTimer.Start();
        e.Handled = true;
    }

    private void ActionButtonReleased(object sender, MouseButtonEventArgs e)
    {
        _holdTimer.Stop();
        if (Mouse.Captured == sender) Mouse.Capture(null);
        if (sender is Button button) ResetProgress(button);
        _pressedButton = null;
        e.Handled = true;
    }

    private void ActionButtonCaptureLost(object sender, MouseEventArgs e)
    {
        _holdTimer.Stop();
        if (sender is Button button) ResetProgress(button);
        _pressedButton = null;
    }

    private void ActionButtonSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Button button) return;
        var progress = button == _pressedButton
            ? Math.Clamp(_holdWatch.Elapsed.TotalMilliseconds / HoldDurationMs, 0, 1)
            : 0;
        UpdateButtonProgress(button, progress);
    }

    private static void UpdateButtonProgress(Button button, double progress)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("PART_ProgressFill", button) is not Border fill) return;
        fill.Width = Math.Max(0, button.ActualWidth * Math.Clamp(progress, 0, 1));
    }

    private void ResetProgress(Button button)
    {
        _holdWatch.Reset();
        UpdateButtonProgress(button, 0);
        HoldHintText.Text = "ボタンを押し続けてください";
    }

    private void HoldTimerElapsed(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(_holdWatch.Elapsed.TotalMilliseconds / HoldDurationMs, 0, 1);
        if (_pressedButton is not null) UpdateButtonProgress(_pressedButton, progress);
        HoldHintText.Text = $"長押し中… {progress:P0}";
        if (progress < 1 || _pressedButton is null) return;

        _holdTimer.Stop();
        _holdWatch.Stop();
        var minutes = _pressedButton == LaterButton ? _delay : 3;
        _pressedButton = null;
        _complete(minutes);
        Close();
    }
}
