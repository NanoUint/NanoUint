
namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer InspectorManager 复刻：供 ObjectExplorer 点击时联动打开 Inspector 面板。</summary>
internal static class InspectorManager
{
    /// <summary>当前 Inspector 面板(由宿主注册)。</summary>
    public static InspectorPanel? Panel { get; set; }

    /// <summary>在 Inspector 中查看一个 GameObject([G] tab)。</summary>
    public static void Inspect(GameObject go) => Panel?.Inspect(go);

    /// <summary>在 Inspector 中查看一个 Component([R] 反射 tab)。</summary>
    public static void Inspect(Component comp) => Panel?.InspectComponent(comp);
}
