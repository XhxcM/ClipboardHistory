using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

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

            VerifyMotionStyles(directory);

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

    private static void VerifyMotionStyles(string dataDirectory)
    {
        var mainWindow = new MainWindow(dataDirectory, manageStartup: false);
        var button = new WpfButton
        {
            Content = "动效检查",
            Style = (Style)WpfApplication.Current.FindResource(typeof(WpfButton))
        };
        var bubble = (Border)mainWindow.HistoryList.ItemTemplate.LoadContent();
        var host = new Window
        {
            Width = 320,
            Height = 180,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Content = new StackPanel { Children = { button, bubble } }
        };

        try
        {
            host.Show();
            host.UpdateLayout();

            Ensure(button.RenderTransform is TransformGroup, "按钮必须定义动画变换");
            button.RaiseEvent(new WpfMouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            WaitForAnimations();
            var buttonScale = ((TransformGroup)button.RenderTransform).Children.OfType<ScaleTransform>().Single();
            Ensure(buttonScale.ScaleX > 1, "按钮悬停时必须轻微放大");

            Ensure(bubble.CornerRadius.TopLeft > 0, "历史记录必须呈现圆角气泡形状");
            Ensure(bubble.RenderTransform is TransformGroup, "历史记录必须定义动画变换");
            Ensure(bubble.Effect is DropShadowEffect, "历史记录必须定义柔和阴影");
            var bubbleTransforms = (TransformGroup)bubble.RenderTransform;
            var bubbleScale = bubbleTransforms.Children.OfType<ScaleTransform>().Single();
            var bubbleLift = bubbleTransforms.Children.OfType<TranslateTransform>().Single();
            var bubbleShadow = (DropShadowEffect)bubble.Effect;
            bubble.RaiseEvent(new WpfMouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            WaitForAnimations();
            Ensure(bubbleScale.ScaleX > 1 && bubbleLift.Y < 0 && bubbleShadow.Opacity > 0,
                "历史记录悬停时必须放大、上浮并显示阴影");
        }
        finally
        {
            host.Close();
            mainWindow.PrepareForExit();
            mainWindow.Close();
        }
    }

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
