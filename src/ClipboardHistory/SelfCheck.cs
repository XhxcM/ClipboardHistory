using System.IO;

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
}
