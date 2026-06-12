using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuickKill;

public partial class KillWindow : Window
{
    private readonly ObservableCollection<ProcessItem> _visible = new();
    private List<ProcessItem> _all = new();
    private int _refreshGeneration;
    private AppSettings _settings = AppSettings.Load();
    private ScrollViewer? _listScrollViewer;
    private bool _recordingHotkey;
    private DateTime _lastShowTime = DateTime.MinValue;

    public KillWindow()
    {
        InitializeComponent();
        ProcessList.ItemsSource = _visible;
        Loaded += (_, _) =>
        {
            // Cache the ListBox's inner ScrollViewer once rendered.
            _listScrollViewer = FindScrollViewer(ProcessList);
            ApplySettings();
        };
    }

    // ─── Show / refresh ───────────────────────────────────────────────────────

    public void ShowAndRefresh()
    {
        // Toggle: if already visible, just hide.
        if (IsVisible) { Hide(); return; }

        _all = ProcessHelper.Cached.ToList();
        SearchBox.Text = "";
        ApplyFilter();

        // Re-center on every show so it's never off-screen.
        var screen = SystemParameters.WorkArea;
        ApplySettings();
        Left = (screen.Width  - Width)  / 2 + screen.Left;
        Top  = (screen.Height - Height) / 2 + screen.Top;

        // No transform/slide animation — layered transparent windows with DropShadowEffect
        // render in software, and animating them causes visible stutter. Show instantly.
        _lastShowTime = DateTime.UtcNow;   // timestamp guard — Deactivated won't hide for 600 ms
        BeginAnimation(OpacityProperty, null); // clear any stale animation holding Opacity
        Opacity = 1;
        Show();
        Activate();
        SearchBox.Focus();

        _ = RefreshInBackgroundAsync();
    }

    private async Task RefreshInBackgroundAsync()
    {
        var generation = ++_refreshGeneration;
        var fresh = await Task.Run(() => ProcessHelper.Refresh().ToList());
        if (generation != _refreshGeneration || !IsVisible) return;

        // Skip the rebuild entirely if nothing changed — a full Clear+Add of the
        // ObservableCollection repaints the whole list and looks like a blink.
        bool same = fresh.Count == _all.Count &&
                    fresh.Select(p => (p.Pid, p.Title))
                         .SequenceEqual(_all.Select(p => (p.Pid, p.Title)));
        if (same) return;

        var selectedPid = (ProcessList.SelectedItem as ProcessItem)?.Pid;
        _all = fresh;
        ApplyFilter(keepSelection: selectedPid);
    }

    // ─── Filter / selection ───────────────────────────────────────────────────

    private void ApplyFilter(int? keepSelection = null)
    {
        var q = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(p =>
                p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
              .ToList();

        _visible.Clear();
        foreach (var item in filtered) _visible.Add(item);

        CountLabel.Text = $"{_all.Count} app{(_all.Count == 1 ? "" : "s")}";

        var empty = _visible.Count == 0;
        ProcessList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility  = empty ? Visibility.Visible   : Visibility.Collapsed;
        EmptyState.Text = string.IsNullOrEmpty(q) ? "No apps running" : $"No results for \"{q}\"";

        ClearButton.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Collapsed : Visibility.Visible;

        if (_visible.Count > 0)
        {
            var restored = keepSelection is int pid
                ? _visible.FirstOrDefault(p => p.Pid == pid)
                : null;
            ProcessList.SelectedItem = restored;
            if (restored is null) ProcessList.SelectedIndex = 0;
        }

        UpdateKillButton();
    }

    private void UpdateKillButton()
    {
        var on = ProcessList.SelectedItem is not null;
        KillButton.IsEnabled = on;
        EnterHint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── Search box ───────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Navigate the list while keeping the cursor in the search box.
        if (_visible.Count == 0) return;
        switch (e.Key)
        {
            case Key.Down:
                if (ProcessList.SelectedIndex < _visible.Count - 1)
                    ProcessList.SelectedIndex++;
                ProcessList.ScrollIntoView(ProcessList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                if (ProcessList.SelectedIndex > 0)
                    ProcessList.SelectedIndex--;
                ProcessList.ScrollIntoView(ProcessList.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateKillButton();
        if (ProcessList.SelectedItem is not null)
            ProcessList.ScrollIntoView(ProcessList.SelectedItem);
    }

    // ─── Kill button ──────────────────────────────────────────────────────────

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessList.SelectedItem is not ProcessItem selected) return;
        Hide();
        await ProcessHelper.KillProcessAsync(selected);
    }

    // ─── Scroll buttons ───────────────────────────────────────────────────────

    private void ScrollUpBtn_Click(object sender, RoutedEventArgs e)
    {
        var sv = _listScrollViewer ??= FindScrollViewer(ProcessList);
        sv?.ScrollToVerticalOffset(sv.VerticalOffset - 60);
    }

    private void ScrollDownBtn_Click(object sender, RoutedEventArgs e)
    {
        var sv = _listScrollViewer ??= FindScrollViewer(ProcessList);
        sv?.ScrollToVerticalOffset(sv.VerticalOffset + 60);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject d)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var child = VisualTreeHelper.GetChild(d, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    // ─── Settings ─────────────────────────────────────────────────────────────

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsPopup.IsOpen) { SettingsPopup.IsOpen = false; return; }

        // Sync sliders, hotkey badge, and startup toggle to current values before opening.
        WidthSlider.Value        = _settings.PanelWidth;
        HeightSlider.Value       = _settings.PanelHeight;
        OpacitySlider.Value      = _settings.BackgroundAlpha;
        HotkeyBadge.Text        = _settings.HotkeyDisplay;
        StartupToggle.IsChecked  = App.IsStartupEnabled();

        SettingsPopup.IsOpen = true;
    }

    private void ChangeHotkeyBtn_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
        HotkeyCapture.Text = "—";
        HotkeyOverlay.Visibility = Visibility.Visible;
        _recordingHotkey = true;
        Focus();
    }

