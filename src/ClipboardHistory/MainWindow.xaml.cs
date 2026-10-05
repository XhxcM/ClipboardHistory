using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;

namespace ClipboardHistory;

public partial class MainWindow : Window
{
    private const int MaxTextBytes = 1024 * 1024;
    private readonly HistoryStore _history;
    private readonly SettingsStore _settingsStore;
    private readonly DispatcherTimer _cleanupTimer;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseMenuItem;
    private readonly HashSet<string> _pendingExcludedApps = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings;
    private IntPtr _windowHandle;
    private HwndSource? _windowSource;
    private bool _allowClose;
    private bool _captureRunning;
    private bool _captureQueued;
    private string _queuedSourceApp = "未知应用";
    private bool _hotKeyRegistered;
    private int _pendingHotKeyModifiers;
    private int _pendingHotKeyVirtualKey;

    public event EventHandler? ExitRequested;

    public MainWindow(string? dataDirectory = null, bool manageStartup = true)
    {
        InitializeComponent();

        dataDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardHistory");
        _history = new HistoryStore(dataDirectory);
        _settingsStore = new SettingsStore(dataDirectory);
        _settings = _settingsStore.Load();
        _pendingHotKeyModifiers = _settings.HotKeyModifiers;
        _pendingHotKeyVirtualKey = _settings.HotKeyVirtualKey;

        _pauseMenuItem = new Forms.ToolStripMenuItem();
        _pauseMenuItem.Click += (_, _) => Dispatcher.Invoke(TogglePaused);
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("打开", null, (_, _) => Dispatcher.Invoke(ShowAndActivate));
        trayMenu.Items.Add(_pauseMenuItem);
        trayMenu.Items.Add("清空全部", null, (_, _) => Dispatcher.Invoke(ClearHistory));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "剪贴历史",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowAndActivate);

        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _cleanupTimer.Tick += (_, _) =>
        {
            if (_history.CleanupExpired() > 0)
            {
                RefreshHistory();
            }
        };
        _cleanupTimer.Start();

        SourceInitialized += MainWindow_SourceInitialized;
        UpdatePauseUi();
        LoadSettingsControls();
        RefreshHistory();

