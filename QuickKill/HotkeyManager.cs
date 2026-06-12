using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuickKill;

public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int  WM_HOTKEY   = 0x0312;
    private const int  ID          = 0x1337;
    private const uint MOD_NOREPEAT = 0x4000; // prevents WM_HOTKEY from repeating while key is held

    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;

    public event EventHandler? HotkeyFired;

    private HwndSource? _source;
    private IntPtr      _hwnd;
    private bool        _registered;

    public bool Register(Window msgWindow, uint modifiers, uint vk)
    {
        _hwnd = new WindowInteropHelper(msgWindow).Handle;
        if (_source is null)
        {
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);
        }
        _registered = RegisterHotKey(_hwnd, ID, modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == ID)
        {
            HotkeyFired?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public bool Reregister(uint modifiers, uint vk)
    {
        if (_registered && _hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, ID);
        _registered = RegisterHotKey(_hwnd, ID, modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, ID);
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _registered = false;
    }
}
