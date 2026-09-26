using NanoUint.Debugging;

namespace NanoUint.Tests;

public class DebugTreeTests
{
    [Fact]
    public void GetOrAdd_CreatesNestedPath()
    {
        var tree = new DebugTree();

        var node = tree.SetValue("Phone/Messages/1", "hello");

        Assert.Equal("1", node.Name);
        Assert.Equal("hello", node.Value);
        Assert.Equal("hello", tree.Find("Phone/Messages/1")!.Value);
    }

    [Fact]
    public void GetOrAdd_ReusesExistingNodes()
    {
        var tree = new DebugTree();

        var a = tree.GetOrAdd("A/B");
        var b = tree.GetOrAdd("A/B");

        Assert.Same(a, b);
        Assert.Single(tree.Root.Children);
    }

    [Fact]
    public void Find_ReturnsNullForMissingPath()
    {
        var tree = new DebugTree();
        tree.GetOrAdd("A/B");

        Assert.Null(tree.Find("A/C"));
        Assert.Null(tree.Find("Z"));
    }

    [Fact]
    public void Remove_DetachesSubtree()
    {
        var tree = new DebugTree();
        tree.GetOrAdd("A/B/C");

        Assert.True(tree.Remove("A/B"));
        Assert.Null(tree.Find("A/B"));
        Assert.Empty(tree.Root.Children[0].Children);
    }

    [Fact]
    public void Remove_RootIsIgnored()
    {
        var tree = new DebugTree();
        Assert.False(tree.Remove(""));
        Assert.NotNull(tree.Find(""));
    }

    [Fact]
    public void Clear_DropsAllChildrenAndRaisesChanged()
    {
        var tree = new DebugTree();
        tree.GetOrAdd("A/B");
        var raised = 0;
        tree.Changed += () => raised++;

        tree.Clear();

        Assert.Empty(tree.Root.Children);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Providers_RebuildTheirSubtreeInPlace()
    {
        var provider = new CountingProvider();
        DebugRegistry.RegisterProvider(provider);
        try
        {
            DebugRegistry.RefreshProviders();
            Assert.Equal("Phone", DebugRegistry.Tree.Root.Children[0].Name);
            Assert.Equal("3", DebugRegistry.Tree.Find("Phone/Unread")!.Value);

            provider.Unread = 7;
            DebugRegistry.RefreshProviders();

            Assert.Equal("7", DebugRegistry.Tree.Find("Phone/Unread")!.Value);
            Assert.Single(DebugRegistry.Tree.Root.Children);
        }
        finally
        {
            DebugRegistry.UnregisterProvider(provider);
            DebugRegistry.Tree.Clear();
        }
    }

    private sealed class CountingProvider : IDebugTreeProvider
    {
        public int Unread { get; set; } = 3;

        public string RootName => "Phone";

        public void Build(DebugTreeNode root)
        {
            root.AddChild("Unread").Value = Unread.ToString();
        }
    }
}
