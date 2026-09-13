
namespace NanoUint.Debugging.UE;

internal static class InspectorManager
{
    public static InspectorPanel? Panel { get; set; }

    public static void Inspect(GameObject go) => Panel?.Inspect(go);

    public static void Inspect(Component comp) => Panel?.InspectComponent(comp);
}
