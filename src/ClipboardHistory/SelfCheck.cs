using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
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
        #pragma warning disable WPF0001
        Ensure(WpfApplication.Current.ThemeMode == ThemeMode.Dark,
            "应用必须固定使用深色主题，不得跟随系统切换到浅色");
        #pragma warning restore WPF0001
        var activeBackground = GetColor(WpfApplication.Current.Resources, "MonolythBackgroundBrush");
        Ensure(activeBackground.R <= 8 && activeBackground.G <= 8 && activeBackground.B <= 10,
            "应用启动时必须直接使用近黑背景");

        var resources = new ResourceDictionary();

        App.ApplyMonolythPalette(resources);
        var darkBackground = GetColor(resources, "MonolythBackgroundBrush");
        var darkSurface = GetColor(resources, "MonolythSurfaceBrush");
        Ensure(darkBackground.R <= 8 && darkBackground.G <= 8 && darkBackground.B <= 10,
            "深色背景必须接近黑色");
        Ensure(darkSurface.R > darkBackground.R && darkSurface.G > darkBackground.G && darkSurface.B > darkBackground.B
            && darkSurface.R <= 16 && darkSurface.G <= 20 && darkSurface.B <= 22,
            "深色表面必须在近黑背景上保持轻微分层");
        Ensure(GetColor(resources, "MonolythPrimaryTextBrush") == ParseColor("#E8EEEE"), "深色主文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythSecondaryTextBrush") == ParseColor("#929E9D"), "深色次要文字必须使用批准色值");
        Ensure(GetColor(resources, "MonolythAccentBrush") == ParseColor("#45CDBB"), "深色强调色必须使用批准色值");

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
        VerifyActionButtonMotion(actionStyle);

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

            Ensure(mainWindow.FindName("CircuitField") is Viewbox { Effect: null } circuitField
                   && circuitField.Child is Canvas { Height: >= 640 } circuitCanvas
                   && circuitCanvas.Children.OfType<System.Windows.Shapes.Path>().Count() >= 12,
                "背景必须提供覆盖全窗口的透视电路平面");
            var signalLayer = mainWindow.FindName("SignalLayer") as Canvas;
            Ensure(signalLayer is { Height: >= 640, Effect: null },
                "发光信号层必须覆盖全窗口且不得使用全层视觉效果");
            var signals = signalLayer!.Children.OfType<FrameworkElement>()
                .Where(element => element.Name.StartsWith("Signal", StringComparison.Ordinal)
                                  && element.RenderTransform is TranslateTransform)
                .ToList();
            Ensure(signals.Count is >= 8 and <= 10 && signals.All(signal => signal.Effect is null),
                "环境动效必须使用八至十个低开销发光信号点");
            Ensure(signals.All(signal => signal.Width >= 10 && signal.Height >= 10),
                "环境信号点必须包含清晰可见的多层光晕");

            var ambientStoryboard = (Storyboard)mainWindow.Resources["AmbientSignalStoryboard"];
            Ensure(ambientStoryboard.Duration.HasTimeSpan
                   && ambientStoryboard.Duration.TimeSpan.TotalSeconds is >= 14 and <= 18
                   && ambientStoryboard.RepeatBehavior == RepeatBehavior.Forever,
                "环境故事板必须以 14 至 18 秒低频循环");
            Ensure(Timeline.GetDesiredFrameRate(ambientStoryboard) is >= 8 and <= 12,
                "透视电路故事板必须在八至十二帧间平滑运行");
            foreach (var timeline in ambientStoryboard.Children)
            {
                var property = Storyboard.GetTargetProperty(timeline)?.Path;
                var target = mainWindow.FindName(Storyboard.GetTargetName(timeline));
                Ensure(property == "Opacity" || target is TranslateTransform && property is "X" or "Y",
                    "环境故事板只能动画透明度和变换");
            }
            foreach (var transform in signals.Select(signal => signal.RenderTransform).OfType<TranslateTransform>())
            {
                var targetName = ambientStoryboard.Children
                    .Select(Storyboard.GetTargetName)
                    .First(name => ReferenceEquals(mainWindow.FindName(name), transform));
                var properties = ambientStoryboard.Children
                    .Where(timeline => Storyboard.GetTargetName(timeline) == targetName)
                    .Select(timeline => Storyboard.GetTargetProperty(timeline)?.Path)
                    .ToHashSet();
                Ensure(properties.Contains("X") && properties.Contains("Y"),
                    "每个环境信号点都必须沿线路的横向和纵向移动");
            }
            foreach (var signal in signals)
            {
                var fade = ambientStoryboard.Children
                    .OfType<DoubleAnimationUsingKeyFrames>()
                    .SingleOrDefault(timeline => Storyboard.GetTargetName(timeline) == signal.Name
                                                 && Storyboard.GetTargetProperty(timeline)?.Path == "Opacity");
                Ensure(fade?.KeyFrames.Cast<DoubleKeyFrame>().LastOrDefault() is DoubleKeyFrame { Value: 0 } lastFrame
                       && lastFrame.KeyTime.TimeSpan == TimeSpan.FromSeconds(16),
                    "每个环境信号点都必须在十六秒循环复位前完全淡出");
            }
            Ensure(ambientStoryboard.Children.Any(timeline =>
                    Storyboard.GetTargetName(timeline) == "CircuitField"
                    && Storyboard.GetTargetProperty(timeline)?.Path == "Opacity"),
                "背景线路必须具有可见的明暗呼吸");
            Ensure(VirtualizingPanel.GetIsVirtualizing(mainWindow.HistoryList)
                   && VirtualizingPanel.GetVirtualizationMode(mainWindow.HistoryList) == VirtualizationMode.Recycling,
                "历史列表必须使用回收虚拟化");

            var historyRow = (FrameworkElement)mainWindow.HistoryList.ItemTemplate.LoadContent();
            var historyVisuals = EnumerateVisualTree(historyRow).Prepend(historyRow).OfType<UIElement>().ToList();
            Ensure(historyVisuals.All(element => element.Effect is not DropShadowEffect and not BlurEffect),
                "历史行不得使用逐项阴影");
            VerifyHistoryRowPresentation(mainWindow.HistoryList.ItemContainerStyle);

            Ensure(mainWindow.FindName("SearchBox") is WpfTextBox,
                "搜索输入框必须保留");
            Ensure(mainWindow.FindName("SearchPanel") is WpfPanel searchPanel
                   && searchPanel.Children.OfType<TextBlock>().All(text => text.Text != "搜索" || text.Visibility != Visibility.Visible),
                "搜索区不得保留独立的搜索文字标签");

            var surfaceColor = ((SolidColorBrush)mainWindow.FindResource("MonolythSurfaceBrush")).Color;
            Ensure(mainWindow.FindName("StatusSurface") is Border { Background: SolidColorBrush statusSurface }
                   && statusSurface.Color == surfaceColor
                   && mainWindow.FindName("HistoryPanel") is WpfPanel { Background: SolidColorBrush historySurface }
                   && historySurface.Color.A == 0
                   && mainWindow.FindName("SettingsPanel") is WpfPanel { Background: SolidColorBrush settingsSurface }
                   && settingsSurface.Color.A == 0,
                "历史与设置内容区必须透明，不得以整块底板遮挡电路背景");

            var settingsPanel = (WpfPanel)mainWindow.FindName("SettingsPanel");
            Ensure(settingsPanel.Margin.Left >= 36 && settingsPanel.Margin.Right >= 36,
                "设置页内容必须保留舒适的左右留白");

            var ambientField = mainWindow.FindName("AmbientField") as FrameworkElement;
            Ensure(ambientField is { Opacity: 1 },
                "主页面必须完整显示动态电路背景");
            mainWindow.ShowSettingsForPreview();
            Ensure(ambientField!.Opacity is > 0 and <= 0.4,
                "设置页必须降低电路背景亮度，避免线路干扰文字阅读");

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

            var dialogOverlay = mainWindow.FindName("DialogOverlay") as Grid;
            var dialogCancelButton = mainWindow.FindName("DialogCancelButton") as WpfButton;
            Ensure(dialogOverlay is { Visibility: Visibility.Collapsed, Effect: null }
                   && KeyboardNavigation.GetTabNavigation(dialogOverlay) == KeyboardNavigationMode.Cycle
                   && dialogCancelButton is not null,
                "窗口内弹层必须初始隐藏、循环 Tab、无模糊并提供安全取消操作");
            mainWindow.ShowClearDialogForPreview();
            Ensure(dialogOverlay!.Visibility == Visibility.Visible
                   && !mainWindow.MainContent.IsEnabled
                   && ReferenceEquals(FocusManager.GetFocusedElement(dialogOverlay), dialogCancelButton),
                "清空预览必须显示弹层、禁用背景并把初始焦点放在取消");
            mainWindow.CloseDialogForPreview();
            Ensure(dialogOverlay.Visibility == Visibility.Collapsed && mainWindow.MainContent.IsEnabled,
                "关闭弹层必须重新启用主内容");
            mainWindow.ShowHotKeyConflictForPreview();
            Ensure(mainWindow.FindName("DialogTitleText") is TextBlock { Text: "快捷键不可用" }
                   && dialogCancelButton!.Visibility == Visibility.Collapsed
                   && mainWindow.FindName("DialogPrimaryButton") is WpfButton { Content: "返回设置" },
                "快捷键冲突必须复用无取消按钮的信息弹层");
            mainWindow.CloseDialogForPreview();
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

    private static void VerifyActionButtonMotion(Style actionStyle)
    {
        var button = new WpfButton { Content = "动效检查", Style = actionStyle };
        var host = CreateAnimationHost(button);
        try
        {
            host.Show();
            host.UpdateLayout();
            Ensure(button.RenderTransform is TransformGroup,
                "主操作按钮必须定义缩放和上浮变换");
            button.RaiseEvent(new System.Windows.Input.MouseEventArgs(Mouse.PrimaryDevice, 0)
                { RoutedEvent = Mouse.MouseEnterEvent });
            WaitForAnimations();
            var transforms = (TransformGroup)button.RenderTransform;
            Ensure(transforms.Children.OfType<ScaleTransform>().Single().ScaleX > 1
                   && transforms.Children.OfType<TranslateTransform>().Single().Y < 0,
                "主操作按钮悬停时必须轻微放大并上浮");
        }
        finally
        {
            host.Close();
        }
    }

    private static void VerifyHistoryRowPresentation(Style itemContainerStyle)
    {
        var item = new ListBoxItem { Content = "历史记录", Style = itemContainerStyle };
        var host = CreateAnimationHost(item);
        try
        {
            host.Show();
            host.UpdateLayout();
            var surface = item.Template.FindName("RowSurface", item) as Border;
            Ensure(surface is not null, "历史记录必须提供独立气泡表面");
            Ensure(surface!.CornerRadius.TopLeft == 14 && surface.Margin.Left >= 8,
                "历史记录必须恢复为独立的 14 像素圆角气泡卡片");
            Ensure(surface.Effect is null && surface.RenderTransform is TransformGroup,
                "历史气泡必须使用低开销变换且不得使用逐项视觉特效");
            item.RaiseEvent(new System.Windows.Input.MouseEventArgs(Mouse.PrimaryDevice, 0)
                { RoutedEvent = Mouse.MouseEnterEvent });
            WaitForAnimations();
            var transforms = (TransformGroup)surface.RenderTransform;
            Ensure(transforms.Children.OfType<ScaleTransform>().Single().ScaleX > 1
                   && transforms.Children.OfType<TranslateTransform>().Single().Y < 0,
                "历史气泡悬停时必须提供轻微放大和上浮反馈");
        }
        finally
        {
            host.Close();
        }
    }

    private static Window CreateAnimationHost(UIElement content) => new()
    {
        Width = 320,
        Height = 180,
        Left = -10000,
        Top = -10000,
        ShowInTaskbar = false,
        ShowActivated = false,
        Opacity = 0,
        Content = content
    };

    private static void WaitForAnimations()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
