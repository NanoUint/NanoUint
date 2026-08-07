using System.Reflection;

namespace NanoUint.Scripting;

/// <summary>将 [RegistryInScript] 装饰的 MethodInfo 包装为 IScriptCommand。</summary>
public class AttributeScriptCommand : IScriptCommand
{
    private readonly MethodInfo _method;
    private readonly object? _instance; // 静态方法为 null

    public string Name { get; }

    /// <param name="commandName">.vns 脚本中使用的命令名称</param>
    /// <param name="method">要调用的方法</param>
    /// <param name="instance">非静态方法的实例，静态方法为 null</param>
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
            // void Foo()
            _method.Invoke(_instance, null);
            return;
        }

        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ScriptCommandContext))
        {
            // void Foo(ScriptCommandContext ctx) — 主要签名
            _method.Invoke(_instance, new object[] { context });
            return;
        }

        // 从 context 自动映射：先按位置参数，再按命名参数
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
        catch
        {
            return GetDefault(target);
        }
    }

    private static object? GetDefault(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}
