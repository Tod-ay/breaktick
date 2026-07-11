using System.Windows.Threading;

namespace BreakTick.App;

public sealed class BreakCoordinator : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly SettingsStore _settingsStore;
    private readonly IIdleDetector _idleDetector;
    private DateTimeOffset _deadline;
    private TimeSpan _pausedRemaining;
    private TimeSpan _breakRemaining;
    private DateTimeOffset _breakStartedAt;
    private DateTimeOffset _skipGraceUntil;
    private TimerPhase _pausedPhase;

    public BreakCoordinator(SettingsStore settingsStore, IIdleDetector? idleDetector = null)
    {
        _settingsStore = settingsStore;
        _idleDetector = idleDetector ?? new Win32IdleDetector();
        Settings = settingsStore.Load();
        ResetDailyCountIfNeeded();
        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Tick();
    }

    public AppSettings Settings { get; }
    public TimerPhase Phase { get; private set; } = TimerPhase.Working;
    public TimeSpan Remaining => Phase switch
    {
        TimerPhase.AwaitingReturn => TimeSpan.Zero,
        TimerPhase.Paused => _pausedRemaining,
        TimerPhase.Breaking => _breakRemaining,
        _ => TimeSpan.FromSeconds(Math.Max(0, (_deadline - DateTimeOffset.Now).TotalSeconds))
    };
    public bool IsPaused => Phase == TimerPhase.Paused;
    public bool IsBreakPausedForActivity { get; private set; }
    public int SkipClickCount { get; private set; }

    public event EventHandler? StateChanged;
    public event EventHandler? WorkStarted;
    public event EventHandler? BreakStarted;
    public event EventHandler? ReturnRequested;

    public void Start()
    {
        ResetDailyCountIfNeeded();
        StartWork();
    }

    public void TogglePause()
    {
        if (Phase == TimerPhase.Paused)
        {
            Phase = _pausedPhase;
            _deadline = DateTimeOffset.Now.Add(_pausedRemaining);
            _timer.Start();
        }
        else
        {
            _pausedPhase = Phase;
            _pausedRemaining = Remaining;
            Phase = TimerPhase.Paused;
            _timer.Stop();
        }

        OnStateChanged();
    }

    public void ResetWork()
    {
        StartWork();
    }

    public void RegisterSkipClick()
    {
        if (Phase is TimerPhase.Breaking or TimerPhase.AwaitingReturn)
        {
            SkipClickCount++;
            _skipGraceUntil = DateTimeOffset.Now.AddSeconds(3);
            if (SkipClickCount >= 3)
            {
                StartWork();
                return;
            }

            OnStateChanged();
        }
    }

    public void ConfirmReturn()
    {
        if (Phase != TimerPhase.AwaitingReturn)
        {
            return;
        }

        ResetDailyCountIfNeeded();
        Settings.CompletedToday++;
        _settingsStore.Save(Settings);
        StartWork();
    }

    public bool UpdateSettings(int workMinutes, int breakSeconds, int dailyGoal)
    {
        if (workMinutes is < 1 or > 120 || breakSeconds is < 20 or > 900 || dailyGoal is < 1 or > 20)
        {
            return false;
        }

        Settings.WorkMinutes = workMinutes;
        Settings.BreakSeconds = breakSeconds;
        Settings.DailyGoal = dailyGoal;
        _settingsStore.Save(Settings);

        if (Phase is TimerPhase.Working or TimerPhase.Paused)
        {
            StartWork();
        }
        else
        {
            OnStateChanged();
        }

        return true;
    }

    private void StartWork()
    {
        Phase = TimerPhase.Working;
        IsBreakPausedForActivity = false;
        SkipClickCount = 0;
        _deadline = DateTimeOffset.Now.AddMinutes(Settings.WorkMinutes);
        _timer.Start();
        WorkStarted?.Invoke(this, EventArgs.Empty);
        OnStateChanged();
    }

    private void StartBreak()
    {
        Phase = TimerPhase.Breaking;
        _breakStartedAt = DateTimeOffset.Now;
        _breakRemaining = TimeSpan.FromSeconds(Settings.BreakSeconds);
        IsBreakPausedForActivity = false;
        SkipClickCount = 0;
        BreakStarted?.Invoke(this, EventArgs.Empty);
        OnStateChanged();
    }

    private void Tick()
    {
        if (Phase == TimerPhase.Breaking)
        {
            TickBreak();
            return;
        }

        if (Phase != TimerPhase.Working || DateTimeOffset.Now < _deadline)
        {
            OnStateChanged();
            return;
        }

        StartBreak();
    }

    private void TickBreak()
    {
        var isInGracePeriod = DateTimeOffset.Now - _breakStartedAt < TimeSpan.FromSeconds(4)
            || DateTimeOffset.Now < _skipGraceUntil;
        var hasRecentInput = _idleDetector.GetIdleDuration() < TimeSpan.FromSeconds(3);
        IsBreakPausedForActivity = !isInGracePeriod && hasRecentInput;

        if (!IsBreakPausedForActivity)
        {
            _breakRemaining -= TimeSpan.FromSeconds(1);
        }

        if (_breakRemaining > TimeSpan.Zero)
        {
            OnStateChanged();
            return;
        }

        _timer.Stop();
        Phase = TimerPhase.AwaitingReturn;
        IsBreakPausedForActivity = false;
        ReturnRequested?.Invoke(this, EventArgs.Empty);
        OnStateChanged();
    }

    private void ResetDailyCountIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (Settings.CompletionDate == today)
        {
            return;
        }

        Settings.CompletionDate = today;
        Settings.CompletedToday = 0;
        _settingsStore.Save(Settings);
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _timer.Stop();
}
