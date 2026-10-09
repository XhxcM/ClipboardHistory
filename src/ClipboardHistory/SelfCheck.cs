using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

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
            VerifyUiContract();

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

    private static void VerifyUiContract()
    {
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
    }

    private static MediaColor GetColor(ResourceDictionary resources, string key) =>
        ((SolidColorBrush)resources[key]).Color;

    private static MediaColor ParseColor(string value) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value);
}
