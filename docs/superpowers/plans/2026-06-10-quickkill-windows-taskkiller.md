# QuickKill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows tray app that shows a Force-Quit-style panel (summoned by Ctrl+Alt+Q) listing running apps with icons, and kills the selected one gracefully then by force.

**Architecture:** WPF app with no startup window; a hidden message window receives WM_HOTKEY; a borderless top-most dark popup lists visible processes; user picks one → CloseMainWindow() with 3s timeout → Kill(). NotifyIcon in system tray provides exit and fallback trigger.

**Tech Stack:** C# .NET 8, WPF, System.Windows.Forms.NotifyIcon (tray), user32.dll P/Invoke for RegisterHotKey, System.Diagnostics.Process, xUnit for business-logic tests, single-file self-contained publish to win-x64.

---

## File Map

| File | Responsibility |
|---|---|
| `QuickKill/QuickKill.csproj` | Project config — WPF + WinForms, manifest, single-file publish |
| `QuickKill/app.manifest` | UAC: requireAdministrator so we can kill elevated processes |
| `QuickKill/App.xaml` | Application entry (no StartupUri) |
| `QuickKill/App.xaml.cs` | Tray icon setup, hotkey wiring, KillWindow lifecycle |
| `QuickKill/ProcessItem.cs` | Model: PID, Name, Title, BitmapSource icon |
| `QuickKill/ProcessHelper.cs` | Enumerate visible windows, safety filter, graceful+force kill |
| `QuickKill/HotkeyManager.cs` | P/Invoke RegisterHotKey + WndProc hook via HwndSource |
| `QuickKill/KillWindow.xaml` | Borderless dark popup: ListBox + Kill button |
| `QuickKill/KillWindow.xaml.cs` | Load processes, keyboard nav, kill dispatch |
| `QuickKill.Tests/QuickKill.Tests.csproj` | xUnit test project |
| `QuickKill.Tests/ProcessHelperTests.cs` | Unit tests for safety filter and filtering logic |

---

### Task 1: Project scaffold

**Files:**
- Create: `QuickKill/QuickKill.csproj`
- Create: `QuickKill/app.manifest`
- Create: `QuickKill/App.xaml`
- Create: `QuickKill/App.xaml.cs`
- Create: `QuickKill.Tests/QuickKill.Tests.csproj`

- [ ] **Step 1: Create QuickKill.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <AssemblyName>QuickKill</AssemblyName>
    <RootNamespace>QuickKill</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create app.manifest**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="QuickKill.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

- [ ] **Step 3: Create App.xaml** (no StartupUri)

```xml
<Application x:Class="QuickKill.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources/>
</Application>
```

- [ ] **Step 4: Create App.xaml.cs stub**

```csharp
using System.Windows;
namespace QuickKill;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }
}
```

- [ ] **Step 5: Create QuickKill.Tests.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0"/>
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\QuickKill\QuickKill.csproj"/>
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Create solution and verify build** (run in the repo root, one level above both project folders)

```
dotnet new sln -n QuickKill
dotnet sln add QuickKill/QuickKill.csproj
dotnet sln add QuickKill.Tests/QuickKill.Tests.csproj
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git init && git add . && git commit -m "chore: scaffold QuickKill WPF project"
```

---

### Task 2: ProcessItem model

**Files:**
- Create: `QuickKill/ProcessItem.cs`
- Create: `QuickKill.Tests/ProcessHelperTests.cs` (stub)

- [ ] **Step 1: Write a stub test so the file compiles**

```csharp
// QuickKill.Tests/ProcessHelperTests.cs
using Xunit;
namespace QuickKill.Tests;
public class ProcessHelperTests
{
    [Fact]
    public void Placeholder() => Assert.True(true);
}
```

- [ ] **Step 2: Run — expect PASS**

```
dotnet test
```

- [ ] **Step 3: Create ProcessItem.cs**

```csharp
// QuickKill/ProcessItem.cs
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace QuickKill;

public sealed class ProcessItem
{
    public int Pid { get; }
    public string Name { get; }
    public string Title { get; }
    public BitmapSource? IconSource { get; }

    public ProcessItem(Process process)
    {
        Pid   = process.Id;
        Name  = process.ProcessName;
        Title = string.IsNullOrWhiteSpace(process.MainWindowTitle)
            ? process.ProcessName
            : process.MainWindowTitle;
        IconSource = LoadIcon(process);
    }

    private static BitmapSource? LoadIcon(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (path is null) return null;

            using var icon   = Icon.ExtractAssociatedIcon(path);
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
```

