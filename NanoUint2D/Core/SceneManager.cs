namespace NanoUint;

/// <summary>Scene manager.</summary>
public static class SceneManager
{
    private static Scene? _activeScene;

    /// <summary>Currently active scene.</summary>
    public static Scene? ActiveScene => _activeScene;

    /// <summary>Raised after a scene is loaded.</summary>
    public static event Action<Scene>? SceneLoaded;

    /// <summary>Loads a scene, replacing the current one.</summary>
    public static void LoadScene(Scene scene)
    {
        _activeScene = scene;
        SceneLoaded?.Invoke(scene);
    }

    /// <summary>Gets the active scene, throwing if none exists.</summary>
    public static Scene GetActiveScene()
    {
        return _activeScene ?? throw new InvalidOperationException("No active scene. Call SceneManager.LoadScene() first.");
    }
}
