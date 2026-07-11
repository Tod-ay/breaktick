namespace BreakTick.App;

public sealed record BadgeProgress(string Label, int Current, int Target);

public static class BadgeCatalog
{
    private static readonly int[] StreakTargets = [3, 7, 14, 21, 30, 60, 90, 180, 365];
    private static readonly int[] TotalTargets = [10, 20, 50, 100, 200, 500, 1000, 2000, 5000];

    public static BadgeProgress Next(DashboardStats stats)
    {
        var streakTarget = StreakTargets.FirstOrDefault(target => target > stats.CurrentStreak);
        var totalTarget = TotalTargets.FirstOrDefault(target => target > stats.TotalCompletions);

        if (streakTarget == 0 && totalTarget == 0)
        {
            return new BadgeProgress("全部徽章已解锁", 1, 1);
        }

        if (totalTarget == 0 || (streakTarget != 0 && streakTarget - stats.CurrentStreak <= totalTarget - stats.TotalCompletions))
        {
            return new BadgeProgress($"连续打卡 {streakTarget} 天", stats.CurrentStreak, streakTarget);
        }

        return new BadgeProgress($"累计完成 {totalTarget} 次", stats.TotalCompletions, totalTarget);
    }
}
