namespace NanoUint.Tests;

public class SceneTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var scene = new Scene("MyScene");
        Assert.Equal("MyScene", scene.Name);
    }

    [Fact]
    public void AddObject_ReturnsSameObject()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        var result = scene.AddObject(go);
        Assert.Same(go, result);
    }

    [Fact]
    public void AddObject_ByName_ReturnsNewObject()
    {
        var scene = new Scene("Test");
        var go = scene.AddObject("MyObj");
        Assert.Equal("MyObj", go.Name);
        Assert.Contains(go, scene.RootObjects);
    }

    [Fact]
    public void AddObject_AppearsInRootObjects()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);
        Assert.Single(scene.RootObjects);
    }

    [Fact]
    public void AddObject_NameCollision_AutoRenames()
    {
        var scene = new Scene("Test");
        scene.AddObject("A");
        var second = scene.AddObject("A");
        Assert.Equal("A_1", second.Name);
    }

    [Fact]
    public void AddObject_MultipleCollisions_IncrementsSuffix()
    {
        var scene = new Scene("Test");
        scene.AddObject("X");
        scene.AddObject("X");
        var third = scene.AddObject("X");
        Assert.Equal("X_2", third.Name);
    }

    [Fact]
    public void FindObject_Existing_ReturnsObject()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Target");
        scene.AddObject(go);
        Assert.Same(go, scene.FindObject("Target"));
    }

    [Fact]
    public void FindObject_Missing_ReturnsNull()
    {
        var scene = new Scene("Test");
        Assert.Null(scene.FindObject("Nonexistent"));
    }

    [Fact]
    public void FindObject_Destroyed_ReturnsNull()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);
        go.Destroy();
        Assert.Null(scene.FindObject("Obj"));
    }

    [Fact]
    public void RemoveObject_RemovesFromScene()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);
        scene.RemoveObject(go);
        Assert.Empty(scene.RootObjects);
        Assert.Null(scene.FindObject("Obj"));
    }

    [Fact]
    public void RootObjects_ExcludesDestroyed()
    {
        var scene = new Scene("Test");
        var go1 = new GameObject("A");
        var go2 = new GameObject("B");
        scene.AddObject(go1);
        scene.AddObject(go2);
        go1.Destroy();

        var alive = scene.RootObjects;
        Assert.Single(alive);
        Assert.Contains(go2, alive);
    }

    [Fact]
    public void RootObjects_IsReadOnly()
    {
        var scene = new Scene("Test");
        Assert.IsAssignableFrom<IReadOnlyList<GameObject>>(scene.RootObjects);
    }

    [Fact]
    public void ToString_ContainsNameAndCount()
    {
        var scene = new Scene("Test");
        var str = scene.ToString();
        Assert.Contains("Test", str);
        Assert.Contains("0", str);
    }

    [Fact]
    public void ToString_AfterAdd_ShowsCorrectCount()
    {
        var scene = new Scene("Test");
        scene.AddObject(new GameObject("A"));
        scene.AddObject(new GameObject("B"));
        Assert.Contains("2", scene.ToString());
    }

    [Fact]
    public void CollectSaveState_ContainsAllObjects()
    {
        var scene = new Scene("Test");
        scene.AddObject("A");
        scene.AddObject("B");
        var state = scene.CollectSaveState();
        Assert.Equal("Test", state.SceneName);
        Assert.Equal(2, state.Objects.Count);
    }

    [Fact]
    public void CollectSaveState_CapturesTransform()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        go.Transform.X = 5f;
        go.Transform.Y = 10f;
        go.Transform.Opacity = 0.7f;
        go.Transform.FlipX = true;
        go.Transform.SortingOrder = 3;
        scene.AddObject(go);

        var state = scene.CollectSaveState();
        var objState = state.Objects[0];
        Assert.Equal("Obj", objState.Name);
        Assert.Equal(5f, objState.TransformX);
        Assert.Equal(10f, objState.TransformY);
        Assert.Equal(0.7f, objState.TransformOpacity);
        Assert.True(objState.TransformFlipX);
        Assert.Equal(3, objState.SortingOrder);
    }

    [Fact]
    public void ApplySaveState_RestoresTransform()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);

        var state = new SceneSaveState
        {
            SceneName = "Test",
            Objects = new List<GameObjectSaveState>
            {
                new() { Name = "Obj", TransformX = 99f, TransformY = 88f, TransformOpacity = 0.3f, TransformFlipX = true, SortingOrder = 7 }
            }
        };
        scene.ApplySaveState(state);

        Assert.Equal(99f, go.Transform.X);
        Assert.Equal(88f, go.Transform.Y);
        Assert.Equal(0.3f, go.Transform.Opacity);
        Assert.True(go.Transform.FlipX);
        Assert.Equal(7, go.Transform.SortingOrder);
    }

    [Fact]
    public void ApplySaveState_SkipsMissingObjects()
    {
        var scene = new Scene("Test");
        scene.AddObject(new GameObject("Exists"));

        var state = new SceneSaveState
        {
            SceneName = "Test",
            Objects = new List<GameObjectSaveState>
            {
                new() { Name = "Exists", TransformX = 5f },
                new() { Name = "Missing", TransformX = 10f }
            }
        };
        scene.ApplySaveState(state); // should not throw
    }

    [Fact]
    public void SaveLoadRoundTrip_PreservesState()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Player");
        go.Transform.X = 42f;
        go.Transform.Y = 84f;
        go.Transform.Opacity = 0.5f;
        scene.AddObject(go);

        var saved = scene.CollectSaveState();

        var scene2 = new Scene("Test2");
        scene2.AddObject(new GameObject("Player"));
        scene2.ApplySaveState(saved);

        var player = scene2.FindObject("Player")!;
        Assert.Equal(42f, player.Transform.X);
        Assert.Equal(84f, player.Transform.Y);
        Assert.Equal(0.5f, player.Transform.Opacity);
    }

    [Fact]
    public void AddObject_SetsSceneReference()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);
        Assert.Same(scene, go.Scene);
    }

    [Fact]
    public void FindObject_AfterDestroy_CleansUpMap()
    {
        var scene = new Scene("Test");
        var go = new GameObject("Obj");
        scene.AddObject(go);
        go.Destroy();

        // First call should find it's destroyed and clean up
        Assert.Null(scene.FindObject("Obj"));
        // Second call should also return null
        Assert.Null(scene.FindObject("Obj"));
    }
}
