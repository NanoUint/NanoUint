using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer SignatureHighlighter 复刻。语法着色：类绿、字段橙、属性青、方法紫、关键字蓝、命名空间灰。</summary>
internal static class SignatureHighlighter
{
    /// <summary>成员名称的高亮色。</summary>
    public static Color ColorFor(MemberInfo member) => member switch
    {
        FieldInfo => UEPalette.SigField,
        PropertyInfo => UEPalette.SigProperty,
        ConstructorInfo => UEPalette.SigMethod,
        MethodInfo => UEPalette.SigMethod,
        _ => UEPalette.TextDefault,
    };

    /// <summary>类型名的签名着色(类/枚举绿, 基元/关键字蓝)。</summary>
    public static Color ColorForType(Type t)
    {
        if (t.IsPrimitive || t == typeof(string) || t == typeof(object) || t == typeof(decimal))
            return UEPalette.SigKeyword;
        return UEPalette.SigClass;
    }

    /// <summary>构建成员签名文本: 灰色前缀(类型) + 彩色成员名。</summary>
    public static TextBlock Signature(MemberInfo member, double fontSize = 12)
    {
        var tb = new TextBlock
        {
            FontSize = fontSize,
            FontFamily = UEPalette.DefaultFont,
            VerticalAlignment = VerticalAlignment.Center,
        };

        switch (member)
        {
            case FieldInfo fi:
                AddTypeRun(tb, fi.FieldType);
                AddRun(tb, fi.Name, UEPalette.SigField);
                break;
            case PropertyInfo pi:
                AddTypeRun(tb, pi.PropertyType);
                AddRun(tb, pi.Name, UEPalette.SigProperty);
                break;
            case ConstructorInfo ci:
                AddRun(tb, TypeName(ci.DeclaringType ?? ci.ReflectedType ?? typeof(object)), UEPalette.SigMethod);
                AddRun(tb, $"({string.Join(", ", ci.GetParameters().Select(p => TypeName(p.ParameterType)))})",
                    UEPalette.TextInactive);
                break;
            case MethodInfo mi:
                AddTypeRun(tb, mi.ReturnType);
                AddRun(tb, mi.Name, UEPalette.SigMethod);
                AddRun(tb, $"({string.Join(", ", mi.GetParameters().Select(p => TypeName(p.ParameterType)))})",
                    UEPalette.TextInactive);
                break;
        }

        return tb;
    }

    /// <summary>简化类型名(泛型/数组友好)。</summary>
    public static string TypeName(Type t)
    {
        if (t.IsArray && t.GetElementType() != null)
            return TypeName(t.GetElementType()!) + "[]";
        if (t.IsGenericType)
        {
            var name = t.Name.Split('`')[0];
            return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(TypeName))}>";
        }
        return t.Name;
    }

    private static void AddTypeRun(TextBlock tb, Type t)
    {
        AddRun(tb, TypeName(t) + " ", ColorForType(t));
    }

    private static void AddRun(TextBlock tb, string text, Color color)
    {
        tb.Inlines.Add(new Run(text)
        {
            Foreground = new SolidColorBrush(color),
        });
    }
}
