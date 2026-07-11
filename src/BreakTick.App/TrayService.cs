using Forms = System.Windows.Forms;

namespace BreakTick.App;

public sealed class TrayService : IDisposable
{
    private readonly BreakCoordinator _coordinator;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayService(BreakCoordinator coordinator, Action showDashboard, Action exit)
    {
        _coordinator = coordinator;
        _pauseItem = new Forms.ToolStripMenuItem("暂停", null, (_, _) => _coordinator.TogglePause());
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("打开 BreakTick", null, (_, _) => showDashboard()));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("重新开始工作计时", null, (_, _) => _coordinator.ResetWork()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("退出", null, (_, _) => exit()));

        _icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => showDashboard();
        Refresh();
    }

    public void Refresh()
    {
        _pauseItem.Text = _coordinator.IsPaused ? "继续" : "暂停";
        _icon.Text = $"BreakTick · {PhaseLabel(_coordinator.Phase)} · {Format(_coordinator.Remaining)}";
    }

    private static string PhaseLabel(TimerPhase phase) => phase switch
    {
        TimerPhase.Working => "工作中",
        TimerPhase.Breaking => "休息中",
        TimerPhase.AwaitingReturn => "等待返回",
        _ => "已暂停"
    };

    internal static string Format(TimeSpan value) => $"{Math.Max(0, (int)value.TotalMinutes):00}:{Math.Max(0, value.Seconds):00}";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
