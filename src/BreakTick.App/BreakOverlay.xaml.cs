using System.Windows;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace BreakTick.App;

public partial class BreakOverlay : Window
{
    private readonly BreakCoordinator _coordinator;

    public BreakOverlay(BreakCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _coordinator.StateChanged += (_, _) => Refresh();
        Loaded += (_, _) => ApplyPosition();
    }

    public void ShowForBreak()
    {
        Show();
        ApplyPosition();
        Activate();
        Refresh();
    }

    public void ShowReturnPrompt()
    {
        Show();
        ApplyPosition();
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
        TitleText.Text = returning ? "休息完成" : _coordinator.IsLongBreak ? "长休息时间" : "现在休息一下";
        BodyText.Text = returning ? "准备好了就回到工作节奏。" : "起身走走、远眺窗外，给眼睛一点空间。";
        TimeText.Text = returning ? "✓" : TrayService.Format(_coordinator.Remaining);
        WarningText.Text = _coordinator.IsBreakPausedForActivity ? "检测到键鼠操作，休息倒计时已暂停" : string.Empty;
        ActionButton.Content = returning
            ? "我回来了"
            : $"跳过休息 ({_coordinator.SkipClickCount}/3)";
    }

    private void ApplyPosition()
    {
        var workArea = SystemParameters.WorkArea;
        var position = _coordinator.Settings.BreakPosition;
        var isFullScreen = position == BreakPosition.FullScreen;

        OverlaySurface.Background = isFullScreen
            ? new SolidColorBrush(WpfColor.FromArgb(235, 13, 20, 16))
            : new SolidColorBrush(WpfColor.FromRgb(253, 254, 253));
        OverlaySurface.BorderThickness = isFullScreen ? new Thickness(0) : new Thickness(1);
        OverlaySurface.CornerRadius = isFullScreen ? new CornerRadius(0) : new CornerRadius(18);

        if (isFullScreen)
        {
            Left = _coordinator.Settings.FullScreenAllDisplays ? SystemParameters.VirtualScreenLeft : 0;
            Top = _coordinator.Settings.FullScreenAllDisplays ? SystemParameters.VirtualScreenTop : 0;
            Width = _coordinator.Settings.FullScreenAllDisplays ? SystemParameters.VirtualScreenWidth : SystemParameters.PrimaryScreenWidth;
            Height = _coordinator.Settings.FullScreenAllDisplays ? SystemParameters.VirtualScreenHeight : SystemParameters.PrimaryScreenHeight;
            return;
        }

        Width = 340;
        Height = 310;
        switch (position)
        {
            case BreakPosition.TopLeft:
                Left = workArea.Left + 24;
                Top = workArea.Top + 24;
                break;
            case BreakPosition.Center:
                Left = workArea.Left + (workArea.Width - Width) / 2;
                Top = workArea.Top + (workArea.Height - Height) / 2;
                break;
            default:
                Left = workArea.Right - Width - 24;
                Top = workArea.Top + 24;
                break;
        }
    }
}
