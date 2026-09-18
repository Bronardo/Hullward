using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// 主炮投射物（表现层）：直线飞向锁定目标，命中（距离判定）后对目标施加伤害。
/// </summary>
public partial class Projectile : Area2D
{
    public ITargetable? Target { get; set; }
    public float Speed { get; set; } = 520f;
    public int Damage { get; set; } = 10;
    public float HitRadius { get; set; } = 14f;
    public float MaxLifetime { get; set; } = 4f;

    /// <summary>暴击弹丸标记（词缀"致命一击/暴击增幅"）：尺寸/颜色增强，仅表现层。</summary>
    public bool IsCritical { get; set; }

    private float _lifetime;
    private Sprite2D _sprite = null!;

    public override void _Ready()
    {
        // Sprint 4 线 A：CC0 激光精灵（13×37 竖直弹体）；暴击更大更亮（金色提亮）
        _sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://assets/lasers/laser_player.png"),
            Scale = IsCritical ? new Vector2(1.5f, 1.5f) : Vector2.One,
            SelfModulate = IsCritical ? new Color(2.2f, 1.6f, 0.8f) : Colors.White,
            Centered = true
        };
        AddChild(_sprite);
        RotateToTarget();
    }

    private void RotateToTarget()
    {
        // 竖直弹体指向目标方向（纹理长轴沿 Y）
        if (Target != null)
        {
            var dir = new Vector2(Target.X, Target.Y) - Position;
            if (dir.LengthSquared() > 0.0001f)
            {
                Rotation = dir.Angle() - Mathf.Pi / 2f;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _lifetime += (float)delta;
        if (Target == null || _lifetime > MaxLifetime)
        {
            QueueFree();
            return;
        }

        // 目标已被击毁/释放（Godot 对象）：本投射物自行消失
        if (Target is GodotObject go && !GodotObject.IsInstanceValid(go))
        {
            QueueFree();
            return;
        }

        Vector2 toTarget = new Vector2(Target.X, Target.Y) - Position;
        if (toTarget.Length() < HitRadius)
        {
            Target.TakeHit(Damage);
            QueueFree();
            return;
        }

        Position += toTarget.Normalized() * Speed * (float)delta;
    }
}
