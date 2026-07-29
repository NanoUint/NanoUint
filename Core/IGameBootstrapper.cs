namespace NanoUint;

/// <summary>
/// 游戏启动引导器。SteinsGateX 项目实现此接口，传入 Application.Run()。
/// </summary>
public interface IGameBootstrapper
{
    /// <summary>引擎启动后调用。在此方法中创建初始场景和所有 GameObject。</summary>
    void OnStart(Scene mainScene);
}
