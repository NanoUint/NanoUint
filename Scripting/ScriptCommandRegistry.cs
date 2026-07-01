namespace NanoUint.Scripting;

/// <summary>
/// Registry of available script commands.
/// Game projects register their command implementations here.
/// Supports alias resolution.
/// </summary>
public class ScriptCommandRegistry
{
    private readonly Dictionary<string, IScriptCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Register a command implementation</summary>
    public void Register(IScriptCommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>Register an alias (shortName → targetCommandName)</summary>
    public void RegisterAlias(string shortName, string targetCommand)
    {
        _aliases[shortName] = targetCommand;
    }

    /// <summary>Resolve a command name (including aliases) to the actual command</summary>
    public IScriptCommand? Resolve(string name)
    {
        // Check direct command
        if (_commands.TryGetValue(name, out var cmd))
            return cmd;

        // Check alias
        if (_aliases.TryGetValue(name, out var target) && _commands.TryGetValue(target, out cmd))
            return cmd;

        return null;
    }

    /// <summary>Check if a command or alias exists</summary>
    public bool Exists(string name) => Resolve(name) != null;

    /// <summary>Get all registered command names</summary>
    public IEnumerable<string> CommandNames => _commands.Keys;

    /// <summary>Get all registered aliases</summary>
    public IEnumerable<KeyValuePair<string, string>> Aliases => _aliases;
}
