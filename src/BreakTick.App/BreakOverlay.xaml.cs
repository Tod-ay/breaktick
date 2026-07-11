using System.Windows;

namespace BreakTick.App;

public partial class BreakOverlay : Window
{
    private readonly BreakCoordinator _coordinator;

    public BreakOverlay(BreakCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _coordinator.StateChanged += (_, _) => Refresh();
        Loaded += (_, _) => PositionTopRight();
    }

    public void ShowForBreak()
    {
        Show();
        PositionTopRight();
        Activate();
        Refresh();
    }

    public void ShowReturnPrompt()
    {
        Show();
        PositionTopRight();
        Refresh();
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.Phase == TimerPhase.AwaitingReturn)
        {
            _coordinator.ConfirmReturn();
        }
        else
        {
            _coordinator.RegisterSkipClick();
        }
    }

    private void Refresh()
    {
        var returning = _coordinator.Phase == TimerPhase.AwaitingReturn;
        TitleText.Text = returning ? "休息完成" : "现在休息一下";
        BodyText.Text = returning ? "准备好了就回到工作节奏。" : "起身走走、远眺窗外，给眼睛一点空间。";
        TimeText.Text = returning ? "✓" : TrayService.Format(_coordinator.Remaining);
        WarningText.Text = _coordinator.IsBreakPausedForActivity ? "检测到键鼠操作，休息倒计时已暂停" : string.Empty;
        ActionButton.Content = returning
            ? "我回来了"
            : $"跳过休息 ({_coordinator.SkipClickCount}/3)";
    }

    private void PositionTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Top + 24;
    }
}
