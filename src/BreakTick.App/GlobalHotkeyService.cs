using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace BreakTick.App;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x4254;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyB = 0x42;
    private readonly Action _onTriggered;

    public GlobalHotkeyService(Action onTriggered)
    {
        _onTriggered = onTriggered;
        IsRegistered = RegisterHotKey(IntPtr.Zero, HotkeyId, ModControl | ModAlt | ModNoRepeat, VirtualKeyB);
        if (IsRegistered)
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        }
    }

    public bool IsRegistered { get; }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey || msg.wParam.ToInt32() != HotkeyId)
        {
            return;
        }

        handled = true;
        _onTriggered();
    }

    public void Dispose()
    {
        if (!IsRegistered)
        {
            return;
        }

        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        UnregisterHotKey(IntPtr.Zero, HotkeyId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
