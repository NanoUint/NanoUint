using System.Numerics;
using Box2DSharp.Collision;
using Box2DSharp.Collision.Collider;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;

namespace NanoUint.Physics;

public sealed class PhysicsSystem : IDisposable
{
    private readonly World _world;
    private readonly Dictionary<Body, Rigidbody2D> _bodyMap = new();
    private readonly Dictionary<Fixture, Collider2D> _fixtureMap = new();
    private Vector2 _gravity = new(0, -9.81f);
    private bool _enabled;

    private readonly bool[,] _layerMatrix = new bool[Physics2D.MaxLayers, Physics2D.MaxLayers];
    private float _interpolationAlpha;

    public bool Enabled { get => _enabled; set => _enabled = value; }

    public float InterpolationAlpha => _interpolationAlpha;

    public Vector2 Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            _world.Gravity = value;
        }
    }

    internal World World => _world;

    public PhysicsSystem()
    {
        _world = new World(new Vector2(0, -9.81f));
        _world.SetContactListener(new ContactListener(this));
        _world.SetContactFilter(new CollisionFilter(this));
        ResetLayerCollision();
        Physics2D.Register(this);
    }

    public void SetLayerCollision(int layerA, int layerB, bool collides)
    {
        if (layerA < 0 || layerA >= Physics2D.MaxLayers || layerB < 0 || layerB >= Physics2D.MaxLayers) return;
        _layerMatrix[layerA, layerB] = collides;
        _layerMatrix[layerB, layerA] = collides;
    }

    public bool GetLayerCollision(int layerA, int layerB)
    {
        if (layerA < 0 || layerA >= Physics2D.MaxLayers || layerB < 0 || layerB >= Physics2D.MaxLayers) return true;
        return _layerMatrix[layerA, layerB];
    }

    public void ResetLayerCollision()
    {
        for (int i = 0; i < Physics2D.MaxLayers; i++)
            for (int j = 0; j < Physics2D.MaxLayers; j++)
                _layerMatrix[i, j] = true;
    }

    internal void RegisterBody(Rigidbody2D rb)
    {
        if (rb.GameObject == null) return;
        var t = rb.GameObject.Transform;
        var pos = new Vector2(t.WorldX, t.WorldY);

        var bodyDef = new BodyDef
        {
            BodyType = Rigidbody2D.MapBodyType(rb.BodyType),
            Position = pos,
            LinearDamping = rb.LinearDamping,
            AngularDamping = rb.AngularDamping,
            FixedRotation = rb.FixedRotation,
            Bullet = rb.Bullet,
            GravityScale = rb.GravityScale
        };

        var body = _world.CreateBody(bodyDef);
        body.UserData = rb;
        rb.Body = body;
        _bodyMap[body] = rb;

        foreach (var comp in rb.GameObject.Components)
        {
            if (comp is Collider2D collider)
                RegisterCollider(rb, collider);
        }
    }

    internal void RegisterCollider(Rigidbody2D rb, Collider2D collider)
    {
        if (rb.Body == null) return;
        var fixture = collider.CreateFixture(rb.Body);
        if (fixture != null)
        {
            fixture.UserData = collider;
            var filter = fixture.Filter;
            filter.CategoryBits = (ushort)(1 << rb.Layer);
            filter.MaskBits = ComputeMaskBits(rb.Layer);
            fixture.Filter = filter;
            collider.Fixture = fixture;
            _fixtureMap[fixture] = collider;
        }
    }

    internal void UpdateFixtureLayer(Collider2D collider, int newLayer)
    {
        if (collider.Fixture == null) return;
        var filter = collider.Fixture.Filter;
        filter.CategoryBits = (ushort)(1 << newLayer);
        filter.MaskBits = ComputeMaskBits(newLayer);
        collider.Fixture.Filter = filter;
    }

    internal ushort ComputeMaskBits(int layer)
    {
        ushort mask = 0;
        for (int i = 0; i < Physics2D.MaxLayers; i++)
        {
            if (_layerMatrix[layer, i])
                mask |= (ushort)(1 << i);
        }
        return mask;
    }

    internal void UnregisterBody(Rigidbody2D rb)
    {
        if (rb.Body == null) return;
        foreach (var fixture in rb.Body.FixtureList.ToList())
        {
            if (fixture.UserData is Collider2D c)
            {
                c.Fixture = null;
                _fixtureMap.Remove(fixture);
            }
        }
        _world.DestroyBody(rb.Body);
        _bodyMap.Remove(rb.Body);
        rb.Body = null;
    }

    public void Step(float dt)
    {
        if (!_enabled) return;

        foreach (var kv in _bodyMap)
        {
            var body = kv.Key;
            var rb = kv.Value;
            if (body.BodyType == Box2DSharp.Dynamics.BodyType.DynamicBody)
            {
                var customGravity = new Vector2(_gravity.X * rb.GravityScale, _gravity.Y * rb.GravityScale);
                var force = (customGravity - _world.Gravity) * body.Mass;
                body.ApplyForceToCenter(force, true);
            }
        }

        _world.Step(dt, 8, 3);

        foreach (var kv in _bodyMap)
        {
            kv.Value.SyncToTransform();
        }
    }

    public void StepWithInterpolation(float dt, float alpha)
    {
        if (!_enabled) return;

        _interpolationAlpha = alpha;

        foreach (var kv in _bodyMap)
        {
            var body = kv.Key;
            var rb = kv.Value;
            if (body.BodyType == Box2DSharp.Dynamics.BodyType.DynamicBody)
            {
                var customGravity = new Vector2(_gravity.X * rb.GravityScale, _gravity.Y * rb.GravityScale);
                var force = (customGravity - _world.Gravity) * body.Mass;
                body.ApplyForceToCenter(force, true);
            }
        }

        foreach (var kv in _bodyMap)
        {
            kv.Value.CapturePreviousState();
        }

        _world.Step(dt, 8, 3);

        foreach (var kv in _bodyMap)
        {
            kv.Value.SyncToTransform();
        }
    }

    public RaycastHit2D? Raycast(Vector2 origin, Vector2 direction, float maxDistance)
    {
        var start = origin;
        var end = origin + direction * maxDistance;

        RaycastHit2D? closest = null;
        var callback = new B2RayCastCallback((fixture, point, normal, fraction) =>
        {
            if (fixture.UserData is Collider2D collider)
            {
                var hit = new RaycastHit2D
                {
                    Collider = collider,
                    Point = point,
                    Normal = normal,
                    Fraction = fraction
                };
                if (closest == null || fraction < closest.Value.Fraction)
                    closest = hit;
            }
            return fraction;
        });
        _world.RayCast(callback, start, end);

        return closest;
    }

    public List<RaycastHit2D> RaycastAll(Vector2 origin, Vector2 direction, float maxDistance)
    {
        var start = origin;
        var end = origin + direction * maxDistance;
        var results = new List<RaycastHit2D>();

        var callback = new B2RayCastCallback((fixture, point, normal, fraction) =>
        {
            if (fixture.UserData is Collider2D collider)
            {
                results.Add(new RaycastHit2D
                {
                    Collider = collider,
                    Point = point,
                    Normal = normal,
                    Fraction = fraction
                });
            }
            return 1f;
        });
        _world.RayCast(callback, start, end);

        return results.OrderBy(h => h.Fraction).ToList();
    }

    public List<Collider2D> OverlapCircle(Vector2 center, float radius)
    {
        var lower = new Vector2(center.X - radius, center.Y - radius);
        var upper = new Vector2(center.X + radius, center.Y + radius);
        var aabb = new AABB(lower, upper);
        var results = new List<Collider2D>();
        var callback = new B2QueryCallback(fixture =>
        {
            if (fixture.UserData is Collider2D collider)
                results.Add(collider);
            return true;
        });
        _world.QueryAABB(callback, aabb);
        return results;
    }

    public List<Collider2D> OverlapBox(Vector2 center, Vector2 size)
    {
        var half = size * 0.5f;
        var lower = center - half;
        var upper = center + half;
        var aabb = new AABB(lower, upper);
        var results = new List<Collider2D>();
        var callback = new B2QueryCallback(fixture =>
        {
            if (fixture.UserData is Collider2D collider)
                results.Add(collider);
            return true;
        });
        _world.QueryAABB(callback, aabb);
        return results;
    }

    public void Dispose()
    {
        Physics2D.Unregister();
        _bodyMap.Clear();
        _fixtureMap.Clear();
    }

    private sealed class B2RayCastCallback : IRayCastCallback
    {
        private readonly Func<Fixture, Vector2, Vector2, float, float> _callback;
        public B2RayCastCallback(Func<Fixture, Vector2, Vector2, float, float> callback) => _callback = callback;
        public float RayCastCallback(Fixture fixture, in Vector2 point, in Vector2 normal, float fraction)
            => _callback(fixture, point, normal, fraction);
    }

    private sealed class B2QueryCallback : IQueryCallback
    {
        private readonly Func<Fixture, bool> _callback;
        public B2QueryCallback(Func<Fixture, bool> callback) => _callback = callback;
        public bool QueryCallback(Fixture fixture) => _callback(fixture);
    }

    private sealed class ContactListener : IContactListener
    {
        private readonly PhysicsSystem _system;
        public ContactListener(PhysicsSystem system) => _system = system;

        public void BeginContact(Contact contact)
        {
            var fixtureA = contact.FixtureA;
            var fixtureB = contact.FixtureB;
            if (fixtureA.UserData is Collider2D colA && fixtureB.UserData is Collider2D colB)
            {
                if (colA.IsTrigger || colB.IsTrigger)
                {
                    colA.RaiseTriggerEnter(colB);
                    colB.RaiseTriggerEnter(colA);
                }
                else
                {
                    var collision = new Collision2D { Collider = colA, OtherCollider = colB };
                    colA.RaiseCollisionEnter(collision);
                    colB.RaiseCollisionEnter(new Collision2D { Collider = colB, OtherCollider = colA });
                }
            }
        }

        public void EndContact(Contact contact)
        {
            var fixtureA = contact.FixtureA;
            var fixtureB = contact.FixtureB;
            if (fixtureA.UserData is Collider2D colA && fixtureB.UserData is Collider2D colB)
            {
                if (colA.IsTrigger || colB.IsTrigger)
                {
                    colA.RaiseTriggerExit(colB);
                    colB.RaiseTriggerExit(colA);
                }
                else
                {
                    var collision = new Collision2D { Collider = colA, OtherCollider = colB };
                    colA.RaiseCollisionExit(collision);
                    colB.RaiseCollisionExit(new Collision2D { Collider = colB, OtherCollider = colA });
                }
            }
        }

        public void PreSolve(Contact contact, in Manifold manifold) { }
        public void PostSolve(Contact contact, in ContactImpulse impulse) { }
    }

    private sealed class CollisionFilter : IContactFilter
    {
        private readonly PhysicsSystem _system;
        public CollisionFilter(PhysicsSystem system) => _system = system;

        public bool ShouldCollide(Fixture fixtureA, Fixture fixtureB)
        {
            var catA = fixtureA.Filter.CategoryBits;
            var catB = fixtureB.Filter.CategoryBits;
            var layerA = GetLayerFromCategory(catA);
            var layerB = GetLayerFromCategory(catB);
            return _system.GetLayerCollision(layerA, layerB);
        }

        private static int GetLayerFromCategory(ushort category)
        {
            for (int i = 0; i < 16; i++)
            {
                if ((category & (1 << i)) != 0)
                    return i;
            }
            return 0;
        }
    }
}
