namespace NanoUint.Scripting;

/// <summary>Marks a method as a script command callable from .vns scripts.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RegistryInScriptAttribute : Attribute
{
    /// <summary>Command name used in .vns scripts to invoke this method</summary>
    public string CommandName { get; }

    /// <param name="commandName">Name used in .vns (e.g. "sys_exit", "my_custom_cmd")</param>
    public RegistryInScriptAttribute(string commandName)
    {
        CommandName = commandName;
    }
}
