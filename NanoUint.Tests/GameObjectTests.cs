namespace NanoUint.Tests;

public class GameObjectTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var go = new GameObject("MyObj");
        Assert.Equal("MyObj", go.Name);
    }

    [Fact]
    public void Constructor_DefaultName()
    {
        var go = new GameObject();
        Assert.Equal("GameObject", go.Name);
    }

    [Fact]
    public void Constructor_CreatesTransform()
    {
        var go = new GameObject("Test");
        Assert.NotNull(go.Transform);
        Assert.IsType<Transform>(go.Transform);
    }

    [Fact]
    public void ActiveSelf_DefaultsTrue()
    {
        var go = new GameObject("Test");
        Assert.True(go.ActiveSelf);
    }

    [Fact]
    public void AddComponent_ReturnsSameType()
    {
        var go = new GameObject("Test");
        var comp = go.AddComponent<StubComponent>();
        Assert.NotNull(comp);
        Assert.IsType<StubComponent>(comp);
    }

    [Fact]
    public void AddComponent_SetsGameObject()
    {
        var go = new GameObject("Test");
        var comp = go.AddComponent<StubComponent>();
        Assert.Same(go, comp.GameObject);
    }

    [Fact]
    public void GetComponent_ReturnsAddedComponent()
    {
        var go = new GameObject("Test");
        var added = go.AddComponent<StubComponent>();
        var found = go.GetComponent<StubComponent>();
        Assert.Same(added, found);
    }

    [Fact]
    public void GetComponent_ReturnsNullWhenMissing()
    {
        var go = new GameObject("Test");
        var found = go.GetComponent<StubComponent>();
        Assert.Null(found);
    }

    [Fact]
    public void TryGetComponent_TrueWhenExists()
    {
        var go = new GameObject("Test");
        go.AddComponent<StubComponent>();
        Assert.True(go.TryGetComponent<StubComponent>(out var comp));
        Assert.NotNull(comp);
    }

    [Fact]
    public void TryGetComponent_FalseWhenMissing()
    {
        var go = new GameObject("Test");
        Assert.False(go.TryGetComponent<StubComponent>(out var comp));
        Assert.Null(comp);
    }

    [Fact]
    public void GetComponents_ReturnsAllOfSameType()
    {
        var go = new GameObject("Test");
        go.AddComponent<StubComponent>();
        go.AddComponent<StubComponent>();
        go.AddComponent<OtherStubComponent>();
        var result = go.GetComponents<StubComponent>();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetComponents_EmptyWhenNone()
    {
        var go = new GameObject("Test");
        var result = go.GetComponents<StubComponent>();
        Assert.Empty(result);
    }

    [Fact]
    public void Components_ListIncludesAll()
    {
        var go = new GameObject("Test");
        go.AddComponent<StubComponent>();
        go.AddComponent<OtherStubComponent>();
        // Transform is always there + 2 added = 3
        Assert.Equal(3, go.Components.Count);
    }

    [Fact]
    public void RemoveComponent_DestroyedAndRemoved()
    {
        var go = new GameObject("Test");
        var comp = go.AddComponent<StubComponent>();
        go.RemoveComponent(comp);
        Assert.Null(go.GetComponent<StubComponent>());
        Assert.True(comp.IsDestroyed);
    }

    [Fact]
    public void RemoveComponent_CannotRemoveTransform()
    {
        var go = new GameObject("Test");
        Assert.Throws<InvalidOperationException>(() => go.RemoveComponent(go.Transform));
    }

    [Fact]
    public void Destroy_SetsIsDestroyed()
    {
        var go = new GameObject("Test");
        go.Destroy();
        Assert.True(go.IsDestroyed);
    }

    [Fact]
    public void Destroy_CallsOnDestroyOnComponents()
    {
        var go = new GameObject("Test");
        var comp = go.AddComponent<StubComponent>();
        go.Destroy();
        Assert.True(comp.IsDestroyed);
    }

    [Fact]
    public void Destroy_RemovesFromScene()
    {
        var scene = new Scene("TestScene");
        var go = new GameObject("Test");
        scene.AddObject(go);
        go.Destroy();
        Assert.Null(scene.FindObject("Test"));
    }

    [Fact]
    public void Destroy_Idempotent()
    {
        var go = new GameObject("Test");
        go.Destroy();
        go.Destroy(); // should not throw
        Assert.True(go.IsDestroyed);
    }

    [Fact]
    public void AddComponent_ToDestroyed_Throws()
    {
        var go = new GameObject("Test");
        go.Destroy();
        Assert.Throws<InvalidOperationException>(() => go.AddComponent<StubComponent>());
    }

    [Fact]
    public void Instantiate_CreatesClone()
    {
        var scene = new Scene("TestScene");
        var original = new GameObject("Original");
        original.Transform.X = 10f;
        original.Transform.Y = 20f;
        scene.AddObject(original);

        var clone = GameObject.Instantiate(original);
        Assert.NotSame(original, clone);
        Assert.Equal("Original(Clone)", clone.Name);
        Assert.Equal(10f, clone.Transform.X);
        Assert.Equal(20f, clone.Transform.Y);
    }

    [Fact]
    public void Instantiate_CopiesComponents()
    {
        var scene = new Scene("TestScene");
        var original = new GameObject("Original");
        original.AddComponent<StubComponent>();
        scene.AddObject(original);

        var clone = GameObject.Instantiate(original);
        Assert.NotNull(clone.GetComponent<StubComponent>());
        Assert.NotSame(original.GetComponent<StubComponent>(), clone.GetComponent<StubComponent>());
    }

    [Fact]
    public void Instantiate_IntoTargetScene()
    {
        var scene1 = new Scene("Scene1");
        var scene2 = new Scene("Scene2");
        var original = new GameObject("Original");
        scene1.AddObject(original);

        var clone = GameObject.Instantiate(original, scene2);
        Assert.Contains(clone, scene2.RootObjects);
        Assert.DoesNotContain(clone, scene1.RootObjects);
    }

    [Fact]
    public void Instantiate_Destroyed_Throws()
    {
        var scene = new Scene("TestScene");
        var go = new GameObject("Test");
        scene.AddObject(go);
        go.Destroy();
        Assert.Throws<InvalidOperationException>(() => GameObject.Instantiate(go));
    }

    [Fact]
    public void ToString_ContainsName()
    {
        var go = new GameObject("MyObj");
        Assert.Contains("MyObj", go.ToString());
    }

    [Fact]
    public void ToString_DestroyedPrefix()
    {
        var go = new GameObject("Test");
        go.Destroy();
        Assert.Contains("Destroyed", go.ToString());
    }
}

public class StubComponent : Component { }

public class OtherStubComponent : Component { }
