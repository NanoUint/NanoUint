namespace NanoUint.Scripting;

/// <summary>
/// 可由引擎执行的脚本命令接口。
/// 游戏项目实现此接口以提供实际的命令行为。
/// </summary>
public interface IScriptCommand
{
    /// <summary>.vns 脚本中使用的命令名称（例如 "say"、"bg"、"jump"）</summary>
    string Name { get; }

    /// <summary>使用给定参数执行命令</summary>
    void Execute(ScriptCommandContext context);
}

/// <summary>传递给命令执行的上下文，提供对引擎状态的访问</summary>
public class ScriptCommandContext
{
    /// <summary>脚本中的位置参数</summary>
    public List<object?> Arguments { get; init; } = new();

    /// <summary>脚本中的命名参数</summary>
    public Dictionary<string, object?> NamedArgs { get; init; } = new();

    /// <summary>运行此命令的脚本引擎</summary>
    public ScriptEngine Engine { get; init; } = null!;

    /// <summary>来自 -> 语法的流程绑定</summary>
    public VnsFlowBindings? FlowBindings { get; init; }

    /// <summary>便捷方法：获取命名参数，带备用值</summary>
    public T? Get<T>(string name, T? fallback = default)
    {
        if (NamedArgs.TryGetValue(name, out var val) && val is T t) return t;
        return fallback;
    }

    /// <summary>便捷方法：获取位置参数，带备用值</summary>
    public T? Arg<T>(int index, T? fallback = default)
    {
        if (index < Arguments.Count && Arguments[index] is T t) return t;
        return fallback;
    }
}
