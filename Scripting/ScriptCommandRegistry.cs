namespace NanoUint.Scripting;

/// <summary>Registry of available script commands.</summary>
public class ScriptCommandRegistry
{
    private readonly Dictionary<string, IScriptCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a command implementation</summary>
    public void Register(IScriptCommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>Registers an alias (shortName → targetCommandName)</summary>
    public void RegisterAlias(string shortName, string targetCommand)
    {
        _aliases[shortName] = targetCommand;
    }

    /// <summary>Resolves a command name (including aliases) to the actual command</summary>
    public IScriptCommand? Resolve(string name)
    {
        if (_commands.TryGetValue(name, out var cmd))
            return cmd;

        if (_aliases.TryGetValue(name, out var target) && _commands.TryGetValue(target, out cmd))
            return cmd;

        return null;
    }

    /// <summary>Checks whether a command or alias exists</summary>
    public bool Exists(string name) => Resolve(name) != null;

    /// <summary>Gets all registered command names</summary>
    public IEnumerable<string> CommandNames => _commands.Keys;

    /// <summary>Gets all registered aliases</summary>
    public IEnumerable<KeyValuePair<string, string>> Aliases => _aliases;
}
