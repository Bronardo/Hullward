using Godot;

namespace Hullward.Game;

/// <summary>
/// 爆炸特效（Sprint 4 线 A fix版）：CPUParticles2D 径向爆裂particle，一次性爆发后auto消散自毁。
/// note：初版用 Kenney fire00-19 帧序column做animation——素材实为长条光束（观感如光柱），且animation未设
/// OneShot（SetAnimationLoop 误删）导致 AnimationFinished 永不trigger、node跨关卡累积。
/// particle方案无素材dependency、生命周期确定、必自毁（ULO1 fix证据）。
/// </summary>
public partial class ExplosionFx : Node2D
{
    public override void _Ready()
    {
        // 橙红→transparency渐隐
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

        // particle播完（0.45s + 余量）后自毁；SceneTreeTimer default Always，pausemedium也会倒计时，avoidance残留
        var timer = GetTree().CreateTimer(0.8f);
        timer.Timeout += QueueFree;
    }
}