- [ ] **Step 4: Build**

```
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add QuickKill/ProcessItem.cs QuickKill.Tests/ProcessHelperTests.cs
git commit -m "feat: add ProcessItem model with icon loading"
```

---

### Task 3: ProcessHelper

**Files:**
- Create: `QuickKill/ProcessHelper.cs`
- Modify: `QuickKill.Tests/ProcessHelperTests.cs`

- [ ] **Step 1: Replace the test stub with real tests**

```csharp
// QuickKill.Tests/ProcessHelperTests.cs
using Xunit;
namespace QuickKill.Tests;

public class ProcessHelperTests
{
    [Theory]
    [InlineData("csrss",    true)]
    [InlineData("wininit",  true)]
    [InlineData("winlogon", true)]
    [InlineData("services", true)]
    [InlineData("lsass",    true)]
    [InlineData("smss",     true)]
    [InlineData("dwm",      true)]
    [InlineData("System",   true)]
    [InlineData("Registry", true)]
    [InlineData("Idle",     true)]
    [InlineData("CSRSS",    true)]
    [InlineData("chrome",   false)]
    [InlineData("notepad",  false)]
    [InlineData("explorer", false)]
    public void IsCriticalProcess_ReturnsExpected(string name, bool expected)
        => Assert.Equal(expected, ProcessHelper.IsCriticalProcess(name));

    [Fact]
    public void GetVisibleProcesses_ContainsNoCriticalProcesses()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var p in ProcessHelper.GetVisibleProcesses())
            Assert.False(ProcessHelper.IsCriticalProcess(p.Name),
                $"Critical process '{p.Name}' leaked into visible list");
    }

    [Fact]
    public void GetVisibleProcesses_AllItemsHaveTitle()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var p in ProcessHelper.GetVisibleProcesses())
            Assert.False(string.IsNullOrWhiteSpace(p.Title));
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (`ProcessHelper` not defined yet)

```
dotnet test
```

- [ ] **Step 3: Create ProcessHelper.cs**

```csharp
// QuickKill/ProcessHelper.cs
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

    public static bool IsCriticalProcess(string name) => _critical.Contains(name);

    public static IReadOnlyList<ProcessItem> GetVisibleProcesses() =>
        Process.GetProcesses()
            .Where(p =>
                p.MainWindowHandle != IntPtr.Zero &&
                !string.IsNullOrEmpty(p.MainWindowTitle) &&
                !IsCriticalProcess(p.ProcessName))
            .Select(p => { try { return new ProcessItem(p); } catch { return null; } })
            .Where(item => item is not null)
            .Cast<ProcessItem>()
            .OrderBy(p => p.Name)
            .ToList();

    public static async Task KillProcessAsync(ProcessItem item, CancellationToken ct = default)
    {
        try
        {
            var proc = Process.GetProcessById(item.Pid);
            proc.CloseMainWindow();
            var exited = await Task.Run(() => proc.WaitForExit(3000), ct);
            if (!exited) proc.Kill();
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```
dotnet test
```

- [ ] **Step 5: Commit**

```bash
git add QuickKill/ProcessHelper.cs QuickKill.Tests/ProcessHelperTests.cs
git commit -m "feat: add ProcessHelper with safety filter and async kill"
```

---

### Task 4: HotkeyManager

**Files:**
- Create: `QuickKill/HotkeyManager.cs`

- [ ] **Step 1: Create HotkeyManager.cs**

```csharp
// QuickKill/HotkeyManager.cs
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuickKill;

public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const int ID        = 0x1337;

    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;

    public event EventHandler? HotkeyFired;

    private HwndSource? _source;
    private IntPtr      _hwnd;
    private bool        _registered;

    public bool Register(Window msgWindow, uint modifiers, uint vk)
    {
        _hwnd    = new WindowInteropHelper(msgWindow).Handle;
        _source  = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_hwnd, ID, modifiers, vk);
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

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, ID);
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _registered = false;
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add QuickKill/HotkeyManager.cs
git commit -m "feat: add HotkeyManager with RegisterHotKey P/Invoke"
```

---

### Task 5: KillWindow UI

**Files:**
- Create: `QuickKill/KillWindow.xaml`
- Create: `QuickKill/KillWindow.xaml.cs`

- [ ] **Step 1: Create KillWindow.xaml**

```xml
<Window x:Class="QuickKill.KillWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="QuickKill"
        Width="440" Height="540"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        WindowStartupLocation="CenterScreen"
        KeyDown="Window_KeyDown"
        Deactivated="Window_Deactivated">

    <Window.Effect>
        <DropShadowEffect BlurRadius="20" ShadowDepth="0" Opacity="0.6" Color="Black"/>
    </Window.Effect>

    <Border Background="#1C1C1E" CornerRadius="12" BorderBrush="#3A3A3C" BorderThickness="1">
        <DockPanel>

            <!-- Header -->
            <Border DockPanel.Dock="Top" Background="#2C2C2E" CornerRadius="12,12,0,0" Padding="18,14">
                <Grid>
                    <TextBlock Text="Kill Application" Foreground="#F2F2F7" FontSize="15" FontWeight="SemiBold"/>
                    <TextBlock Text="Ctrl+Alt+Q" Foreground="#636366" FontSize="11"
                               HorizontalAlignment="Right" VerticalAlignment="Center"/>
                </Grid>
            </Border>

            <!-- Hint -->
            <TextBlock DockPanel.Dock="Top"
                       Text="Select an app and press Enter, or click Force Kill."
                       Foreground="#636366" FontSize="11" Margin="18,10,18,4"/>

            <!-- Kill button -->
            <Button x:Name="KillButton"
                    DockPanel.Dock="Bottom"
                    Content="Force Kill"
                    Height="40" Margin="18,8,18,18"
                    FontSize="13" FontWeight="SemiBold"
                    Cursor="Hand" IsEnabled="False"
                    Click="KillButton_Click">
                <Button.Style>
                    <Style TargetType="Button">
                        <Setter Property="Background" Value="#3A3A3C"/>
                        <Setter Property="Foreground" Value="#636366"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="Button">
                                    <Border Background="{TemplateBinding Background}" CornerRadius="8">
                                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                        <Style.Triggers>
                            <Trigger Property="IsEnabled" Value="True">
                                <Setter Property="Background" Value="#FF3B30"/>
                                <Setter Property="Foreground" Value="White"/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
            </Button>

            <!-- Process list -->
            <ListBox x:Name="ProcessList"
                     Background="Transparent" BorderThickness="0" Margin="8,0"
                     VirtualizingPanel.IsVirtualizing="True"
                     ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                     SelectionChanged="ProcessList_SelectionChanged">
                <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                        <Setter Property="Padding" Value="10,7"/>
                        <Setter Property="Foreground" Value="#F2F2F7"/>
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="ListBoxItem">
                                    <Border Background="{TemplateBinding Background}" CornerRadius="6" Margin="0,1">
                                        <ContentPresenter Margin="{TemplateBinding Padding}"/>
                                    </Border>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsSelected" Value="True">
                                            <Setter Property="Background" Value="#0A84FF"/>
                                        </Trigger>
                                        <MultiTrigger>
                                            <MultiTrigger.Conditions>
                                                <Condition Property="IsMouseOver" Value="True"/>
                                                <Condition Property="IsSelected" Value="False"/>
                                            </MultiTrigger.Conditions>
                                            <Setter Property="Background" Value="#3A3A3C"/>
                                        </MultiTrigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                            <Image Source="{Binding IconSource}" Width="18" Height="18" Margin="0,0,10,0"
                                   RenderOptions.BitmapScalingMode="HighQuality"/>
                            <TextBlock Text="{Binding Title}" VerticalAlignment="Center"
                                       FontSize="12" TextTrimming="CharacterEllipsis"/>
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>

        </DockPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Create KillWindow.xaml.cs**

```csharp
// QuickKill/KillWindow.xaml.cs
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickKill;

public partial class KillWindow : Window
{
    public ObservableCollection<ProcessItem> Processes { get; } = new();

    public KillWindow()
    {
        InitializeComponent();
        ProcessList.ItemsSource = Processes;
    }

    public void ShowAndRefresh()
    {
        Processes.Clear();
        foreach (var item in ProcessHelper.GetVisibleProcesses())
            Processes.Add(item);

        if (Processes.Count > 0)
            ProcessList.SelectedIndex = 0;

        Show();
        Activate();
        ProcessList.Focus();
    }

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        KillButton.IsEnabled = ProcessList.SelectedItem is not null;
        ProcessList.ScrollIntoView(ProcessList.SelectedItem);
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessList.SelectedItem is not ProcessItem selected) return;
        Hide();
        await ProcessHelper.KillProcessAsync(selected);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide(); e.Handled = true; break;
            case Key.Enter:
                KillButton_Click(sender, new RoutedEventArgs()); e.Handled = true; break;
            case Key.Up:
                if (ProcessList.SelectedIndex > 0) ProcessList.SelectedIndex--;
                e.Handled = true; break;
            case Key.Down:
                if (ProcessList.SelectedIndex < ProcessList.Items.Count - 1) ProcessList.SelectedIndex++;
                e.Handled = true; break;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();
}
```

- [ ] **Step 3: Build**

```
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add QuickKill/KillWindow.xaml QuickKill/KillWindow.xaml.cs
git commit -m "feat: add KillWindow — dark borderless process picker"
```

---

### Task 6: App bootstrap — full wiring

**Files:**
- Modify: `QuickKill/App.xaml.cs`

- [ ] **Step 1: Replace App.xaml.cs**

```csharp
// QuickKill/App.xaml.cs
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace QuickKill;

public partial class App : Application
{
    private NotifyIcon?    _tray;
    private KillWindow?    _killWindow;
    private HotkeyManager? _hotkey;
    private Window?        _msgWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _killWindow = new KillWindow();
        SetupTray();
        SetupHotkey();
    }

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon    = SystemIcons.Application,
            Visible = true,
            Text    = "QuickKill  (Ctrl+Alt+Q)"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Kill an App  (Ctrl+Alt+Q)", null, (_, _) => ShowPanel());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => ShowPanel();
    }

    private void SetupHotkey()
    {
        // Zero-size invisible window — provides an HWND to receive WM_HOTKEY
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

        // Ctrl+Alt+Q; fall back to Ctrl+Shift+Q if already taken
        bool ok = _hotkey.Register(_msgWindow,
                      HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT, 0x51);
        if (!ok)
            _hotkey.Register(_msgWindow,
                HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, 0x51);
    }

    private void ShowPanel() => _killWindow?.ShowAndRefresh();

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: Build + test**

