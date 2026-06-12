using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace QuickKill;

public sealed class ProcessItem
{
    // Icon extraction hits the disk; cache per exe path so each app pays once.
    private static readonly ConcurrentDictionary<string, BitmapSource?> _iconCache = new();

    public int Pid { get; }
    public string Name { get; }
    public string Title { get; }
    public string Meta { get; }
    public BitmapSource? IconSource { get; }

    public ProcessItem(Process process)
    {
        Pid   = process.Id;
        Name  = process.ProcessName;
        Title = string.IsNullOrWhiteSpace(process.MainWindowTitle)
            ? process.ProcessName
            : process.MainWindowTitle;
        Meta  = $"{process.ProcessName}  ·  PID {process.Id}";
        IconSource = LoadIcon(process);
    }

    private static BitmapSource? LoadIcon(Process process)
    {
        string? path;
        try { path = process.MainModule?.FileName; }
        catch { return null; }
        if (path is null) return null;

        return _iconCache.GetOrAdd(path, ExtractIcon);
    }

    private static BitmapSource? ExtractIcon(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource  = stream;
            img.CacheOption   = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    public override string ToString() => Title;
}
