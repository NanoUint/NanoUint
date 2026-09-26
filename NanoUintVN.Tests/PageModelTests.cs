using NanoUintVN.Dialogue;

namespace NanoUintVN.Tests;

public class PageModelTests
{
    [Fact]
    public void PageTransition_StaticFactories()
    {
        Assert.Equal(TransitionKind.None, PageTransition.None.Kind);
        Assert.Equal(0f, PageTransition.None.Duration);

        var fb = PageTransition.FlashBlack(1.0f);
        Assert.Equal(TransitionKind.FlashBlack, fb.Kind);
        Assert.Equal(1.0f, fb.Duration);

        var fw = PageTransition.FlashWhite();
        Assert.Equal(TransitionKind.FlashWhite, fw.Kind);
        Assert.Equal(0.6f, fw.Duration);

        var cf = PageTransition.Crossfade();
        Assert.Equal(TransitionKind.Crossfade, cf.Kind);
        Assert.Equal(0.8f, cf.Duration);

        var fi = PageTransition.FadeIn();
        Assert.Equal(TransitionKind.FadeIn, fi.Kind);
        Assert.Equal(0.5f, fi.Duration);
    }

    [Fact]
    public void PageAdvance_StaticFactories()
    {
        Assert.Equal(PageAdvanceMode.Click, PageAdvance.Click.Mode);

        var auto = PageAdvance.Auto(5f);
        Assert.Equal(PageAdvanceMode.Auto, auto.Mode);
        Assert.Equal(5f, auto.AutoDelay);

        var timed = PageAdvance.Timed(3f);
        Assert.Equal(PageAdvanceMode.Timed, timed.Mode);
        Assert.Equal(3f, timed.AutoDelay);
    }

    [Fact]
    public void DialogueBeat_DefaultValues()
    {
        var beat = new DialogueBeat();
        Assert.Equal(string.Empty, beat.Speaker);
        Assert.Equal(string.Empty, beat.Text);
        Assert.Null(beat.VoicePath);
        Assert.Null(beat.BgmPath);
        Assert.False(beat.StopBgmAfter);
        Assert.Equal(0.05f, beat.TextSpeed);
    }

    [Fact]
    public void VNPage_HasUniqueId()
    {
        var p1 = new VNPage();
        var p2 = new VNPage();
        Assert.NotEqual(p1.PageId, p2.PageId);
    }

    [Fact]
    public void VNPage_ElementsFiltered()
    {
        var page = new VNPage
        {
            Elements =
            {
                new BackgroundElement { SpritePath = "bg.png" },
                new SpriteElement { SpritePath = "char.png", X = 100 },
                new BgmElement { MusicPath = "bgm.ogg" }
            }
        };
        Assert.NotNull(page.Background);
        Assert.Single(page.Sprites);
        Assert.NotNull(page.Bgm);
        Assert.Null(page.Choice);
    }

    [Fact]
    public void VNPage_Choice()
    {
        var page = new VNPage
        {
            Choice = new ChoiceElement
            {
                Labels = new[] { "A", "B", "C" },
                Targets = new[] { "a", "b", "c" }
            }
        };
        Assert.NotNull(page.Choice);
        Assert.Equal(3, page.Choice.Labels.Length);
    }

    [Fact]
    public void VNDocument_GetPage()
    {
        var doc = new VNDocument { Title = "Test Chapter" };
        var p1 = new VNPage { PageId = "p1" };
        var p2 = new VNPage { PageId = "p2" };
        doc.Pages.AddRange(new[] { p1, p2 });

        Assert.Same(p1, doc.GetPage("p1"));
        Assert.Same(p2, doc.GetPage("p2"));
        Assert.Null(doc.GetPage("nonexistent"));
    }

    [Fact]
    public void VNDocument_GetPageIndex()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage { PageId = "a" });
        doc.Pages.Add(new VNPage { PageId = "b" });

        Assert.Equal(0, doc.GetPageIndex("a"));
        Assert.Equal(1, doc.GetPageIndex("b"));
        Assert.Equal(-1, doc.GetPageIndex("c"));
    }

    [Fact]
    public void ElementId_Record()
    {
        var id1 = new ElementId("test");
        var id2 = new ElementId("test");
        var id3 = new ElementId("other");
        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.Equal("test", id1.ToString());
    }

    [Fact]
    public void VNDocument_HasUniqueId()
    {
        var d1 = new VNDocument();
        var d2 = new VNDocument();
        Assert.NotEqual(d1.DocumentId, d2.DocumentId);
    }
}
