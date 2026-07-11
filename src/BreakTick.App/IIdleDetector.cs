namespace BreakTick.App;

public interface IIdleDetector
{
    TimeSpan GetIdleDuration();
}
