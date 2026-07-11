namespace BreakTick.App;

public sealed record TimerSnapshot(
    TimerPhase Phase,
    TimerPhase? ResumePhase,
    DateTimeOffset? Deadline,
    TimeSpan Remaining,
    long? SessionId);
