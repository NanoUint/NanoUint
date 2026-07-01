namespace NanoUint.Scripting;

/// <summary>
/// Interface for a script command that can be executed by the engine.
/// Game projects implement this to provide actual command behavior.
/// </summary>
public interface IScriptCommand
{
    /// <summary>Command name as used in .vns scripts (e.g., "say", "bg", "jump")</summary>
    string Name { get; }

    /// <summary>Execute the command with the given parameters</summary>
    void Execute(ScriptCommandContext context);
}

/// <summary>Context passed to command execution, providing access to engine state</summary>
public class ScriptCommandContext
{
    /// <summary>Positional arguments from the script</summary>
    public List<object?> Arguments { get; init; } = new();

    /// <summary>Named arguments from the script</summary>
    public Dictionary<string, object?> NamedArgs { get; init; } = new();

    /// <summary>The script engine running this command</summary>
    public ScriptEngine Engine { get; init; } = null!;

    /// <summary>Flow bindings from -> syntax</summary>
    public VnsFlowBindings? FlowBindings { get; init; }

    /// <summary>Convenience: get a named arg with fallback</summary>
    public T? Get<T>(string name, T? fallback = default)
    {
        if (NamedArgs.TryGetValue(name, out var val) && val is T t) return t;
        return fallback;
    }

    /// <summary>Convenience: get a positional arg with fallback</summary>
    public T? Arg<T>(int index, T? fallback = default)
    {
        if (index < Arguments.Count && Arguments[index] is T t) return t;
        return fallback;
    }
}
