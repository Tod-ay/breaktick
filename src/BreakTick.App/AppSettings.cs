namespace BreakTick.App;

public sealed class AppSettings
{
    public int WorkMinutes { get; set; } = 60;
    public int BreakSeconds { get; set; } = 120;
    public int DailyGoal { get; set; } = 8;
    public BreakPosition BreakPosition { get; set; } = BreakPosition.TopRight;
    public bool ResetOnSessionUnlock { get; set; } = true;
    public bool WorkHoursEnabled { get; set; }
    public string WorkStart { get; set; } = "09:00";
    public string WorkEnd { get; set; } = "18:00";
    public bool LaunchAtLogin { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool FullScreenAllDisplays { get; set; }
    public bool LongBreakEnabled { get; set; }
    public int LongBreakInterval { get; set; } = 4;
    public int LongBreakSeconds { get; set; } = 900;
    public int CompletedToday { get; set; }
    public DateOnly CompletionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
