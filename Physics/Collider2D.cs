using System.Numerics;

namespace NanoUint.Physics;

public abstract class Collider2D : Behaviour
{
    private bool _isTrigger;
    private Vector2 _offset;

    public bool IsTrigger
    {
        get => _isTrigger;
        set => _isTrigger = value;
    }

    public Vector2 Offset
    {
        get => _offset;
        set => _offset = value;
    }

    public Rigidbody2D? Rigidbody => GameObject?.GetComponent<Rigidbody2D>();

    internal abstract Box2DSharp.Dynamics.Fixture? CreateFixture(Box2DSharp.Dynamics.Body body);

    internal void RebuildFixture(Box2DSharp.Dynamics.Body body)
    {
        if (Fixture != null)
        {
            body.DestroyFixture(Fixture);
            Fixture = null;
        }
        Fixture = CreateFixture(body);
    }

    internal Box2DSharp.Dynamics.Fixture? Fixture { get; set; }

    public event Action<Collision2D>? OnCollisionEnter2D;
    public event Action<Collision2D>? OnCollisionStay2D;
    public event Action<Collision2D>? OnCollisionExit2D;
    public event Action<Collider2D>? OnTriggerEnter2D;
    public event Action<Collider2D>? OnTriggerStay2D;
    public event Action<Collider2D>? OnTriggerExit2D;

    internal void RaiseCollisionEnter(Collision2D c) => OnCollisionEnter2D?.Invoke(c);
    internal void RaiseCollisionStay(Collision2D c) => OnCollisionStay2D?.Invoke(c);
    internal void RaiseCollisionExit(Collision2D c) => OnCollisionExit2D?.Invoke(c);
    internal void RaiseTriggerEnter(Collider2D other) => OnTriggerEnter2D?.Invoke(other);
    internal void RaiseTriggerStay(Collider2D other) => OnTriggerStay2D?.Invoke(other);
    internal void RaiseTriggerExit(Collider2D other) => OnTriggerExit2D?.Invoke(other);
}

public sealed class BoxCollider2D : Collider2D
{
    private Vector2 _size = new(1f, 1f);

    public Vector2 Size
    {
        get => _size;
        set { _size = value; RebuildIfReady(); }
    }

    internal override Box2DSharp.Dynamics.Fixture? CreateFixture(Box2DSharp.Dynamics.Body body)
    {
        var shape = new Box2DSharp.Collision.Shapes.PolygonShape();
        shape.SetAsBox(_size.X * 0.5f, _size.Y * 0.5f);
        var def = new Box2DSharp.Dynamics.FixtureDef
        {
            Shape = shape,
            Density = 1.0f,
            IsSensor = IsTrigger
        };
        return body.CreateFixture(def);
    }

    private void RebuildIfReady()
    {
        if (Body != null && Fixture != null && GameObject != null)
            RebuildFixture(Body);
    }

    internal Box2DSharp.Dynamics.Body? Body => Rigidbody?.Body;
}

public sealed class CircleCollider2D : Collider2D
{
    private float _radius = 0.5f;

    public float Radius
    {
        get => _radius;
        set { _radius = value; RebuildIfReady(); }
    }

    internal override Box2DSharp.Dynamics.Fixture? CreateFixture(Box2DSharp.Dynamics.Body body)
    {
        var shape = new Box2DSharp.Collision.Shapes.CircleShape();
        shape.Radius = _radius;
        var def = new Box2DSharp.Dynamics.FixtureDef
        {
            Shape = shape,
            Density = 1.0f,
            IsSensor = IsTrigger
        };
        return body.CreateFixture(def);
    }

    private void RebuildIfReady()
    {
        if (Body != null && Fixture != null && GameObject != null)
            RebuildFixture(Body);
    }

    internal Box2DSharp.Dynamics.Body? Body => Rigidbody?.Body;
}

public sealed class PolygonCollider2D : Collider2D
{
    private Vector2[] _vertices = Array.Empty<Vector2>();

    public Vector2[] Vertices
    {
        get => _vertices;
        set { _vertices = value; RebuildIfReady(); }
    }

    internal override Box2DSharp.Dynamics.Fixture? CreateFixture(Box2DSharp.Dynamics.Body body)
    {
        if (_vertices.Length < 3) return null;
        var shape = new Box2DSharp.Collision.Shapes.PolygonShape();
        shape.Set(_vertices, _vertices.Length);
        var def = new Box2DSharp.Dynamics.FixtureDef
        {
            Shape = shape,
            Density = 1.0f,
            IsSensor = IsTrigger
        };
        return body.CreateFixture(def);
    }

    private void RebuildIfReady()
    {
        if (Body != null && Fixture != null && GameObject != null)
            RebuildFixture(Body);
    }

    internal Box2DSharp.Dynamics.Body? Body => Rigidbody?.Body;
}
