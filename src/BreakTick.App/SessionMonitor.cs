using Microsoft.Win32;

namespace BreakTick.App;

public sealed class SessionMonitor : IDisposable
{
    private bool _wasLocked;

    public SessionMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public event EventHandler? UnlockedAfterLock;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _wasLocked = true;
            return;
        }

        if (e.Reason == SessionSwitchReason.SessionUnlock && _wasLocked)
        {
            _wasLocked = false;
            UnlockedAfterLock?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => SystemEvents.SessionSwitch -= OnSessionSwitch;
}
