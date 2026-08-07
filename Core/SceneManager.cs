namespace NanoUint;

/// <summary>场景管理器。</summary>
public static class SceneManager
{
    private static Scene? _activeScene;

    /// <summary>当前活跃场景。</summary>
    public static Scene? ActiveScene => _activeScene;

    /// <summary>场景加载后触发。</summary>
    public static event Action<Scene>? SceneLoaded;

    /// <summary>加载一个场景（替换当前场景）。</summary>
    public static void LoadScene(Scene scene)
    {
        _activeScene = scene;
        SceneLoaded?.Invoke(scene);
    }

    /// <summary>获取活跃场景，若不存在则抛出异常。</summary>
    public static Scene GetActiveScene()
    {
        return _activeScene ?? throw new InvalidOperationException("No active scene. Call SceneManager.LoadScene() first.");
    }
}
