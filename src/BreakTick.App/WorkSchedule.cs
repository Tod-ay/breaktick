namespace BreakTick.App;

public static class WorkSchedule
{
    public static bool IsActive(AppSettings settings, DateTime now)
    {
        if (!settings.WorkHoursEnabled)
        {
            return true;
        }

        if (!TimeOnly.TryParse(settings.WorkStart, out var start) || !TimeOnly.TryParse(settings.WorkEnd, out var end))
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(now);
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }
}
