using System.Threading;
using System.Windows;

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

        var darkCapture = e.Args.Contains("--capture-ui-dark", StringComparer.OrdinalIgnoreCase);
        var captureFlag = darkCapture ? "--capture-ui-dark" : "--capture-ui";
        var captureIndex = Array.FindIndex(e.Args, argument => argument.Equals(captureFlag, StringComparison.OrdinalIgnoreCase));
        if (captureIndex >= 0)
        {
            if (captureIndex + 1 >= e.Args.Length)
            {
                Shutdown(2);
                return;
            }

            if (darkCapture)
            {
#pragma warning disable WPF0001
                ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
            }

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
