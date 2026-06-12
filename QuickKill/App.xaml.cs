using Microsoft.Win32;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace QuickKill;

public partial class App : Application
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "QuickKill";

    private NotifyIcon?    _tray;
    private KillWindow?    _killWindow;
    private HotkeyManager? _hotkey;
    private Window?        _msgWindow;
    private Mutex?         _instanceMutex;
    private string         _hotkeyLabel = "Ctrl+Alt+Q";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance — a second launch just exits instead of piling up
        // tray icons and stealing the hotkey registration.
        _instanceMutex = new Mutex(true, @"Local\QuickKill_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _killWindow = new KillWindow();
        // Pre-create the HWND so first Show() never triggers a flash from HWND construction.
        new System.Windows.Interop.WindowInteropHelper(_killWindow).EnsureHandle();
        SetupHotkey();
        SetupTray();

        // Warm the process cache now and keep it fresh, so the first
        // hotkey press opens an already-populated panel.
        ProcessHelper.StartBackgroundRefresh(TimeSpan.FromSeconds(4));

        _tray?.ShowBalloonTip(2500, "QuickKill is running",
            $"Press {_hotkeyLabel} to open the kill panel. Right-click this icon to exit.",
            ToolTipIcon.Info);
    }

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon    = LoadTrayIcon(),
            Visible = true,
            Text    = $"QuickKill  ({_hotkeyLabel})"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add($"Kill an App  ({_hotkeyLabel})", null, (_, _) => ShowPanel());
        menu.Items.Add(new ToolStripSeparator());

        var startup = new ToolStripMenuItem("Start with Windows")
        {
            Checked      = IsStartupEnabled(),
            CheckOnClick = true
        };
        startup.CheckedChanged += (_, _) => SetStartupEnabled(startup.Checked);
        menu.Items.Add(startup);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => ShowPanel();
    }

    private static Icon LoadTrayIcon()
    {
        // Use our own exe's icon so QuickKill is recognizable in the tray.
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is not null)
            {
                var icon = Icon.ExtractAssociatedIcon(exe);
                if (icon is not null) return icon;
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    internal static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunKeyName) is not null;
    }

    internal static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled && Environment.ProcessPath is string exe)
                key.SetValue(RunKeyName, $"\"{exe}\"");
            else
                key.DeleteValue(RunKeyName, throwOnMissingValue: false);
        }
        catch { }
    }

    private void SetupHotkey()
    {
        // Zero-size invisible window provides an HWND to receive WM_HOTKEY
        _msgWindow = new Window
        {
            Width = 0, Height = 0,
            WindowStyle   = WindowStyle.None,
            ShowInTaskbar = false,
            Visibility    = Visibility.Hidden
        };
        _msgWindow.Show();
        _msgWindow.Hide();

        _hotkey = new HotkeyManager();
        _hotkey.HotkeyFired += (_, _) => ShowPanel();

        var saved = AppSettings.Load();
        bool ok = _hotkey.Register(_msgWindow, saved.HotkeyModifiers, saved.HotkeyVk);
        if (!ok)
        {
            // Saved combo is already claimed — fall back to Ctrl+Shift+Q
            ok = _hotkey.Register(_msgWindow, HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, 0x51);
            _hotkeyLabel = ok ? "Ctrl+Shift+Q" : "hotkey unavailable — use tray icon";
        }
        else
        {
            _hotkeyLabel = saved.HotkeyDisplay;
        }
    }

    internal static bool ChangeHotkey(uint modifiers, uint vk, string display)
    {
        if (Current is not App app) return false;
        bool ok = app._hotkey?.Reregister(modifiers, vk) ?? false;
        if (ok)
        {
            app._hotkeyLabel = display;
            if (app._tray is not null)
                app._tray.Text = $"QuickKill  ({display})";
        }
        else
        {
            // New combo is taken — restore the previously-saved one so the hotkey doesn't go dead
            var prev = AppSettings.Load();
            app._hotkey?.Reregister(prev.HotkeyModifiers, prev.HotkeyVk);
        }
        return ok;
    }

    internal static string CurrentHotkeyDisplay =>
        (Current as App)?._hotkeyLabel ?? "Ctrl+Alt+Q";

    private void ShowPanel() => _killWindow?.ShowAndRefresh();

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false; // remove icon immediately instead of lingering until hover
            _tray.Dispose();
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
