using System.Numerics;

namespace NanoUint.Physics;

public enum BodyType
{
    Dynamic,
    Kinematic,
    Static
}

public sealed class Rigidbody2D : Behaviour
{
    private BodyType _bodyType = BodyType.Dynamic;
    private float _gravityScale = 1f;
    private float _linearDamping;
    private float _angularDamping;
    private bool _fixedRotation;
    private bool _bullet;
    private int _layer;
    private Box2DSharp.Dynamics.Body? _body;

    private Vector2 _previousPosition;
    private float _previousRotation;

    internal Box2DSharp.Dynamics.Body? Body { get => _body; set => _body = value; }

    public BodyType BodyType
    {
        get => _bodyType;
        set { _bodyType = value; if (_body != null) _body.BodyType = MapBodyType(value); }
    }

    public float GravityScale
    {
        get => _gravityScale;
        set => _gravityScale = value;
    }

    public float LinearDamping
    {
        get => _linearDamping;
        set { _linearDamping = value; if (_body != null) _body.LinearDamping = value; }
    }

    public float AngularDamping
    {
        get => _angularDamping;
        set { _angularDamping = value; if (_body != null) _body.AngularDamping = value; }
    }

    public bool FixedRotation
    {
        get => _fixedRotation;
        set { _fixedRotation = value; if (_body != null) _body.IsFixedRotation = value; }
    }

    public bool Bullet
    {
        get => _bullet;
        set { _bullet = value; if (_body != null) _body.IsBullet = value; }
    }

    public int Layer
    {
        get => _layer;
        set
        {
            var old = _layer;
            _layer = value;
            if (_body != null && old != value)
            {
                foreach (var fixture in _body.FixtureList)
                {
                    var filter = fixture.Filter;
                    filter.CategoryBits = (ushort)(1 << value);
                    var physics = Application.Default?.Physics;
                    if (physics != null)
                        filter.MaskBits = (ushort)physics.ComputeMaskBits(value);
                    fixture.Filter = filter;
                }
            }
        }
    }

    public Vector2 Velocity
    {
        get => _body?.LinearVelocity ?? Vector2.Zero;
        set { _body?.SetLinearVelocity(value); }
    }

    public float AngularVelocity
    {
        get => _body?.AngularVelocity ?? 0f;
        set { _body?.SetAngularVelocity(value); }
    }

    public void ApplyLinearImpulse(Vector2 impulse)
    {
        if (_body == null) return;
        var wc = _body.GetWorldCenter();
        _body.ApplyLinearImpulse(impulse, wc, true);
    }

    public void ApplyLinearImpulse(Vector2 impulse, Vector2 worldPoint)
    {
        _body?.ApplyLinearImpulse(impulse, worldPoint, true);
    }

    public void ApplyTorque(float torque)
    {
        _body?.ApplyTorque(torque, true);
    }

    public void SetPosition(Vector2 position)
    {
        if (_body != null)
        {
            var angle = _body.GetAngle();
            _body.SetTransform(position, angle);
        }
    }

    public void SetRotation(float radians)
    {
        if (_body != null)
        {
            var pos = _body.GetPosition();
            _body.SetTransform(pos, radians);
        }
    }

    internal static Box2DSharp.Dynamics.BodyType MapBodyType(BodyType type) => type switch
    {
        BodyType.Dynamic => Box2DSharp.Dynamics.BodyType.DynamicBody,
        BodyType.Kinematic => Box2DSharp.Dynamics.BodyType.KinematicBody,
        BodyType.Static => Box2DSharp.Dynamics.BodyType.StaticBody,
        _ => Box2DSharp.Dynamics.BodyType.DynamicBody
    };

    internal void SyncToTransform()
    {
        if (_body == null || GameObject == null) return;
        var pos = _body.GetPosition();
        var go = GameObject;
        go.Transform.Position = new Drawing.Vector3(pos.X, pos.Y, go.Transform.Position.Z);
    }

    internal void SyncFromTransform()
    {
        if (_body == null || GameObject == null) return;
        var t = GameObject.Transform;
        var pos = new Vector2(t.WorldX, t.WorldY);
        _body.SetTransform(pos, _body.GetAngle());
    }

    internal void CapturePreviousState()
    {
        if (_body == null) return;
        _previousPosition = _body.GetPosition();
        _previousRotation = _body.GetAngle();
    }

    public Vector2 InterpolatedPosition(float alpha)
    {
        if (_body == null) return Vector2.Zero;
        var current = _body.GetPosition();
        return Vector2.Lerp(_previousPosition, current, alpha);
    }

    public float InterpolatedRotation(float alpha)
    {
        if (_body == null) return 0f;
        var current = _body.GetAngle();
        return _previousRotation + (current - _previousRotation) * alpha;
    }

    protected internal override void Start()
    {
        base.Start();
        var physics = Application.Default?.Physics;
        if (physics != null && physics.Enabled)
            physics.RegisterBody(this);
    }

    protected internal override void OnDestroy()
    {
        var physics = Application.Default?.Physics;
        if (physics != null && _body != null)
            physics.UnregisterBody(this);
        base.OnDestroy();
    }
}
