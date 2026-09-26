using System.IO;

namespace NanoUintVN.History;

/// <summary>
/// File-backed read history. Loads on construction and writes through a temporary file so a crash
/// cannot truncate the live record — losing it would make every line look unread again.
/// </summary>
public sealed class FileReadHistory : IReadHistory
{
    private readonly string _filePath;
    private readonly HashSet<string> _read = new(StringComparer.Ordinal);

    public FileReadHistory(string filePath)
    {
        _filePath = filePath;
        LoadFromDisk();
    }

    public int Count => _read.Count;

    public IReadOnlyCollection<string> All => _read;

    public bool HasRead(string lineKey) => !string.IsNullOrEmpty(lineKey) && _read.Contains(lineKey);

    public void MarkRead(string lineKey)
    {
        if (!string.IsNullOrEmpty(lineKey)) _read.Add(lineKey);
    }

    public bool Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, System.Text.Json.JsonSerializer.Serialize(_read.OrderBy(k => k, StringComparer.Ordinal).ToList()));

            if (File.Exists(_filePath)) File.Replace(tempPath, _filePath, null);
            else File.Move(tempPath, _filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Clear()
    {
        _read.Clear();
        Save();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var keys = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            if (keys == null) return;
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key)) _read.Add(key);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                     or System.Text.Json.JsonException)
        {
            _read.Clear();
        }
    }
}
