using System.IO;
using System.Text;

namespace NanoUintVN.Save;

/// <summary>Local file save store with atomic writes (.tmp → .bak → commit).</summary>
public sealed class FileSaveStore : ISaveStore
{
    private readonly string _directory;

    public FileSaveStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public bool Save(string slotId, string json)
    {
        var path = GetPath(slotId);
        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        try
        {
            if (File.Exists(path))
                File.Copy(path, bakPath, true);

            File.WriteAllText(tmpPath, json, Encoding.UTF8);
            File.Copy(tmpPath, path, true);
            File.Delete(tmpPath);
            return true;
        }
        catch
        {
            try { File.Delete(tmpPath); } catch { }
            if (File.Exists(bakPath))
            {
                try { File.Copy(bakPath, path, true); } catch { }
            }
            return false;
        }
    }

    public string? Load(string slotId)
    {
        var path = GetPath(slotId);
        if (!File.Exists(path))
        {
            var bakPath = path + ".bak";
            if (File.Exists(bakPath))
                return File.ReadAllText(bakPath, Encoding.UTF8);
            return null;
        }
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public bool Delete(string slotId)
    {
        var path = GetPath(slotId);
        var bakPath = path + ".bak";
        var tmpPath = path + ".tmp";

        bool deleted = false;
        if (File.Exists(path)) { File.Delete(path); deleted = true; }
        if (File.Exists(bakPath)) { File.Delete(bakPath); deleted = true; }
        if (File.Exists(tmpPath)) { File.Delete(tmpPath); deleted = true; }
        return deleted;
    }

    public bool Exists(string slotId) => File.Exists(GetPath(slotId));

    public IReadOnlyList<SaveSlotManifest> GetAllManifests()
    {
        var manifests = new List<SaveSlotManifest>();
        foreach (var file in Directory.GetFiles(_directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var state = System.Text.Json.JsonSerializer.Deserialize<VNSaveState>(json);
                if (state != null)
                {
                    manifests.Add(new SaveSlotManifest
                    {
                        SlotId = state.SlotId,
                        DocumentId = state.DocumentId,
                        PageIndex = state.PageIndex,
                        BeatIndex = state.BeatIndex,
                        PageId = state.PageId,
                        SaveTime = state.SaveTime,
                        Description = state.Description,
                        ThumbnailBase64 = state.ThumbnailBase64,
                        PlayTime = state.PlayTime
                    });
                }
            }
            catch { }
        }
        return manifests.OrderBy(m => m.SlotId).ToList();
    }

    private string GetPath(string slotId) => Path.Combine(_directory, $"save_{slotId}.json");
}
