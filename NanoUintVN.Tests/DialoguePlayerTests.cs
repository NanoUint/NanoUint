using NanoUintVN.Dialogue;

namespace NanoUintVN.Tests;

public class DialoguePlayerTests
{
    [Fact]
    public void NewPlayer_IsIdle()
    {
        var player = new DialoguePlayer();
        Assert.Equal(PlayerState.Idle, player.State);
        Assert.Null(player.CurrentPage);
        Assert.Null(player.CurrentBeat);
    }

    [Fact]
    public void Start_WithEmptyDocument_Completes()
    {
        var player = new DialoguePlayer();
        player.LoadDocument(new VNDocument());
        player.Start();
        Assert.Equal(PlayerState.Complete, player.State);
    }

    [Fact]
    public void Start_WithPages_PlaysFirstPage()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "Okabe", Text = "El Psy Kongroo" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        Assert.Equal(PlayerState.Playing, player.State);
        Assert.Equal(0, player.PageIndex);
        Assert.Equal(0, player.BeatIndex);
    }

    [Fact]
    public void Advance_WhileTyping_CompletesBeat()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "Kurisu", Text = "Test" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        Assert.Equal(PlayerState.Playing, player.State);
        player.Advance();
        Assert.Equal(PlayerState.WaitingForAdvance, player.State);
    }

    [Fact]
    public void Advance_WhileWaiting_MovesToNextBeat()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats =
            {
                new DialogueBeat { Speaker = "A", Text = "First" },
                new DialogueBeat { Speaker = "B", Text = "Second" }
            }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Advance(); // complete first beat
        Assert.Equal(0, player.BeatIndex);
        player.Advance(); // move to second beat
        Assert.Equal(1, player.BeatIndex);
        Assert.Equal(PlayerState.Playing, player.State);
    }

    [Fact]
    public void Advance_AfterLastBeat_MovesToNextPage()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Page 1" } }
        });
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "B", Text = "Page 2" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Advance(); // complete beat
        player.Advance(); // move to page 2
        Assert.Equal(1, player.PageIndex);
        Assert.Equal(0, player.BeatIndex);
    }

    [Fact]
    public void Advance_AfterLastPage_Completes()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Only page" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Advance(); // complete beat
        player.Advance(); // try next page -> complete
        Assert.Equal(PlayerState.Complete, player.State);
    }

    [Fact]
    public void PageWithChoice_WaitsForChoice()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Choice = new ChoiceElement
            {
                Labels = new[] { "Yes", "No" },
                Targets = new[] { "ch1_yes", "ch1_no" }
            }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        Assert.Equal(PlayerState.WaitingForChoice, player.State);
    }

    [Fact]
    public void SelectChoice_MovesToNextPage()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Choice = new ChoiceElement { Labels = new[] { "A", "B" } }
        });
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "X", Text = "After choice" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.SelectChoice(0);
        Assert.Equal(1, player.PageIndex);
    }

    [Fact]
    public void PauseAndResume()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Test" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Pause();
        Assert.Equal(PlayerState.Paused, player.State);
        player.Resume();
        Assert.Equal(PlayerState.Playing, player.State);
    }

    [Fact]
    public void Stop_ResetsState()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Test" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Stop();
        Assert.Equal(PlayerState.Idle, player.State);
        Assert.Null(player.CurrentPage);
    }

    [Fact]
    public void SaveAndLoadState()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats =
            {
                new DialogueBeat { Speaker = "A", Text = "First" },
                new DialogueBeat { Speaker = "B", Text = "Second" }
            }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();
        player.Advance(); // complete beat 0
        player.Advance(); // move to beat 1

        player.SaveState(out var pageIdx, out var beatIdx);
        Assert.Equal(0, pageIdx);
        Assert.Equal(1, beatIdx);

        var player2 = new DialoguePlayer();
        player2.LoadDocument(doc);
        player2.LoadState(pageIdx, beatIdx);
        Assert.Equal(1, player2.BeatIndex);
    }

    [Fact]
    public void Events_AreRaised()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Test" } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);

        bool beatStartFired = false;
        bool pageEnterFired = false;
        player.Events.OnBeatStart += b => beatStartFired = true;
        player.Events.onPageEnter += p => pageEnterFired = true;

        player.Start();
        Assert.True(beatStartFired);
        Assert.True(pageEnterFired);
    }

    [Fact]
    public void UpdateTypewriter_Progresses()
    {
        var doc = new VNDocument();
        doc.Pages.Add(new VNPage
        {
            Beats = { new DialogueBeat { Speaker = "A", Text = "Test", TextSpeed = 1.0f } }
        });
        var player = new DialoguePlayer();
        player.LoadDocument(doc);
        player.Start();

        Assert.Equal(0f, player.TypewriterProgress);
        player.UpdateTypewriter(0.5f, 1.0f);
        Assert.Equal(0.5f, player.TypewriterProgress);
        player.UpdateTypewriter(0.6f, 1.0f);
        Assert.Equal(1f, player.TypewriterProgress);
        Assert.Equal(PlayerState.WaitingForAdvance, player.State);
    }
}
