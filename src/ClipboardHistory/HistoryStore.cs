using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipboardHistory;

public sealed class HistoryItem
{
    public required string Text { get; set; }
    public required string SourceApp { get; set; }
    public DateTimeOffset FirstCapturedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }

    [JsonIgnore]
    public string DisplayTime => LastUsedAt.LocalDateTime.ToString("M月d日 HH:mm");
}

public sealed class HistoryStore
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(72);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly Func<DateTimeOffset> _now;
    private readonly List<HistoryItem> _items = [];

    public HistoryStore(string dataDirectory, Func<DateTimeOffset>? now = null)
    {
        _filePath = Path.Combine(dataDirectory, "history.json");
        _now = now ?? (() => DateTimeOffset.Now);
        Load();
        CleanupExpired();
    }

    public IReadOnlyList<HistoryItem> Items => _items;

    public void AddOrUpdate(string text, string sourceApp, DateTimeOffset? usedAt = null)
    {
        var timestamp = usedAt ?? _now();
        var existing = _items.FirstOrDefault(item => string.Equals(item.Text, text, StringComparison.Ordinal));
        if (existing is null)
        {
            _items.Add(new HistoryItem
            {
                Text = text,
                SourceApp = sourceApp,
                FirstCapturedAt = timestamp,
                LastUsedAt = timestamp
            });
        }
        else
        {
            existing.LastUsedAt = timestamp;
            existing.SourceApp = sourceApp;
        }

        Sort();
        Save();
    }

    public IReadOnlyList<HistoryItem> Search(string? query)
    {
        IEnumerable<HistoryItem> result = _items;
        if (!string.IsNullOrWhiteSpace(query))
        {
            result = result.Where(item => item.Text.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return result.OrderByDescending(item => item.LastUsedAt).ToList();
    }

    public int CleanupExpired(DateTimeOffset? now = null)
    {
        var cutoff = (now ?? _now()) - Retention;
        var removed = _items.RemoveAll(item => item.LastUsedAt < cutoff);
        if (removed > 0)
        {
            Save();
        }

        return removed;
    }

    public void Delete(HistoryItem item)
    {
        if (_items.Remove(item))
        {
            Save();
        }
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            _items.AddRange(JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(_filePath), JsonOptions) ?? []);
            Sort();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            var backup = $"{_filePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(_filePath, backup, false);
        }
    }

    private void Sort() => _items.Sort((left, right) => right.LastUsedAt.CompareTo(left.LastUsedAt));

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_items, JsonOptions), new UTF8Encoding(false));
        File.Move(temporaryPath, _filePath, true);
    }
}
