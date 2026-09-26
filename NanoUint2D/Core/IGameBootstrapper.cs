namespace NanoUint;

/// <summary>Game bootstrapper interface passed to Application.Run().</summary>
public interface IGameBootstrapper
{
    /// <summary>Called after the engine starts to create the initial scene.</summary>
    void OnStart(Scene mainScene);
}
