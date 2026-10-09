using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ClipboardHistory;

internal static class SelfCheck
{
    public static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ClipboardHistory-SelfCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
            var store = new HistoryStore(directory, () => now);
            store.AddOrUpdate("Alpha", "one.exe", now.AddMinutes(-2));
            store.AddOrUpdate("Beta", "two.exe", now.AddMinutes(-1));
            store.AddOrUpdate("Alpha", "three.exe", now);
            Ensure(store.Items.Count == 2, "完全相同的文本必须去重");
            Ensure(store.Items[0].Text == "Alpha" && store.Items[0].SourceApp == "three.exe", "重复文本必须更新时间、来源并置顶");
            Ensure(store.Search("alp").Count == 1, "搜索必须不区分大小写");

            store.AddOrUpdate("Expired", "old.exe", now.AddHours(-73));
            Ensure(store.CleanupExpired(now) == 1, "超过 72 小时的记录必须被清理");
            var reloaded = new HistoryStore(directory, () => now);
            Ensure(reloaded.Items.Count == 2, "历史记录必须能够持久化并重新加载");

            var settingsStore = new SettingsStore(directory);
            var settings = new AppSettings { ExcludedApps = ["KeePass.exe"] };
            settingsStore.Save(settings);
            Ensure(settingsStore.Load().ExcludedApps.SequenceEqual(["KeePass.exe"]), "排除应用设置必须能够持久化");
            var modifierOnlyHotKey = NativeMethods.ModWin | NativeMethods.ModifierFromKey(System.Windows.Input.Key.LeftShift);
            Ensure(NativeMethods.FormatHotKey(modifierOnlyHotKey, 0) == "Win+Shift",
                "必须能够表示只包含修饰键的快捷键");

            VerifyPaletteResources();
            VerifyUiContract(directory);

            Console.WriteLine("ClipboardHistory self-check: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ClipboardHistory self-check: FAIL - {exception.Message}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void VerifyPaletteResources()
    {
        var resources = new ResourceDictionary();

        App.ApplyMonolythPalette(resources, dark: true);
        Ensure(GetColor(resources, "MonolythBackgroundBrush") == ParseColor("#0B1011"), "深色背景必须使用批准色值");
        Ensure(GetColor(resources, "MonolythSurfaceBrush") == ParseColor("#131A1C"), "深色表面必须使用批准色值");
        Ensure(GetColor(resources, "MonolythPrimaryTextBrush") == ParseColor("#E8EEEE"), "深色主文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythSecondaryTextBrush") == ParseColor("#929E9D"), "深色次要文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythAccentBrush") == ParseColor("#45CDBB"), "深色强调色必须使用批准色值");

