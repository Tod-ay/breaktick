namespace BreakTick.App;

public sealed record DashboardStats(int TotalCompletions, int CurrentStreak, IReadOnlyList<int> RecentDays);
