namespace NanoUint.Scripting;

/// <summary>将方法标记为可从 .vns 脚本调用的脚本命令。</summary>
/// <example>
/// [RegistryInScript("open_message")]
/// public static void OpenMessage(ScriptCommandContext ctx) { ... }
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RegistryInScriptAttribute : Attribute
{
    /// <summary>.vns 脚本中用于调用此方法的命令名称</summary>
    public string CommandName { get; }

    /// <param name="commandName">.vns 中使用的名称（例如 "sys_exit"、"my_custom_cmd"）</param>
    public RegistryInScriptAttribute(string commandName)
    {
        CommandName = commandName;
    }
}
