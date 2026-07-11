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
    public int CompletedToday { get; set; }
    public DateOnly CompletionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
