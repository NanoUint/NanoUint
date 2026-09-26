using System.Numerics;
using Box2DSharp.Collision;
using Box2DSharp.Collision.Collider;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;

namespace NanoUint.Physics;

public readonly struct RaycastHit2D
{
    public Collider2D Collider { get; init; }
    public Vector2 Point { get; init; }
    public Vector2 Normal { get; init; }
    public float Fraction { get; init; }
}

public readonly struct Collision2D
{
    public Collider2D Collider { get; init; }
    public Collider2D OtherCollider { get; init; }
    public Vector2 Point { get; init; }
    public Vector2 Normal { get; init; }
}

public static class Physics2D
{
    private static PhysicsSystem? _system;

    internal static void Register(PhysicsSystem system) => _system = system;
    internal static void Unregister() => _system = null;

    public static bool Enabled => _system?.Enabled ?? false;

    public static void Enable()
    {
        if (_system == null)
        {
            _system = new PhysicsSystem();
            _system.Enabled = true;
        }
        else
        {
            _system.Enabled = true;
        }
    }

    public static void Disable()
    {
        if (_system != null)
            _system.Enabled = false;
    }

    public static void Dispose()
    {
        _system?.Dispose();
        _system = null;
    }

    public static RaycastHit2D? Raycast(Vector2 origin, Vector2 direction, float maxDistance = float.MaxValue)
    {
        return _system?.Raycast(origin, direction, maxDistance);
    }

    public static List<RaycastHit2D> RaycastAll(Vector2 origin, Vector2 direction, float maxDistance = float.MaxValue)
    {
        return _system?.RaycastAll(origin, direction, maxDistance) ?? new List<RaycastHit2D>();
    }

    public static List<Collider2D> OverlapCircle(Vector2 center, float radius)
    {
        return _system?.OverlapCircle(center, radius) ?? new List<Collider2D>();
    }

    public static List<Collider2D> OverlapBox(Vector2 center, Vector2 size)
    {
        return _system?.OverlapBox(center, size) ?? new List<Collider2D>();
    }

    public static Vector2 Gravity
    {
        get => _system?.Gravity ?? new Vector2(0, -9.81f);
        set { if (_system != null) _system.Gravity = value; }
    }

    public const int MaxLayers = 32;

    public static void SetLayerCollision(int layerA, int layerB, bool collides)
    {
        if (_system != null)
            _system.SetLayerCollision(layerA, layerB, collides);
    }

    public static bool GetLayerCollision(int layerA, int layerB)
    {
        return _system?.GetLayerCollision(layerA, layerB) ?? true;
    }

    public static void ResetLayerCollision()
    {
        _system?.ResetLayerCollision();
    }

    public static float InterpolationAlpha => _system?.InterpolationAlpha ?? 0f;
}
