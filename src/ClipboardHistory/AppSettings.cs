using System.IO;
using System.Text;
using System.Text.Json;

namespace ClipboardHistory;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool IsPaused { get; set; }
    public int HotKeyModifiers { get; set; } = NativeMethods.ModWin | NativeMethods.ModAlt;
    public int HotKeyVirtualKey { get; set; } = 0x56;
    public List<string> ExcludedApps { get; set; } = [];
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public SettingsStore(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions), new UTF8Encoding(false));
        File.Move(temporaryPath, _filePath, true);
    }
}
