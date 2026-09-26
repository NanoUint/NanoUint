using NanoUintVN.Characters;

namespace NanoUintVN.Tests;

public class CharacterRegistryTests
{
    [Fact]
    public void RegisterAndFind_ById()
    {
        var reg = new CharacterRegistry();
        reg.Register(new FakeCharacter("okabe", "岡部倫太郎"));

        Assert.Equal(1, reg.Count);
        Assert.Equal("岡部倫太郎", reg.Find("okabe")!.DisplayName);
        Assert.Null(reg.Find("missing"));
    }

    [Fact]
    public void RegisterAndFind_ByDisplayName()
    {
        var reg = new CharacterRegistry();
        reg.Register(new FakeCharacter("okabe", "岡部倫太郎"));

        Assert.NotNull(reg.FindByDisplayName("岡部倫太郎"));
        Assert.Null(reg.FindByDisplayName("kurisu"));
    }

    [Fact]
    public void UnregisterAndClear_Behave()
    {
        var reg = new CharacterRegistry();
        reg.Register(new FakeCharacter("okabe", "岡部倫太郎"));
        reg.Register(new FakeCharacter("kurisu", "牧瀬紅莉栖"));

        Assert.True(reg.Unregister("okabe"));
        Assert.False(reg.Unregister("okabe"));
        Assert.Single(reg.All);

        reg.Clear();
        Assert.Empty(reg.All);
    }

    [Fact]
    public void BaseCharacter_DefaultsAreEmptyNotThrowing()
    {
        var c = new FakeCharacter("plain", "Plain");

        Assert.Empty(c.Images.Expressions);
        Assert.Null(c.Images.GetImage("smile"));
        Assert.Null(c.Voices);
        Assert.Null(c.Phone);
        Assert.Null(c.NameColorHex);
    }

    [Fact]
    public void Conversation_WalksLinesThenCompletes()
    {
        var convo = new FakeConversation();

        Assert.Null(convo.Current);
        Assert.False(convo.IsComplete);

        Assert.True(convo.Advance());
        Assert.Equal("a", convo.Current!.Text);

        Assert.True(convo.Advance());
        Assert.Same(convo.Lines[1], convo.Current);

        Assert.False(convo.Advance());
        Assert.True(convo.IsComplete);

        convo.Reset();
        Assert.Null(convo.Current);
        Assert.False(convo.IsComplete);
    }

    private sealed class FakeCharacter : Character
    {
        public FakeCharacter(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public override string Id { get; }

        public override string DisplayName { get; }
    }

    private sealed class FakeConversation : IConversation
    {
        private int _position = -1;

        public string Id => "c1";

        public IReadOnlyList<ConversationLine> Lines { get; } = new[]
        {
            new ConversationLine("okabe", "a"),
            new ConversationLine("kurisu", "b", "voice_b", "smile"),
        };

        public int Position => _position;

        public bool IsComplete => _position >= Lines.Count - 1;

        public ConversationLine? Current => _position >= 0 && _position < Lines.Count ? Lines[_position] : null;

        public bool Advance()
        {
            if (IsComplete) return false;
            _position++;
            return true;
        }

        public void Reset() => _position = -1;
    }
}
