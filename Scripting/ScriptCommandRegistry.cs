namespace NanoUint.Scripting;

/// <summary>
/// 可用脚本命令的注册表。
/// 游戏项目在此处注册其命令实现。
/// 支持别名解析。
/// </summary>
public class ScriptCommandRegistry
{
    private readonly Dictionary<string, IScriptCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册命令实现</summary>
    public void Register(IScriptCommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>注册别名（shortName → targetCommandName）</summary>
    public void RegisterAlias(string shortName, string targetCommand)
    {
        _aliases[shortName] = targetCommand;
    }

    /// <summary>将命令名称（包括别名）解析为实际命令</summary>
    public IScriptCommand? Resolve(string name)
    {
        // 检查直接命令
        if (_commands.TryGetValue(name, out var cmd))
            return cmd;

        // 检查别名
        if (_aliases.TryGetValue(name, out var target) && _commands.TryGetValue(target, out cmd))
            return cmd;

        return null;
    }

    /// <summary>检查命令或别名是否存在</summary>
    public bool Exists(string name) => Resolve(name) != null;

    /// <summary>获取所有已注册的命令名称</summary>
    public IEnumerable<string> CommandNames => _commands.Keys;

    /// <summary>获取所有已注册的别名</summary>
    public IEnumerable<KeyValuePair<string, string>> Aliases => _aliases;
}
