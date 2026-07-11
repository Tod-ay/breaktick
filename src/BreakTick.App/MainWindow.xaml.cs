using System.ComponentModel;
using System.Windows;

namespace BreakTick.App;

public partial class MainWindow : Window
{
    private readonly BreakCoordinator _coordinator;
    private readonly SettingsStore _settingsStore;

    public MainWindow(BreakCoordinator coordinator, SettingsStore settingsStore)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _coordinator.StateChanged += (_, _) => Refresh();
        WorkMinutesBox.Text = _coordinator.Settings.WorkMinutes.ToString();
        BreakSecondsBox.Text = _coordinator.Settings.BreakSeconds.ToString();
        DailyGoalBox.Text = _coordinator.Settings.DailyGoal.ToString();
        PositionBox.SelectedValue = _coordinator.Settings.BreakPosition.ToString();
        ResetOnUnlockCheck.IsChecked = _coordinator.Settings.ResetOnSessionUnlock;
        WorkHoursEnabledCheck.IsChecked = _coordinator.Settings.WorkHoursEnabled;
        WorkStartBox.Text = _coordinator.Settings.WorkStart;
        WorkEndBox.Text = _coordinator.Settings.WorkEnd;
        Refresh();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.TogglePause();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.ResetWork();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WorkMinutesBox.Text, out var workMinutes)
            || !int.TryParse(BreakSecondsBox.Text, out var breakSeconds)
            || !int.TryParse(DailyGoalBox.Text, out var dailyGoal)
            || !Enum.TryParse<BreakPosition>(PositionBox.SelectedValue?.ToString(), out var breakPosition)
            || !TimeOnly.TryParse(WorkStartBox.Text, out _)
            || !TimeOnly.TryParse(WorkEndBox.Text, out _)
            || !_coordinator.UpdateSettings(workMinutes, breakSeconds, dailyGoal, breakPosition, ResetOnUnlockCheck.IsChecked == true, WorkHoursEnabledCheck.IsChecked == true, WorkStartBox.Text, WorkEndBox.Text))
        {
            SettingsMessage.Text = "工作 1–120 分钟；休息 20–900 秒；目标 1–20 次。";
            return;
        }

        SettingsMessage.Text = "设置已保存，并已重新开始本轮工作计时。";
        Refresh();
    }

    private void Refresh()
    {
        PhaseText.Text = _coordinator.Phase switch
        {
            TimerPhase.Working => "专注工作中",
            TimerPhase.Breaking => "请暂时离开屏幕",
            TimerPhase.AwaitingReturn => "休息完成",
            _ => "已暂停"
        };
        TimeText.Text = TrayService.Format(_coordinator.Remaining);
        PauseButton.Content = _coordinator.IsPaused ? "继续" : "暂停";
        ProgressText.Text = $"今日完成 {_coordinator.Settings.CompletedToday} / {_coordinator.Settings.DailyGoal} 次休息";
        var stats = _coordinator.Statistics;
        StatsText.Text = $"连续打卡 {stats.CurrentStreak} 天 · 累计 {stats.TotalCompletions} 次";
        WeekText.Text = $"近 7 天：{string.Join(" · ", stats.RecentDays)}";
        var badge = BadgeCatalog.Next(stats);
        BadgeText.Text = $"下一枚徽章：{badge.Label}（{badge.Current}/{badge.Target}）";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _settingsStore.Save(_coordinator.Settings);
    }
}
