using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipboardHistory;

internal static class UiCapture
{
    public static async Task<int> RunAsync(App app, string outputDirectory)
    {
        var outputPath = Path.GetFullPath(outputDirectory);
        var dataPath = Path.Combine(Path.GetTempPath(), "ClipboardHistory-Capture-" + Guid.NewGuid().ToString("N"));
        MainWindow? window = null;

        try
        {
            Directory.CreateDirectory(outputPath);
            var now = DateTimeOffset.Now;
            var history = new HistoryStore(dataPath, () => now);
            history.AddOrUpdate("会议记录：周一 10:00 讨论项目进度", "Notepad.exe", now.AddMinutes(-8));
            history.AddOrUpdate("https://example.com/project/clipboard-history", "msedge.exe", now.AddMinutes(-3));
            history.AddOrUpdate("订单编号：CH-2026-1005", "Code.exe", now);

            window = new MainWindow(dataPath, manageStartup: false);
#pragma warning disable WPF0001
            window.ThemeMode = app.ThemeMode;
#pragma warning restore WPF0001
            app.MainWindow = window;
            window.Show();
            await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(250);
            SaveClientArea(window, Path.Combine(outputPath, "main-window.png"));

            window.ShowSettingsForPreview();
            await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveClientArea(window, Path.Combine(outputPath, "settings-page.png"));
            return 0;
        }
        catch
        {
            return 1;
        }
        finally
        {
            if (window is not null)
            {
                window.PrepareForExit();
                window.Close();
            }

            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, true);
            }
        }
    }

    private static void SaveClientArea(Window window, string outputPath)
    {
        var visual = (FrameworkElement)window.Content;
        visual.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(visual);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(visual.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(visual.ActualHeight * dpi.DpiScaleY)),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
