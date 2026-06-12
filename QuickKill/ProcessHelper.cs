using System.ComponentModel;
using System.Diagnostics;

namespace QuickKill;

public static class ProcessHelper
{
    private static readonly HashSet<string> _critical =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "csrss", "wininit", "winlogon", "services", "lsass",
            "smss", "dwm", "System", "Registry", "Idle"
        };

    // Snapshot refreshed in the background so the panel can open instantly.
    private static volatile IReadOnlyList<ProcessItem> _cache = Array.Empty<ProcessItem>();

    public static IReadOnlyList<ProcessItem> Cached => _cache;

    public static void StartBackgroundRefresh(TimeSpan interval)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try { _cache = GetVisibleProcesses(); }
                catch { /* never let the refresh loop die */ }
                await Task.Delay(interval);
            }
        });
    }

    public static IReadOnlyList<ProcessItem> Refresh()
    {
        var fresh = GetVisibleProcesses();
        _cache = fresh;
        return fresh;
    }

    public static bool IsCriticalProcess(string name) => _critical.Contains(name);

    public static IReadOnlyList<ProcessItem> GetVisibleProcesses()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes
                .Where(p =>
                    p.MainWindowHandle != IntPtr.Zero &&
                    !string.IsNullOrEmpty(p.MainWindowTitle) &&
                    !IsCriticalProcess(p.ProcessName))
                .Select(p => { try { return new ProcessItem(p); } catch { return null; } })
                .Where(item => item is not null)
                .Cast<ProcessItem>()
                .OrderBy(p => p.Name)
                .ToList();
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    public static async Task KillProcessAsync(ProcessItem item, CancellationToken ct = default)
    {
        try
        {
            using var proc = Process.GetProcessById(item.Pid);
            proc.CloseMainWindow();
            var exited = await Task.Run(() => proc.WaitForExit(3000), ct);
            if (!exited) proc.Kill();
        }
        catch (ArgumentException) { }         // process already gone
        catch (InvalidOperationException) { } // process exited mid-call
        catch (Win32Exception) { }            // access denied (elevated/protected process)
    }
}
