using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ClipboardHistory;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\ClipboardHistory.SingleInstance";
    private const string ShowEventName = @"Local\ClipboardHistory.Show";
    private const string ExitEventName = @"Local\ClipboardHistory.Exit";
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private EventWaitHandle? _exitEvent;
    private CancellationTokenSource? _showCancellation;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
            Shutdown(SelfCheck.Run());
            return;
        }

        var captureIndex = Array.FindIndex(e.Args, argument =>
            argument.Equals("--capture-ui", StringComparison.OrdinalIgnoreCase)
            || argument.Equals("--capture-ui-dark", StringComparison.OrdinalIgnoreCase));
        if (captureIndex >= 0)
        {
            if (captureIndex + 1 >= e.Args.Length)
            {
                Shutdown(2);
                return;
            }

#pragma warning disable WPF0001
            ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
            ApplyMonolythPalette(Resources);

            _ = CaptureUiAsync(e.Args[captureIndex + 1]);
            return;
        }

        _mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                var eventName = e.Args.Contains("--exit-existing", StringComparer.OrdinalIgnoreCase)
                    ? ExitEventName
                    : ShowEventName;
                EventWaitHandle.OpenExisting(eventName).Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first process is still starting; a second instance should still exit.
            }

            Shutdown();
            return;
        }

        if (e.Args.Contains("--exit-existing", StringComparer.OrdinalIgnoreCase))
        {
            Shutdown();
            return;
        }

        ApplyMonolythPalette(Resources);

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        _showCancellation = new CancellationTokenSource();
        _window = new MainWindow();
        MainWindow = _window;
        _window.ExitRequested += (_, _) =>
        {
            _window.PrepareForExit();
            Shutdown();
        };

        _window.Show();
        if (e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            _window.Hide();
        }

        _ = Task.Run(() => WaitForShowRequests(_showCancellation.Token));
    }

    private async Task CaptureUiAsync(string outputDirectory)
    {
        var exitCode = await UiCapture.RunAsync(this, outputDirectory);
        Shutdown(exitCode);
    }

    internal static void ApplyMonolythPalette(ResourceDictionary resources)
    {
        SetBrush(resources, "MonolythBackgroundBrush", "#050708");
        SetBrush(resources, "MonolythSurfaceBrush", "#0B1012");
        SetBrush(resources, "MonolythPrimaryTextBrush", "#E8EEEE");
        SetBrush(resources, "MonolythSecondaryTextBrush", "#929E9D");
        SetBrush(resources, "MonolythAccentBrush", "#45CDBB");
        SetBrush(resources, "MonolythAccentSoftBrush", "#3345CDBB");
        SetBrush(resources, "MonolythBorderBrush", "#334244");
        SetBrush(resources, "MonolythHoverBrush", "#1B2527");
        SetBrush(resources, "MonolythPressedBrush", "#233033");
        SetBrush(resources, "MonolythDangerBrush", "#D9827B");
        SetBrush(resources, "MonolythDangerSurfaceBrush", "#291A1A");
        SetBrush(resources, "MonolythCircuitBrush", "#805FD8CA");
        SetBrush(resources, "MonolythOverlayBrush", "#B3080C0D");
    }

    private static void SetBrush(ResourceDictionary resources, string key, string color)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        resources[key] = brush;
    }

    private void WaitForShowRequests(CancellationToken cancellationToken)
    {
        var events = new WaitHandle[] { _showEvent!, _exitEvent! };
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var signaled = WaitHandle.WaitAny(events, 500);
                if (signaled == 0)
                {
                    Dispatcher.BeginInvoke(() => _window?.ShowAndActivate());
                }
                else if (signaled == 1)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _window?.PrepareForExit();
                        Shutdown();
                    });
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _window?.PrepareForExit();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showCancellation?.Cancel();
        _showEvent?.Dispose();
        _exitEvent?.Dispose();
        _mutex?.Dispose();
        _window?.PrepareForExit();
        NativeMethods.FreeConsole();
        base.OnExit(e);
    }
}
