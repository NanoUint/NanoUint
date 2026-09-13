using System.Reflection;
using NanoUint.Diagnostics;

namespace NanoUint.Scripting;

/// <summary>Wraps a [RegistryInScript]-decorated MethodInfo as an IScriptCommand.</summary>
public class AttributeScriptCommand : IScriptCommand
{
    private readonly MethodInfo _method;
    private readonly object? _instance; // null for static methods

    public string Name { get; }

    /// <param name="commandName">Command name used in .vns scripts</param>
    /// <param name="method">Method to invoke</param>
    /// <param name="instance">Instance for non-static methods; null for static methods</param>
    public AttributeScriptCommand(string commandName, MethodInfo method, object? instance = null)
    {
        Name = commandName;
        _method = method;
        _instance = instance;
    }

    public void Execute(ScriptCommandContext context)
    {
        var parameters = _method.GetParameters();

        if (parameters.Length == 0)
        {
            _method.Invoke(_instance, null);
            return;
        }

        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ScriptCommandContext))
        {
            // void Foo(ScriptCommandContext ctx) — primary signature
            _method.Invoke(_instance, new object[] { context });
            return;
        }

        // Auto-map from context: positional arguments first, then named
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if (p.ParameterType == typeof(ScriptCommandContext))
            {
                args[i] = context;
            }
            else if (i < context.Arguments.Count && context.Arguments[i] != null)
            {
                args[i] = TryConvert(context.Arguments[i]!, p.ParameterType);
            }
            else if (context.NamedArgs.TryGetValue(p.Name!, out var namedVal) && namedVal != null)
            {
                args[i] = TryConvert(namedVal, p.ParameterType);
            }
            else
            {
                args[i] = p.DefaultValue is DBNull ? GetDefault(p.ParameterType) : p.DefaultValue;
            }
        }

        _method.Invoke(_instance, args);
    }

    private static object? TryConvert(object value, Type target)
    {
        if (target.IsInstanceOfType(value)) return value;

        try
        {
            if (target == typeof(string)) return value.ToString();
            return Convert.ChangeType(value, target);
        }
        catch (Exception ex)
        {
            Logger.Warning("Scripting", $"Argument convert failed: '{value}' → {target.Name}: {ex.Message}");
            return GetDefault(target);
        }
    }

    private static object? GetDefault(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}
