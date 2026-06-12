using System.IO;
using System.Text.Json;

namespace QuickKill;

public sealed class AppSettings
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickKill", "settings.json");

    public double PanelWidth      { get; set; } = 460;
    public double PanelHeight     { get; set; } = 560;
    public double BackgroundAlpha { get; set; } = 0.80;

    // Hotkey — stored as raw Win32 values so we can re-register on startup.
    public uint   HotkeyModifiers { get; set; } = 0x0003; // MOD_CONTROL | MOD_ALT
    public uint   HotkeyVk        { get; set; } = 0x51;   // Q
    public string HotkeyDisplay   { get; set; } = "Ctrl+Alt+Q";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