    private async void HandleHotkeyRecording(KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // If only modifiers are held down, preview what's pressed so far
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                 or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Tab)
        {
            var preview = BuildHotkeyDisplay(Keyboard.Modifiers, null);
            HotkeyCapture.Text = string.IsNullOrEmpty(preview) ? "—" : preview + " + ...";
            return;
        }

        if (key == Key.Escape)
        {
            ExitRecordingMode();
            return;
        }

        var mods = Keyboard.Modifiers;
        uint winMod = 0;
        if (mods.HasFlag(ModifierKeys.Control)) winMod |= HotkeyManager.MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Alt))     winMod |= HotkeyManager.MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Shift))   winMod |= HotkeyManager.MOD_SHIFT;

        uint vk      = (uint)KeyInterop.VirtualKeyFromKey(key);
        string label = BuildHotkeyDisplay(mods, key);

        bool ok = App.ChangeHotkey(winMod, vk, label);
        if (ok)
        {
            _settings.HotkeyModifiers = winMod;
            _settings.HotkeyVk        = vk;
            _settings.HotkeyDisplay   = label;
            _settings.Save();
            ExitRecordingMode();
        }
        else
        {
            HotkeyCapture.Text = "Already in use — try another";
            await Task.Delay(1800);
            if (_recordingHotkey) { HotkeyCapture.Text = "—"; }
        }
    }

    private void ExitRecordingMode()
    {
        _recordingHotkey = false;
        HotkeyOverlay.Visibility = Visibility.Collapsed;
    }

    private static string BuildHotkeyDisplay(ModifierKeys mods, Key? key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (key.HasValue) parts.Add(key.Value.ToString());
        return string.Join("+", parts);
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        App.SetStartupEnabled(StartupToggle.IsChecked == true);
    }

    private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{exe}\"");
    }

    private void AttributionBtn_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "https://github.com/anshmajumdar121",
            UseShellExecute = true
        });
    }

    private void AttributionLock_Click(object sender, RoutedEventArgs e)
    {
        // Password protection: SHA-256("263153@Maj") = 4b5328ff06f7ec3597d76e38f06444f7cb1d763d6c77e46f57a5edaed1c3c9ef
        // Attribution is hardcoded — this overlay just confirms it cannot be changed without the key.
        var dlg = new AttributionPasswordDialog();
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        Width = e.NewValue;
        _settings.PanelWidth = e.NewValue;
        _settings.Save();
    }

    private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        Height = e.NewValue;
        _settings.PanelHeight = e.NewValue;
        _settings.Save();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        ApplyBackgroundAlpha(e.NewValue);
        _settings.BackgroundAlpha = e.NewValue;
        _settings.Save();
    }

    private void ApplySettings()
    {
        Width  = _settings.PanelWidth;
        Height = _settings.PanelHeight;
        ApplyBackgroundAlpha(_settings.BackgroundAlpha);
    }

    private void ApplyBackgroundAlpha(double alpha)
    {
        byte a = (byte)(alpha * 255);
        GlassBase.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, 0x1C, 0x1C, 0x1E));
    }

    // ─── Title-bar window controls ────────────────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    // ─── Keyboard ─────────────────────────────────────────────────────────────

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingHotkey)
        {
            HandleHotkeyRecording(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                if (SettingsPopup.IsOpen) SettingsPopup.IsOpen = false;
                else Hide();
                e.Handled = true;
                break;
            case Key.Enter when KillButton.IsEnabled:
                KillButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    // ─── Focus / deactivated ──────────────────────────────────────────────────

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Don't hide within 600 ms of showing — Activated/Deactivated race during hotkey open.
        if ((DateTime.UtcNow - _lastShowTime).TotalMilliseconds < 600) return;
        // Don't hide while the settings popup is open (it takes focus momentarily).
        if (SettingsPopup.IsOpen) return;
        Hide();
    }
}
