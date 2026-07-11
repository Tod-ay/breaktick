using System.Windows.Threading;

namespace BreakTick.App;

public sealed class BreakCoordinator : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly SettingsStore _settingsStore;
    private readonly AppDatabase _database;
    private readonly IIdleDetector _idleDetector;
    private DateTimeOffset _deadline;
    private TimeSpan _pausedRemaining;
    private TimeSpan _breakRemaining;
    private DateTimeOffset _breakStartedAt;
    private DateTimeOffset _skipGraceUntil;
    private TimerPhase _pausedPhase;
    private long? _sessionId;
    private DashboardStats _statistics;
    private bool _schedulePaused;

    public BreakCoordinator(SettingsStore settingsStore, AppDatabase database, IIdleDetector? idleDetector = null)
    {
        _settingsStore = settingsStore;
        _database = database;
        _idleDetector = idleDetector ?? new Win32IdleDetector();
        Settings = settingsStore.Load();
        RefreshTodayCount();
        _statistics = _database.GetDashboardStats();
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
    public bool IsLongBreak { get; private set; }
    public DashboardStats Statistics => _statistics;

    public event EventHandler? StateChanged;
    public event EventHandler? WorkStarted;
    public event EventHandler? BreakStarted;
    public event EventHandler? ReturnRequested;
    public event EventHandler? SettingsChanged;

    public void Start()
    {
        RefreshTodayCount();
        if (!TryRestoreTimer())
        {
            StartWork();
        }
    }

    public void TogglePause()
    {
        if (Phase == TimerPhase.Paused)
        {
            if (_schedulePaused && !WorkSchedule.IsActive(Settings, DateTime.Now))
            {
                return;
            }

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

    public void ResetWork() => StartWork();

    public void RegisterSkipClick()
    {
        if (Phase is not (TimerPhase.Breaking or TimerPhase.AwaitingReturn))
        {
            return;
        }

        SkipClickCount++;
        _skipGraceUntil = DateTimeOffset.Now.AddSeconds(3);
        if (SkipClickCount < 3)
        {
            OnStateChanged();
            return;
        }

        if (_sessionId is long sessionId)
        {
            _database.MarkSkipped(sessionId);
            _sessionId = null;
        }

        StartWork();
    }

    public void ConfirmReturn()
    {
        if (Phase != TimerPhase.AwaitingReturn)
        {
            return;
        }

        if (_sessionId is long sessionId)
        {
            Settings.CompletedToday = _database.CompleteBreak(sessionId);
            _sessionId = null;
            _statistics = _database.GetDashboardStats();
        }

        _settingsStore.Save(Settings);
        StartWork();
    }

    public bool UpdateSettings(int workMinutes, int breakSeconds, int dailyGoal, BreakPosition breakPosition, bool resetOnSessionUnlock, bool workHoursEnabled, string workStart, string workEnd, bool launchAtLogin, bool soundEnabled, bool fullScreenAllDisplays, bool longBreakEnabled)
    {
        if (workMinutes is < 1 or > 120 || breakSeconds is < 20 or > 900 || dailyGoal is < 1 or > 20)
        {
            return false;
        }

        Settings.WorkMinutes = workMinutes;
        Settings.BreakSeconds = breakSeconds;
        Settings.DailyGoal = dailyGoal;
        Settings.BreakPosition = breakPosition;
        Settings.ResetOnSessionUnlock = resetOnSessionUnlock;
        Settings.WorkHoursEnabled = workHoursEnabled;
        Settings.WorkStart = workStart;
        Settings.WorkEnd = workEnd;
        Settings.LaunchAtLogin = launchAtLogin;
        Settings.SoundEnabled = soundEnabled;
        Settings.FullScreenAllDisplays = fullScreenAllDisplays;
        Settings.LongBreakEnabled = longBreakEnabled;
        _settingsStore.Save(Settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);

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

    public void HandleSessionUnlock()
    {
        if (Settings.ResetOnSessionUnlock)
        {
            StartWork();
        }
    }

    public string ExportData() => _database.ExportJson();

    private bool TryRestoreTimer()
    {
        var snapshot = _database.LoadTimerSnapshot();
        if (snapshot?.SessionId is not long sessionId)
        {
            return false;
        }

        _sessionId = sessionId;
        switch (snapshot.Phase)
        {
            case TimerPhase.Working when snapshot.Deadline is DateTimeOffset deadline:
                _deadline = deadline;
                if (_deadline <= DateTimeOffset.Now)
                {
                    StartBreak();
                    return true;
                }

                Phase = TimerPhase.Working;
                _timer.Start();
                WorkStarted?.Invoke(this, EventArgs.Empty);
                OnStateChanged();
                return true;

            case TimerPhase.Breaking:
                _breakRemaining = snapshot.Remaining;
                if (_breakRemaining <= TimeSpan.Zero)
                {
                    Phase = TimerPhase.AwaitingReturn;
                    ReturnRequested?.Invoke(this, EventArgs.Empty);
                    OnStateChanged();
                    return true;
                }

                Phase = TimerPhase.Breaking;
                _breakStartedAt = DateTimeOffset.Now;
                _timer.Start();
                BreakStarted?.Invoke(this, EventArgs.Empty);
                OnStateChanged();
                return true;

            case TimerPhase.AwaitingReturn:
                Phase = TimerPhase.AwaitingReturn;
                ReturnRequested?.Invoke(this, EventArgs.Empty);
                OnStateChanged();
                return true;

            case TimerPhase.Paused when snapshot.ResumePhase is TimerPhase resumePhase:
                Phase = TimerPhase.Paused;
                _pausedPhase = resumePhase;
                _pausedRemaining = snapshot.Remaining;
                OnStateChanged();
                return true;

            default:
                _sessionId = null;
                return false;
        }
    }

    private void StartWork()
    {
        if (_sessionId is long existingSessionId)
        {
            _database.EndSession(existingSessionId);
        }

        Phase = TimerPhase.Working;
        IsBreakPausedForActivity = false;
        SkipClickCount = 0;
        _deadline = DateTimeOffset.Now.AddMinutes(Settings.WorkMinutes);
        _sessionId = _database.StartSession(Settings);
        _timer.Start();
        WorkStarted?.Invoke(this, EventArgs.Empty);
        OnStateChanged();
    }

    private void StartBreak()
    {
        Phase = TimerPhase.Breaking;
        _breakStartedAt = DateTimeOffset.Now;
        IsLongBreak = Settings.LongBreakEnabled
            && Settings.LongBreakInterval is >= 2 and <= 8
            && (Settings.CompletedToday + 1) % Settings.LongBreakInterval == 0;
        _breakRemaining = TimeSpan.FromSeconds(IsLongBreak ? Settings.LongBreakSeconds : Settings.BreakSeconds);
        if (_sessionId is long sessionId)
        {
            _database.MarkBreakStarted(sessionId);
        }

        IsBreakPausedForActivity = false;
        SkipClickCount = 0;
        if (Settings.SoundEnabled)
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        BreakStarted?.Invoke(this, EventArgs.Empty);
        OnStateChanged();
    }

    private void Tick()
    {
        if (Phase == TimerPhase.Paused && _schedulePaused)
        {
            if (WorkSchedule.IsActive(Settings, DateTime.Now))
            {
                _schedulePaused = false;
                Phase = _pausedPhase;
                _deadline = DateTimeOffset.Now.Add(_pausedRemaining);
            }
            else
            {
                OnStateChanged();
                return;
            }
        }

        if (Phase == TimerPhase.Working && !WorkSchedule.IsActive(Settings, DateTime.Now))
        {
            _pausedPhase = TimerPhase.Working;
            _pausedRemaining = Remaining;
            _schedulePaused = true;
            Phase = TimerPhase.Paused;
            OnStateChanged();
            return;
        }

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

    private void RefreshTodayCount()
    {
        Settings.CompletionDate = DateOnly.FromDateTime(DateTime.Today);
        Settings.CompletedToday = _database.GetTodayCompletionCount();
        _settingsStore.Save(Settings);
    }

    private void OnStateChanged()
    {
        _database.SaveTimerSnapshot(new TimerSnapshot(
            Phase,
            Phase == TimerPhase.Paused ? _pausedPhase : null,
            Phase == TimerPhase.Working ? _deadline : null,
            Remaining,
            _sessionId));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
        OnStateChanged();
    }
}
