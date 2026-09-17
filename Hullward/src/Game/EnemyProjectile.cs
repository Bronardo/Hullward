using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// 敌方弹幕（表现层，LD Sprint 3 §4.2 远程炮艇）：沿固定方向飞行，命中玩家造成伤害。
/// 反伤镀层：命中玩家时经 Player.TakeDamage 反弹给发射者（Attacker）。
/// </summary>
public partial class EnemyProjectile : Area2D
{
    public PlayerShip? Player { get; set; }

    /// <summary>发射者（远程炮艇表现层节点）：反伤的目标（ITargetable）。</summary>
    public ITargetable? Attacker { get; set; }

    public float Speed { get; set; } = 300f;
    public int Damage { get; set; } = 5;
    public float MaxLifetime { get; set; } = 6f;
    public Vector2 Direction { get; set; }

    private float _lifetime;

    public override void _Ready()
    {
        // 敌方弹丸：亮红
        var dot = new ColorRect
        {
            Size = new Vector2(8, 8),
            Color = new Color("ff4d4d"),
            Position = new Vector2(-4, -4)
        };
        AddChild(dot);
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
            Player.TakeDamage(Damage, Attacker); // 反伤经 TakeDamage 反弹给发射者
            QueueFree();
        }
    }
}
