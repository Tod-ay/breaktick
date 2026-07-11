using Microsoft.Win32;

namespace BreakTick.App;

public sealed class AutoStartService
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "BreakTick";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
    }
}
