using System.IO;
using NanoUintVN.Save;

namespace NanoUintVN.Tests;

public class SaveSystemTests : IDisposable
{
    private readonly string _testDir;

    public SaveSystemTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "NanoUintVN_TestSaves_" + Guid.NewGuid().ToString("N")[..8]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void MemorySaveStore_SaveAndLoad()
    {
        var store = new MemorySaveStore();
        Assert.True(store.Save("0", "{\"test\":true}"));
        Assert.Equal("{\"test\":true}", store.Load("0"));
        Assert.True(store.Exists("0"));
    }

    [Fact]
    public void MemorySaveStore_Delete()
    {
        var store = new MemorySaveStore();
        store.Save("0", "data");
        Assert.True(store.Delete("0"));
        Assert.False(store.Exists("0"));
        Assert.Null(store.Load("0"));
    }

    [Fact]
    public void MemorySaveStore_LoadNonexistent_ReturnsNull()
    {
        var store = new MemorySaveStore();
        Assert.Null(store.Load("99"));
    }

    [Fact]
    public void FileSaveStore_AtomicWrite()
    {
        var store = new FileSaveStore(_testDir);
        Assert.True(store.Save("1", "{\"hello\":\"world\"}"));
        Assert.Equal("{\"hello\":\"world\"}", store.Load("1"));
    }

    [Fact]
    public void FileSaveStore_BackupOnOverwrite()
    {
        var store = new FileSaveStore(_testDir);
        store.Save("2", "{\"v\":1}");
        store.Save("2", "{\"v\":2}");
        Assert.Equal("{\"v\":2}", store.Load("2"));
    }

    [Fact]
    public void FileSaveStore_Delete()
    {
        var store = new FileSaveStore(_testDir);
        store.Save("3", "data");
        Assert.True(store.Delete("3"));
        Assert.False(store.Exists("3"));
    }

    [Fact]
    public void FileSaveStore_GetAllManifests()
    {
        var store = new FileSaveStore(_testDir);
        var state1 = new VNSaveState { SlotId = "0", DocumentId = "doc1", PageIndex = 1, SaveTime = DateTime.UtcNow };
        var state2 = new VNSaveState { SlotId = "5", DocumentId = "doc2", PageIndex = 3, SaveTime = DateTime.UtcNow };
        store.Save("0", System.Text.Json.JsonSerializer.Serialize(state1));
        store.Save("5", System.Text.Json.JsonSerializer.Serialize(state2));

        var manifests = store.GetAllManifests();
        Assert.Equal(2, manifests.Count);
        Assert.Contains(manifests, m => m.SlotId == "0" && m.DocumentId == "doc1");
        Assert.Contains(manifests, m => m.SlotId == "5" && m.DocumentId == "doc2");
    }

    [Fact]
    public void DefaultSaveBoundary_AllowsValidSlots()
    {
        var boundary = new DefaultSaveBoundary();
        Assert.True(boundary.CanSave("0"));
        Assert.True(boundary.CanSave("29"));
        Assert.False(boundary.CanSave("30"));
        Assert.True(boundary.CanSave("99"));
        Assert.False(boundary.CanSave("abc"));
    }

    [Fact]
    public void DefaultSaveBoundary_DisableSave()
    {
        var boundary = new DefaultSaveBoundary();
        boundary.AllowSave = false;
        Assert.False(boundary.CanSave("0"));
        Assert.False(boundary.CanSave("99"));
    }

    [Fact]
    public void SaveManagerV2_SaveAndLoad()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        var state = new VNSaveState
        {
            DocumentId = "test_doc",
            PageIndex = 2,
            BeatIndex = 1,
            Flags = new Dictionary<string, bool> { { "met_kurisu", true } }
        };

        Assert.True(mgr.Save("0", state));
        var loaded = mgr.Load("0");
        Assert.NotNull(loaded);
        Assert.Equal("test_doc", loaded.DocumentId);
        Assert.Equal(2, loaded.PageIndex);
        Assert.True(loaded.Flags["met_kurisu"]);
    }

    [Fact]
    public void SaveManagerV2_QuickSaveAndLoad()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        var state = new VNSaveState { DocumentId = "quick_test" };
        Assert.True(mgr.QuickSave(state));
        var loaded = mgr.QuickLoad();
        Assert.NotNull(loaded);
        Assert.Equal("quick_test", loaded.DocumentId);
    }

    [Fact]
    public void SaveManagerV2_BoundaryBlocksSave()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        boundary.AllowSave = false;
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        var state = new VNSaveState { DocumentId = "blocked" };
        Assert.False(mgr.Save("0", state));
        Assert.Null(mgr.Load("0"));
    }

    [Fact]
    public void SaveManagerV2_DeleteSlot()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        mgr.Save("7", new VNSaveState { DocumentId = "to_delete" });
        Assert.True(mgr.SlotExists("7"));
        Assert.True(mgr.Delete("7"));
        Assert.False(mgr.SlotExists("7"));
    }

    [Fact]
    public void SaveManagerV2_GetManifest()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        mgr.Save("3", new VNSaveState { DocumentId = "manifest_test", PageIndex = 5 });
        var manifest = mgr.GetManifest("3");
        Assert.NotNull(manifest);
        Assert.Equal("manifest_test", manifest.DocumentId);
        Assert.Equal(5, manifest.PageIndex);
    }

    [Fact]
    public void SaveManagerV2_ContributorIsRestored()
    {
        var store = new MemorySaveStore();
        var boundary = new DefaultSaveBoundary();
        var projector = new DefaultManifestProjector();
        var mgr = new SaveManagerV2(store, boundary, projector);

        var contributor = new TestContributor { Health = 42 };
        mgr.RegisterContributor(contributor);

        var state = new VNSaveState { DocumentId = "contrib_test" };
        mgr.Save("0", state);

        var contributor2 = new TestContributor();
        var mgr2 = new SaveManagerV2(store, boundary, projector);
        mgr2.RegisterContributor(contributor2);
        mgr2.Load("0");

        Assert.Equal(42, contributor2.Health);
    }
}

internal class TestContributor : ISaveStateContributor
{
    public string Domain => "test";
    public int SchemaVersion => 1;
    public Type CapturedType => typeof(int);
    public int Health { get; set; }
    public object CaptureState() => Health;
    public void RestoreState(object state) => Health = (int)state;
}
