using System.Windows.Interop;

namespace LyricFlow.UI;

/// <summary>A global hotkey delivered through a hidden message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4C46;

    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("LyricFlowHotkey")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public event Action? Pressed;

    /// <summary>Replaces the current hotkey. Returns false if the text is invalid or another app owns the combination.</summary>
    public bool Register(string text)
    {
        Unregister();

        if (!HotkeyParser.TryParse(text, out var modifiers, out var key))
            return false;

        _registered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, key);
        return _registered;
    }

    private void Unregister()
    {
        if (!_registered)
            return;

        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
