using System.Reflection;
using System.Runtime.CompilerServices;

namespace NanoUint.Scripting;

/// <summary>扫描程序集和对象实例，查找 [RegistryInScript] 装饰的方法并注册到脚本引擎。</summary>
public static class ScriptCommandScanner
{
    /// <summary>扫描给定程序集中所有带有 [RegistryInScript] 特性的静态方法。</summary>
    public static void ScanAssembly(ScriptEngine engine, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            // 跳过编译器生成的类型（闭包、匿名类型等）
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                continue;
            if (type.Name.Contains('<') || type.Name.Contains('>'))
                continue;

            ScanTypeStatic(engine, type);
        }
    }

    /// <summary>扫描特定类型中的静态方法。</summary>
    public static void ScanType<T>(ScriptEngine engine) => ScanTypeStatic(engine, typeof(T));

    /// <summary>扫描特定类型中的静态方法（非泛型重载）。</summary>
    public static void ScanType(ScriptEngine engine, Type type) => ScanTypeStatic(engine, type);

    /// <summary>扫描给定对象上的实例方法和静态方法。</summary>
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
                // 程序集扫描跳过实例方法
                continue;
            }

            engine.RegisterCommand(new AttributeScriptCommand(attr.CommandName, method, null));
        }
    }
}
