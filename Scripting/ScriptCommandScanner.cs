using System.Reflection;
using System.Runtime.CompilerServices;

namespace NanoUint.Scripting;

/// <summary>Scans assemblies and object instances for [RegistryInScript]-decorated methods and registers them with the script engine.</summary>
public static class ScriptCommandScanner
{
    /// <summary>Scans all static methods with the [RegistryInScript] attribute in the given assembly.</summary>
    public static void ScanAssembly(ScriptEngine engine, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            // Skip compiler-generated types (closures, anonymous types, etc.)
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                continue;
            if (type.Name.Contains('<') || type.Name.Contains('>'))
                continue;

            ScanTypeStatic(engine, type);
        }
    }

    /// <summary>Scans static methods in the given type.</summary>
    public static void ScanType<T>(ScriptEngine engine) => ScanTypeStatic(engine, typeof(T));

    /// <summary>Scans static methods in the given type (non-generic overload).</summary>
    public static void ScanType(ScriptEngine engine, Type type) => ScanTypeStatic(engine, type);

    /// <summary>Scans instance and static methods on the given objects.</summary>
    public static void ScanInstances(ScriptEngine engine, params object[] instances)
    {
        foreach (var instance in instances)
        {
            var type = instance.GetType();
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<RegistryInScriptAttribute>();
                if (attr == null) continue;

                var inst = method.IsStatic ? null : instance;
                engine.RegisterCommand(new AttributeScriptCommand(attr.CommandName, method, inst));
            }
        }
    }

    private static void ScanTypeStatic(ScriptEngine engine, Type type)
    {
        foreach (var method in type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var attr = method.GetCustomAttribute<RegistryInScriptAttribute>();
            if (attr == null) continue;

            if (!method.IsStatic)
            {
                // Assembly scan skips instance methods
                continue;
            }

            engine.RegisterCommand(new AttributeScriptCommand(attr.CommandName, method, null));
        }
    }
}
