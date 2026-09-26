using System.IO;
using NanoUintVN;
using NanoUintVN.Dialogue;
using NanoUintVN.History;
using NanoUintVN.Settings;

namespace NanoUintVN.Tests;

public class ReadHistoryTests : IDisposable
{
    private readonly string _dir;

    public ReadHistoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "NanoUintVN_ReadHistory_" + Guid.NewGuid().ToString("N")[..8]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Keys_AreContentBasedAndStable()
    {
        var a = new DialogueBeat { Speaker = "Okabe", Text = "El Psy Kongroo" };
        var b = new DialogueBeat { Speaker = "Okabe", Text = "El Psy Kongroo" };
        var c = new DialogueBeat { Speaker = "Kurisu", Text = "El Psy Kongroo" };

        Assert.Equal(ReadHistoryKeys.ForBeat(a), ReadHistoryKeys.ForBeat(b));
        Assert.NotEqual(ReadHistoryKeys.ForBeat(a), ReadHistoryKeys.ForBeat(c));
        Assert.Equal(16, ReadHistoryKeys.ForBeat(a).Length);
    }

    [Fact]
    public void Keys_SeparateSpeakerFromText()
    {
        Assert.NotEqual(ReadHistoryKeys.ForLine("ab", "c"), ReadHistoryKeys.ForLine("a", "bc"));
    }

    [Fact]
    public void MemoryHistory_MarksAndQueries()
    {
        var history = new MemoryReadHistory();
        Assert.False(history.HasRead("k1"));

        history.MarkRead("k1");
        history.MarkRead("k1");
        history.MarkRead("");

        Assert.True(history.HasRead("k1"));
        Assert.Equal(1, history.Count);

        history.Clear();
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void FileHistory_RoundTripsAcrossInstances()
    {
        var path = Path.Combine(_dir, "read.json");
        var first = new FileReadHistory(path);
        first.MarkRead("alpha");
        first.MarkRead("beta");
        Assert.True(first.Save());

        var second = new FileReadHistory(path);
        Assert.Equal(2, second.Count);
        Assert.True(second.HasRead("alpha"));
        Assert.True(second.HasRead("beta"));
    }

    [Fact]
    public void FileHistory_MissingFileStartsEmpty()
    {
        var history = new FileReadHistory(Path.Combine(_dir, "absent.json"));
        Assert.Equal(0, history.Count);
        Assert.False(history.HasRead("anything"));
    }

    [Fact]
    public void FileHistory_CorruptFileStartsEmptyInsteadOfThrowing()
    {
        var path = Path.Combine(_dir, "broken.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{ this is not json");

        var history = new FileReadHistory(path);

        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void FileHistory_ClearPersists()
    {
        var path = Path.Combine(_dir, "read.json");
        var first = new FileReadHistory(path);
        first.MarkRead("alpha");
        first.Clear();

        Assert.Equal(0, new FileReadHistory(path).Count);
    }

    [Fact]
    public void Skip_ReadOnly_StopsOnUnreadCurrentLine()
    {
        var beat = new DialogueBeat { Speaker = "Okabe", Text = "unseen" };
        var player = PlayerAt(beat);
        var history = new MemoryReadHistory();
        var controller = new VNPlaybackController(player) { History = history, SkipPolicy = SkipMode.ReadOnly };

        controller.SetSkip(true);
        controller.Update(0.016f);

        Assert.False(controller.IsSkipMode);
        Assert.True(history.HasRead(ReadHistoryKeys.ForBeat(beat)));
    }

    [Fact]
    public void Skip_ReadOnly_KeepsGoingOnReadLine()
    {
        var beat = new DialogueBeat { Speaker = "Okabe", Text = "seen" };
        var player = PlayerAt(beat);
        var history = new MemoryReadHistory();
        history.MarkRead(ReadHistoryKeys.ForBeat(beat));
        var controller = new VNPlaybackController(player) { History = history, SkipPolicy = SkipMode.ReadOnly };

        controller.SetSkip(true);
        controller.Update(0.016f);

        Assert.True(controller.IsSkipMode);
    }

    [Fact]
    public void Skip_All_IgnoresHistory()
    {
        var player = PlayerAt(new DialogueBeat { Speaker = "Okabe", Text = "unseen" });
        var controller = new VNPlaybackController(player)
        {
            History = new MemoryReadHistory(),
            SkipPolicy = SkipMode.All,
        };

        controller.SetSkip(true);
        controller.Update(0.016f);

        Assert.True(controller.IsSkipMode);
    }

    [Fact]
    public void Skip_WithoutHistory_BehavesLikeAll()
    {
        var player = PlayerAt(new DialogueBeat { Speaker = "Okabe", Text = "unseen" });
        var controller = new VNPlaybackController(player) { History = null, SkipPolicy = SkipMode.ReadOnly };

        controller.SetSkip(true);
        controller.Update(0.016f);

        Assert.True(controller.IsSkipMode);
    }

    [Fact]
    public void History_RecordsEachBeatOnlyOnce()
    {
        var beat = new DialogueBeat { Speaker = "Okabe", Text = "once" };
        var player = PlayerAt(beat);
        var history = new MemoryReadHistory();
        var controller = new VNPlaybackController(player) { History = history };

        controller.Update(0.016f);
        controller.Update(0.016f);
        controller.Update(0.016f);

        Assert.Equal(1, history.Count);
    }

    private static DialoguePlayer PlayerAt(DialogueBeat beat)
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage { Beats = { beat } });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Advance();
        return player;
    }
}