        try
        {
            if (manageStartup)
            {
                StartupManager.SetEnabled(_settings.StartWithWindows);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            SetStatus("无法更新开机启动设置");
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(_windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        if (!NativeMethods.AddClipboardFormatListener(_windowHandle))
        {
            SetStatus("无法监听剪贴板变化");
        }

        if (!TryRegisterHotKey(_settings.HotKeyModifiers, _settings.HotKeyVirtualKey))
        {
            SetStatus("快捷键已被占用，请在设置中修改");
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmClipboardUpdate)
        {
            _queuedSourceApp = NativeMethods.GetForegroundProcessName();
            _captureQueued = true;
            _ = CaptureQueuedClipboardAsync();
            handled = true;
        }
        else if (message == NativeMethods.WmHotKey && wParam.ToInt32() == NativeMethods.HotKeyId)
        {
            ShowAndActivate();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private async Task CaptureQueuedClipboardAsync()
    {
        if (_captureRunning)
        {
            return;
        }

        _captureRunning = true;
        try
        {
            while (_captureQueued)
            {
                _captureQueued = false;
                var sourceApp = _queuedSourceApp;
                if (_settings.IsPaused || _settings.ExcludedApps.Contains(sourceApp, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? text = null;
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        if (WpfClipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
                        {
                            text = WpfClipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
                        }
                        break;
                    }
                    catch (Exception exception) when (exception is COMException or System.Runtime.InteropServices.ExternalException)
                    {
                        await Task.Delay(35 * attempt);
                    }
                }

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
                {
                    SetStatus("已跳过超过 1 MB 的文本");
                    continue;
                }

                try
                {
                    _history.AddOrUpdate(text, sourceApp);
                    RefreshHistory();
                    SetStatus($"已记录 · {_history.Items.Count} 条");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    SetStatus("记录保存失败，请检查本地数据目录");
                }
            }
        }
        finally
        {
            _captureRunning = false;
        }
    }

    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        SearchBox.Focus();
        SearchBox.SelectAll();
        _history.CleanupExpired();
        RefreshHistory();
    }

    public void PrepareForExit()
    {
        if (_allowClose)
        {
            return;
        }

        _allowClose = true;
        _cleanupTimer.Stop();
        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_windowHandle);
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotKeyId);
        }
        _windowSource?.RemoveHook(WindowMessageHook);
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void RefreshHistory()
    {
        var selectedText = (HistoryList.SelectedItem as HistoryItem)?.Text;
        var items = _history.Search(SearchBox.Text);
        HistoryList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (selectedText is not null)
        {
            HistoryList.SelectedItem = items.FirstOrDefault(item => item.Text == selectedText);
        }
    }

    private void CopySelected()
    {
        if (HistoryList.SelectedItem is not HistoryItem item)
        {
            return;
        }

        try
        {
            WpfClipboard.SetText(item.Text, System.Windows.TextDataFormat.UnicodeText);
            Hide();
        }
        catch (Exception exception) when (exception is COMException or System.Runtime.InteropServices.ExternalException)
        {
            SetStatus("剪贴板正被其他程序占用，请重试");
        }
    }

    private void TogglePaused()
    {
        _settings.IsPaused = !_settings.IsPaused;
        _settingsStore.Save(_settings);
        UpdatePauseUi();
    }

    private void UpdatePauseUi()
    {
        PauseButton.Content = _settings.IsPaused ? "恢复记录" : "暂停记录";
        _pauseMenuItem.Text = _settings.IsPaused ? "恢复记录" : "暂停记录";
        SetStatus(_settings.IsPaused ? "记录已暂停" : $"正在记录 · {_history.Items.Count} 条");
    }

    private void ClearHistory()
    {
        if (_history.Items.Count == 0)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(this, "确定清空全部剪贴板历史吗？此操作无法撤销。", "清空历史",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            _history.Clear();
            RefreshHistory();
            SetStatus("历史记录已清空");
        }
    }

    private bool TryRegisterHotKey(int modifiers, int virtualKey)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (_hotKeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotKeyId);
        }
        _hotKeyRegistered = NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.HotKeyId, modifiers, virtualKey);
        return _hotKeyRegistered;
    }

    private void LoadSettingsControls()
    {
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        _pendingHotKeyModifiers = _settings.HotKeyModifiers;
        _pendingHotKeyVirtualKey = _settings.HotKeyVirtualKey;
        HotKeyTextBox.Text = NativeMethods.FormatHotKey(_pendingHotKeyModifiers, _pendingHotKeyVirtualKey);
        _pendingExcludedApps.Clear();
        foreach (var app in _settings.ExcludedApps)
        {
            _pendingExcludedApps.Add(app);
        }
        RefreshExcludedApps();
    }

    private void RefreshExcludedApps()
    {
        ExcludedAppsListBox.ItemsSource = _pendingExcludedApps.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                BackToHistory_Click(sender, e);
            }
            else
            {
                Hide();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SettingsPanel.Visibility != Visibility.Visible)
        {
            CopySelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshHistory();
    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CopySelected();
    private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => CopySelected();

    private void HistoryList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }
        if (element is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
        {
            _history.Delete(item);
            RefreshHistory();
        }
    }

    private void ExcludeSourceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryItem item || item.SourceApp == "未知应用")
        {
            return;
        }

        if (!_settings.ExcludedApps.Contains(item.SourceApp, StringComparer.OrdinalIgnoreCase))
        {
            _settings.ExcludedApps.Add(item.SourceApp);
            _settingsStore.Save(_settings);
            SetStatus($"以后不再记录 {item.SourceApp}");
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePaused();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearHistory();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        LoadSettingsControls();
        HistoryPanel.Visibility = Visibility.Collapsed;
        HistoryFooter.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
    }

    internal void ShowSettingsForPreview() => OpenSettings_Click(this, new RoutedEventArgs());

    private void BackToHistory_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Visible;
        HistoryFooter.Visibility = Visibility.Visible;
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
    }

    private void HotKeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var pressedModifier = NativeMethods.ModifierFromKey(key);

        var modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.ModControl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.ModAlt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.ModShift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.ModWin;
        if (NativeMethods.IsWindowsKeyDown()) modifiers |= NativeMethods.ModWin;
        modifiers |= pressedModifier;
        if (pressedModifier != 0)
        {
            if ((modifiers & (modifiers - 1)) == 0)
            {
                return;
            }

            _pendingHotKeyModifiers = modifiers;
            // ponytail: Windows 11 已验证空主键注册；若 Windows 10 拒绝，则要求增加普通键。
            _pendingHotKeyVirtualKey = 0;
            HotKeyTextBox.Text = NativeMethods.FormatHotKey(modifiers, 0);
            return;
        }
        if (modifiers == 0)
        {
            SetStatus("快捷键必须包含 Ctrl、Alt、Shift 或 Win");
            return;
        }

        _pendingHotKeyModifiers = modifiers;
        _pendingHotKeyVirtualKey = KeyInterop.VirtualKeyFromKey(key);
        HotKeyTextBox.Text = NativeMethods.FormatHotKey(modifiers, _pendingHotKeyVirtualKey);
    }

    private void AddExcludedApp_Click(object sender, RoutedEventArgs e)
    {
        var app = Path.GetFileName(ExcludedAppTextBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(app) || app.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            SetStatus("请输入有效的程序名，例如 KeePass.exe");
            return;
        }
        if (!app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            app += ".exe";
        }
        _pendingExcludedApps.Add(app);
        ExcludedAppTextBox.Clear();
        RefreshExcludedApps();
    }

    private void RemoveExcludedApp_Click(object sender, RoutedEventArgs e)
    {
        if (ExcludedAppsListBox.SelectedItem is string app)
        {
            _pendingExcludedApps.Remove(app);
            RefreshExcludedApps();
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var oldModifiers = _settings.HotKeyModifiers;
        var oldVirtualKey = _settings.HotKeyVirtualKey;
        if ((_pendingHotKeyModifiers != oldModifiers || _pendingHotKeyVirtualKey != oldVirtualKey)
            && !TryRegisterHotKey(_pendingHotKeyModifiers, _pendingHotKeyVirtualKey))
        {
            TryRegisterHotKey(oldModifiers, oldVirtualKey);
            System.Windows.MessageBox.Show(this, "这个快捷键已被其他程序占用，请换一个组合。", "快捷键不可用",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.HotKeyModifiers = _pendingHotKeyModifiers;
        _settings.HotKeyVirtualKey = _pendingHotKeyVirtualKey;
        _settings.ExcludedApps = _pendingExcludedApps.Order(StringComparer.OrdinalIgnoreCase).ToList();
        try
        {
            StartupManager.SetEnabled(_settings.StartWithWindows);
            _settingsStore.Save(_settings);
            SetStatus("设置已保存");
            BackToHistory_Click(sender, e);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            SetStatus("设置保存失败，请检查本地数据目录");
        }
    }
}
