using System.IO;

namespace NanoUintVN.Settings;

/// <summary>File-backed settings store. Writes through a temporary file so a crash cannot truncate the live file.</summary>
public sealed class FileSettingsStore : ISettingsStore
{
    private static readonly System.Text.Json.JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public FileSettingsStore(string filePath) => _filePath = filePath;

    public string Name => "file";

    public string DescribeLocation() => _filePath;

    public bool Save(VNSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, System.Text.Json.JsonSerializer.Serialize(settings, WriteOptions));

            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, null);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public VNSettings? Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return System.Text.Json.JsonSerializer.Deserialize<VNSettings>(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                     or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public bool Delete()
    {
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