```
dotnet build && dotnet test
```
Expected: build and all tests pass.

- [ ] **Step 3: Commit**

```bash
git add QuickKill/App.xaml.cs
git commit -m "feat: wire tray icon, hotkey, and KillWindow lifecycle"
```

---

### Task 7: Smoke test + publish

- [ ] **Step 1: Run on Windows**

```
dotnet run --project QuickKill
```

Expected behavior:
- UAC prompt (admin elevation)
- No window — only a tray icon in system tray
- Ctrl+Alt+Q → dark panel appears, centered, listing running apps
- ↑↓ to navigate, Enter to kill, Esc to dismiss
- Tray right-click → "Exit" cleanly closes

- [ ] **Step 2: Kill flow test**

1. Open Notepad
2. Press Ctrl+Alt+Q
3. Select "Notepad" in the list
4. Press Enter
5. Notepad closes within 3 seconds

- [ ] **Step 3: Publish single .exe**

```
dotnet publish QuickKill -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```
Expected: `publish/QuickKill.exe` (~60-80 MB)

- [ ] **Step 4: Final commit**

```bash
git add publish/ --force
git commit -m "feat: QuickKill v1 complete — tray + hotkey + force-quit panel"
```

---

## Known Limitations / v2 items

| Issue | Fix |
|---|---|
| UAC prompt on every launch | Task Scheduler entry: "Run with highest privileges", trigger at login |
| Generic tray icon | Add `tray.ico` as embedded resource, reference in `App.xaml.cs` |
| Hotkey won't fire on secure desktop (Ctrl+Alt+Del screen) | Windows UIPI limit — no workaround |
| No hotkey config | Read from `appsettings.json` next to the .exe |
| Unsigned → SmartScreen warning | Sign with EV/OV code-signing cert for distribution |