        App.ApplyMonolythPalette(resources, dark: false);
        Ensure(GetColor(resources, "MonolythBackgroundBrush") == ParseColor("#EEF2F2"), "浅色背景必须使用批准色值");
        Ensure(GetColor(resources, "MonolythSurfaceBrush") == ParseColor("#F7F9F9"), "浅色表面必须使用批准色值");
        Ensure(GetColor(resources, "MonolythPrimaryTextBrush") == ParseColor("#111819"), "浅色主文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythSecondaryTextBrush") == ParseColor("#667372"), "浅色次要文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythAccentBrush") == ParseColor("#087D72"), "浅色强调色必须使用批准色值");
    }

    private static void VerifyUiContract(string dataDirectory)
    {
        Ensure(MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Normal, false),
            "可见活动窗口应允许运行动效");
        Ensure(!MainWindow.ShouldRunAmbientMotion(false, true, true, WindowState.Normal, false),
            "截图模式必须关闭动效");
        Ensure(!MainWindow.ShouldRunAmbientMotion(true, false, true, WindowState.Normal, false),
            "系统关闭动画时必须静态显示");
        Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, false, WindowState.Normal, false),
            "隐藏窗口必须停止动效");
        Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Minimized, false),
            "最小化窗口必须停止动效");
        Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Normal, true),
            "暂停记录时必须停止动效");

        var keys = new[]
        {
            "ActionButtonStyle",
            "DangerButtonStyle",
            "CaptionButtonStyle",
            "TextFieldStyle",
            "ToggleStyle",
            "MonolythContextMenuStyle",
            "MonolythMenuItemStyle"
        };
        foreach (var key in keys)
        {
            Ensure(WpfApplication.Current.TryFindResource(key) is Style, $"缺少共享样式：{key}");
        }

        var actionStyle = (Style)WpfApplication.Current.FindResource("ActionButtonStyle");
        Ensure(actionStyle.Setters.OfType<Setter>().All(setter => setter.Property != UIElement.RenderTransformProperty),
            "主操作按钮不得使用缩放变换");
        Ensure(actionStyle.Triggers.OfType<EventTrigger>().All(trigger => trigger.Actions.Count == 0),
            "主操作按钮不得包含常驻或循环故事板");

        var mainWindow = new MainWindow(dataDirectory, manageStartup: false);
        try
        {
            var chrome = WindowChrome.GetWindowChrome(mainWindow);
            Ensure(chrome is { CaptionHeight: > 0 }
                   && chrome.ResizeBorderThickness.Left > 0
                   && chrome.ResizeBorderThickness.Top > 0,
                "主窗口必须使用可拖动和可缩放的 WindowChrome");
            Ensure(mainWindow.WindowStyle == WindowStyle.None && !mainWindow.AllowsTransparency,
                "自定义标题栏不得依赖透明窗口");
            Ensure(mainWindow.Width == 640 && mainWindow.Height == 700
                   && mainWindow.MinWidth >= 520 && mainWindow.MinHeight >= 560,
                "窗口尺寸必须符合批准规格且最小尺寸不得裁切底部操作");

            var captionStyle = (Style)WpfApplication.Current.FindResource("CaptionButtonStyle");
            foreach (var name in new[] { "MinimizeButton", "MaximizeButton", "CloseButton" })
            {
                Ensure(mainWindow.FindName(name) is WpfButton button
                       && ReferenceEquals(button.Style, captionStyle)
                       && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                    $"标题栏按钮必须存在、应用共享样式并提供自动化名称：{name}");
            }

            Ensure(mainWindow.FindName("CircuitField") is FrameworkElement { Effect: null },
                "静态线路层不得使用视觉效果");
            var signalLayer = mainWindow.FindName("SignalLayer") as Canvas;
            Ensure(signalLayer is { Effect: null },
                "信号层不得使用视觉效果");
            var signals = signalLayer!.Children.OfType<System.Windows.Shapes.Ellipse>().ToList();
            Ensure(signals.Count is > 0 and <= 4 && signals.All(signal => signal.Effect is null),
                "环境动效只能使用最多四个无特效信号点");

            var ambientStoryboard = (Storyboard)mainWindow.Resources["AmbientSignalStoryboard"];
            Ensure(ambientStoryboard.Duration.HasTimeSpan
                   && ambientStoryboard.Duration.TimeSpan.TotalSeconds is >= 14 and <= 18
                   && ambientStoryboard.RepeatBehavior == RepeatBehavior.Forever,
                "环境故事板必须以 14 至 18 秒低频循环");
            foreach (var timeline in ambientStoryboard.Children)
            {
                var property = Storyboard.GetTargetProperty(timeline)?.Path;
                var target = mainWindow.FindName(Storyboard.GetTargetName(timeline));
                Ensure(property == "Opacity" || target is TranslateTransform && property is "X" or "Y",
                    "环境故事板只能动画透明度和变换");
            }
            Ensure(VirtualizingPanel.GetIsVirtualizing(mainWindow.HistoryList)
                   && VirtualizingPanel.GetVirtualizationMode(mainWindow.HistoryList) == VirtualizationMode.Recycling,
                "历史列表必须使用回收虚拟化");

            var historyRow = (FrameworkElement)mainWindow.HistoryList.ItemTemplate.LoadContent();
            var historyVisuals = EnumerateVisualTree(historyRow).Prepend(historyRow).OfType<UIElement>().ToList();
            Ensure(historyVisuals.All(element => element.Effect is not DropShadowEffect and not BlurEffect),
                "历史行不得使用逐项阴影");
            var hasMotionTransform = historyRow.RenderTransform is ScaleTransform or TranslateTransform
                                     || historyRow.RenderTransform is TransformGroup transformGroup
                                     && transformGroup.Children.Cast<Transform>()
                                         .Any(transform => transform is ScaleTransform or TranslateTransform);
            Ensure(!hasMotionTransform,
                "历史行不得缩放或悬浮位移");

            Ensure(mainWindow.FindName("SearchBox") is WpfTextBox,
                "搜索输入框必须保留");
            Ensure(mainWindow.FindName("SearchPanel") is WpfPanel searchPanel
                   && searchPanel.Children.OfType<TextBlock>().All(text => text.Text != "搜索" || text.Visibility != Visibility.Visible),
                "搜索区不得保留独立的搜索文字标签");

            Ensure(mainWindow.FindName("StartupSettingsCard") is null
                   && mainWindow.FindName("ExcludedAppsCard") is null,
                "设置页不得保留嵌套大卡片");
            Ensure(mainWindow.FindName("StartupSectionTitle") is TextBlock { Text: "启动与快捷键" }
                   && mainWindow.FindName("ExcludedAppsSectionTitle") is TextBlock { Text: "排除应用" },
                "设置页必须保留清晰的分组标题");
            Ensure(mainWindow.FindName("ExcludedAppsEmptyState") is TextBlock,
                "排除应用列表必须提供空状态");

            var settingsScrollViewer = mainWindow.FindName("SettingsScrollViewer") as System.Windows.Controls.ScrollViewer;
            var saveSettingsButton = mainWindow.FindName("SaveSettingsButton") as WpfButton;
            Ensure(settingsScrollViewer is not null && saveSettingsButton is not null
                   && !IsVisualDescendantOf(saveSettingsButton, settingsScrollViewer),
                "保存设置操作必须固定在滚动区域之外");

            var hotKeyTextBox = mainWindow.FindName("HotKeyTextBox") as WpfTextBox;
            var hotKeyHandler = typeof(MainWindow).GetMethod(
                "HotKeyTextBox_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
            Ensure(hotKeyTextBox is not null
                   && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(hotKeyTextBox))
                   && hotKeyHandler is not null,
                "快捷键字段必须保留可访问名称和键盘捕获处理器");

            var excludedAppsList = mainWindow.FindName("ExcludedAppsListBox") as WpfListBox;
            var removeExcludedAppButton = mainWindow.FindName("RemoveExcludedAppButton") as WpfButton;
            Ensure(excludedAppsList is not null && removeExcludedAppButton is { Visibility: Visibility.Collapsed },
                "未选择排除项时移除操作必须隐藏");
            if (excludedAppsList!.Items.Count > 0)
            {
                excludedAppsList.SelectedIndex = 0;
                Ensure(removeExcludedAppButton!.Visibility == Visibility.Visible,
                    "选择排除项后必须显示移除操作");
                excludedAppsList.SelectedIndex = -1;
                Ensure(removeExcludedAppButton.Visibility == Visibility.Collapsed,
                    "取消选择后必须再次隐藏移除操作");
                excludedAppsList.SelectedIndex = 0;
                removeExcludedAppButton.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
                Ensure(mainWindow.FindName("ExcludedAppsEmptyState") is TextBlock { Visibility: Visibility.Visible }
                       && excludedAppsList.Visibility == Visibility.Collapsed,
                    "移除最后一个排除项后必须显示空状态");
            }
        }
        finally
        {
            mainWindow.PrepareForExit();
            mainWindow.Close();
        }
    }

    private static MediaColor GetColor(ResourceDictionary resources, string key) =>
        ((SolidColorBrush)resources[key]).Color;

    private static MediaColor ParseColor(string value) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value);

    private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in EnumerateVisualTree(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsVisualDescendantOf(DependencyObject child, DependencyObject ancestor)
    {
        for (var current = child; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }
}
