using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// enemy弹幕（presentation，LD Sprint 3 §4.2 gunboat）：沿fixeddirection飞row，hit玩家造成damage。
/// thorns镀层：hit玩家时经 Player.TakeDamage 反弹给发射者（Attacker）。
/// </summary>
public partial class EnemyProjectile : Area2D
{
    public PlayerShip? Player { get; set; }

    /// <summary>发射者（gunboatpresentationnode）：thorns的target（ITargetable）。</summary>
    public ITargetable? Attacker { get; set; }

    public float Speed { get; set; } = 300f;
    public int Damage { get; set; } = 5;
    public float MaxLifetime { get; set; } = 6f;
    public Vector2 Direction { get; set; }

    private float _lifetime;

    public override void _Ready()
    {
        // Sprint 4 线 A：CC0 enemy激光精灵，旋转指向飞rowdirection（纹理长轴沿 Y）
        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://assets/lasers/laser_enemy.png"),
            Centered = true
        };
        AddChild(sprite);
        if (Direction.LengthSquared() > 0.0001f)
        {
            Rotation = Direction.Angle() - Mathf.Pi / 2f;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _lifetime += (float)delta;
        if (Player == null || _lifetime > MaxLifetime || !IsInstanceValid(Player))
        {
            QueueFree();
            return;
        }

        Position += Direction * Speed * (float)delta;

        if (Position.DistanceTo(Player.Position) < 12f)
        {
            Player.TakeDamage(Damage, Attacker); // thorns经 TakeDamage 反弹给发射者
            QueueFree();
        }
    }
}
