using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// 主炮投射物（presentation）：直线飞向lock定target，hit（distance判定）后对target施加damage。
/// </summary>
public partial class Projectile : Area2D
{
    public ITargetable? Target { get; set; }
    public float Speed { get; set; } = 520f;
    public int Damage { get; set; } = 10;
    public float HitRadius { get; set; } = 14f;
    public float MaxLifetime { get; set; } = 4f;

    /// <summary>crit弹丸marker（affix"fatal一击/crit增幅"）：尺寸/颜色增强，仅presentation。</summary>
    public bool IsCritical { get; set; }

    private float _lifetime;
    private Sprite2D _sprite = null!;

    public override void _Ready()
    {
        // Sprint 4 线 A：CC0 激光精灵（13×37 竖直弹体）；crit更大更亮（金色提亮）
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
        // 竖直弹体指向targetdirection（纹理长轴沿 Y）
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

        // target已被击毁/释放（Godot object）：本投射物自row消失
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
