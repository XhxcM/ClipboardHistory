using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ClipboardHistory;

internal static class NativeMethods
{
    public const int WmClipboardUpdate = 0x031D;
    public const int WmHotKey = 0x0312;
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;
    public const int HotKeyId = 0x4348;
    public const uint AttachParentProcess = 0xFFFFFFFF;
    private const int VkLeftWin = 0x5B;
    private const int VkRightWin = 0x5C;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr windowHandle, int id, int modifiers, int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeConsole();

    public static string GetForegroundProcessName()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? process.ProcessName
                : process.ProcessName + ".exe";
        }
        catch
        {
            return "未知应用";
        }
    }

    public static int ModifierFromKey(Key key) => key switch
    {
        Key.LeftAlt or Key.RightAlt => ModAlt,
        Key.LeftCtrl or Key.RightCtrl => ModControl,
        Key.LeftShift or Key.RightShift => ModShift,
        Key.LWin or Key.RWin => ModWin,
        _ => 0
    };

    public static bool IsWindowsKeyDown() =>
        (GetKeyState(VkLeftWin) & 0x8000) != 0 || (GetKeyState(VkRightWin) & 0x8000) != 0;

    public static string FormatHotKey(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & ModWin) != 0) parts.Add("Win");
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Shift");
        if (virtualKey != 0)
        {
            parts.Add(KeyInterop.KeyFromVirtualKey(virtualKey).ToString().ToUpperInvariant());
        }
        return string.Join("+", parts);
    }
}
