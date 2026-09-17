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

    private float _lifetime;

    public override void _Ready()
    {
        // 像素弹丸
        var dot = new ColorRect
        {
            Size = new Vector2(6, 6),
            Color = new Color("ffd166"),
            Position = new Vector2(-3, -3)
        };
        AddChild(dot);
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
