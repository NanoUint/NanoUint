using NanoUint.Physics;
using System.Numerics;

namespace NanoUint.Tests;

public class PhysicsTests : IDisposable
{
    public PhysicsTests()
    {
        Physics2D.Dispose();
    }

    public void Dispose()
    {
        Physics2D.Dispose();
    }

    [Fact]
    public void Physics2D_DisabledByDefault()
    {
        Assert.False(Physics2D.Enabled);
    }

    [Fact]
    public void Physics2D_Enable_SetsEnabled()
    {
        Physics2D.Enable();
        Assert.True(Physics2D.Enabled);
    }

    [Fact]
    public void Physics2D_Disable_SetsDisabled()
    {
        Physics2D.Enable();
        Physics2D.Disable();
        Assert.False(Physics2D.Enabled);
    }

    [Fact]
    public void Physics2D_Dispose_ClearsSystem()
    {
        Physics2D.Enable();
        Physics2D.Dispose();
        Assert.False(Physics2D.Enabled);
    }

    [Fact]
    public void LayerCollision_DefaultAllTrue()
    {
        Physics2D.Enable();
        Assert.True(Physics2D.GetLayerCollision(0, 1));
        Assert.True(Physics2D.GetLayerCollision(5, 10));
        Assert.True(Physics2D.GetLayerCollision(31, 31));
    }

    [Fact]
    public void LayerCollision_SetFalse_DisablesCollision()
    {
        Physics2D.Enable();
        Physics2D.SetLayerCollision(0, 1, false);
        Assert.False(Physics2D.GetLayerCollision(0, 1));
        Assert.False(Physics2D.GetLayerCollision(1, 0));
    }

    [Fact]
    public void LayerCollision_SetTrue_RestoresCollision()
    {
        Physics2D.Enable();
        Physics2D.SetLayerCollision(0, 1, false);
        Physics2D.SetLayerCollision(0, 1, true);
        Assert.True(Physics2D.GetLayerCollision(0, 1));
    }

    [Fact]
    public void LayerCollision_SameLayer_CanDisable()
    {
        Physics2D.Enable();
        Physics2D.SetLayerCollision(5, 5, false);
        Assert.False(Physics2D.GetLayerCollision(5, 5));
    }

    [Fact]
    public void LayerCollision_ResetRestoresAll()
    {
        Physics2D.Enable();
        Physics2D.SetLayerCollision(0, 1, false);
        Physics2D.SetLayerCollision(2, 3, false);
        Physics2D.ResetLayerCollision();
        Assert.True(Physics2D.GetLayerCollision(0, 1));
        Assert.True(Physics2D.GetLayerCollision(2, 3));
    }

    [Fact]
    public void LayerCollision_OutOfRange_ReturnsTrue()
    {
        Physics2D.Enable();
        Assert.True(Physics2D.GetLayerCollision(-1, 0));
        Assert.True(Physics2D.GetLayerCollision(0, 32));
    }

    [Fact]
    public void LayerCollision_SetOutOfRange_IsIgnored()
    {
        Physics2D.Enable();
        Physics2D.SetLayerCollision(-1, 0, false);
        Physics2D.SetLayerCollision(0, 32, false);
        Assert.True(Physics2D.GetLayerCollision(0, 0));
    }

    [Fact]
    public void Rigidbody2D_DefaultValues()
    {
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        Assert.Equal(BodyType.Dynamic, rb.BodyType);
        Assert.Equal(1f, rb.GravityScale);
        Assert.Equal(0f, rb.LinearDamping);
        Assert.Equal(0f, rb.AngularDamping);
        Assert.False(rb.FixedRotation);
        Assert.False(rb.Bullet);
        Assert.Equal(0, rb.Layer);
    }

    [Fact]
    public void Rigidbody2D_Velocity_DefaultsToZero()
    {
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        Assert.Equal(Vector2.Zero, rb.Velocity);
        Assert.Equal(0f, rb.AngularVelocity);
    }

    [Fact]
    public void Rigidbody2D_BodyType_CanChange()
    {
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.BodyType = BodyType.Static;
        Assert.Equal(BodyType.Static, rb.BodyType);
        rb.BodyType = BodyType.Kinematic;
        Assert.Equal(BodyType.Kinematic, rb.BodyType);
    }

