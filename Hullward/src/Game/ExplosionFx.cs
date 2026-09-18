using Godot;

namespace Hullward.Game;

/// <summary>
/// 爆炸特效（Sprint 4 线 A 修复版）：CPUParticles2D 径向爆裂粒子，一次性爆发后自动消散自毁。
/// 说明：初版用 Kenney fire00-19 帧序列做动画——素材实为长条光束（观感如光柱），且动画未设
/// OneShot（SetAnimationLoop 误删）导致 AnimationFinished 永不触发、节点跨关卡累积。
/// 粒子方案无素材依赖、生命周期确定、必自毁（ULO1 修复证据）。
/// </summary>
public partial class ExplosionFx : Node2D
{
    public override void _Ready()
    {
        // 橙红→透明渐隐
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.82f, 0.45f, 1f));
        ramp.SetOffset(0, 0f);
        ramp.SetColor(1, new Color(1f, 0.55f, 0.22f, 0.9f));
        ramp.SetOffset(1, 0.4f);
        ramp.SetColor(2, new Color(0.55f, 0.18f, 0.08f, 0f));
        ramp.SetOffset(2, 1f);

        var particles = new CpuParticles2D
        {
            Emitting = true,
            Amount = 30,
            Lifetime = 0.45f,
            OneShot = true,          // 只爆发一次
            Explosiveness = 1f,      // 全部粒子同时爆开
            Direction = Vector2.Up,
            Spread = 180f,           // 全方向
            InitialVelocityMin = 70f,
            InitialVelocityMax = 240f,
            Gravity = new Vector2(0f, 80f),
            ScaleAmountMin = 2.5f,
            ScaleAmountMax = 6f,
            ColorRamp = ramp
        };
        AddChild(particles);

        // 粒子播完（0.45s + 余量）后自毁；SceneTreeTimer 默认 Always，暂停中也会倒计时，避免残留
        var timer = GetTree().CreateTimer(0.8f);
        timer.Timeout += QueueFree;
    }
}