    [Fact]
    public void Rigidbody2D_GravityScale_CanSet()
    {
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.GravityScale = 0f;
        Assert.Equal(0f, rb.GravityScale);
        rb.GravityScale = -1f;
        Assert.Equal(-1f, rb.GravityScale);
    }

    [Fact]
    public void Rigidbody2D_WithPhysics_BodyIsCreated()
    {
        Physics2D.Enable();
        var go = new GameObject("Test");
        go.Transform.X = 1f;
        go.Transform.Y = 2f;
        var rb = go.AddComponent<Rigidbody2D>();
        var system = new PhysicsSystem();
        system.Enabled = true;
        system.RegisterBody(rb);
        Assert.NotNull(rb.Body);
        system.Dispose();
    }

    [Fact]
    public void Rigidbody2D_WithoutPhysics_BodyIsNull()
    {
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.Start();
        Assert.Null(rb.Body);
    }

    [Fact]
    public void Rigidbody2D_Destroy_UnregistersBody()
    {
        Physics2D.Enable();
        var go = new GameObject("Test");
        var rb = go.AddComponent<Rigidbody2D>();
        var system = new PhysicsSystem();
        system.Enabled = true;
        system.RegisterBody(rb);
        Assert.NotNull(rb.Body);
        system.UnregisterBody(rb);
        Assert.Null(rb.Body);
        system.Dispose();
    }

    [Fact]
    public void BoxCollider2D_DefaultValues()
    {
        var go = new GameObject("Test");
        var col = go.AddComponent<BoxCollider2D>();
        Assert.False(col.IsTrigger);
        Assert.Equal(new Vector2(1f, 1f), col.Size);
    }

    [Fact]
    public void CircleCollider2D_DefaultValues()
    {
        var go = new GameObject("Test");
        var col = go.AddComponent<CircleCollider2D>();
        Assert.False(col.IsTrigger);
        Assert.Equal(0.5f, col.Radius);
    }

    [Fact]
    public void PolygonCollider2D_DefaultValues()
    {
        var go = new GameObject("Test");
        var col = go.AddComponent<PolygonCollider2D>();
        Assert.False(col.IsTrigger);
        Assert.Empty(col.Vertices);
    }

    [Fact]
    public void Collider2D_Rigidbody_ReturnsNullWhenNoRigidbody()
    {
        var go = new GameObject("Test");
        var col = go.AddComponent<BoxCollider2D>();
        Assert.Null(col.Rigidbody);
    }

    [Fact]
    public void Collider2D_Rigidbody_ReturnsRigidbodyWhenPresent()
    {
        var go = new GameObject("Test");
        go.AddComponent<Rigidbody2D>();
        var col = go.AddComponent<BoxCollider2D>();
        Assert.NotNull(col.Rigidbody);
    }

    [Fact]
    public void PhysicsSystem_Step_WhenDisabled_DoesNothing()
    {
        Physics2D.Enable();
        var system = new PhysicsSystem();
        system.Enabled = false;
        system.Step(1f / 60f);
    }

    [Fact]
    public void PhysicsSystem_Gravity_CanSet()
    {
        Physics2D.Enable();
        var system = new PhysicsSystem();
        system.Gravity = new Vector2(0, -20f);
        Assert.Equal(new Vector2(0, -20f), system.Gravity);
    }

    [Fact]
    public void PhysicsSystem_InterpolationAlpha_DefaultsToZero()
    {
        Physics2D.Enable();
        var system = new PhysicsSystem();
        Assert.Equal(0f, system.InterpolationAlpha);
    }

    [Fact]
    public void Physics2D_StaticQueries_ReturnEmptyWhenNoSystem()
    {
        Assert.Null(Physics2D.Raycast(Vector2.Zero, Vector2.UnitX));
        Assert.Empty(Physics2D.RaycastAll(Vector2.Zero, Vector2.UnitX));
        Assert.Empty(Physics2D.OverlapCircle(Vector2.Zero, 1f));
        Assert.Empty(Physics2D.OverlapBox(Vector2.Zero, Vector2.One));
    }

    [Fact]
    public void Physics2D_Gravity_DefaultWhenNoSystem()
    {
        Assert.Equal(new Vector2(0, -9.81f), Physics2D.Gravity);
    }

    [Fact]
    public void Physics2D_StaticCalls_NoOpWhenDisabled()
    {
        Physics2D.Disable();
        Physics2D.SetLayerCollision(0, 1, false);
        Physics2D.ResetLayerCollision();
        Assert.False(Physics2D.Enabled);
    }
}
