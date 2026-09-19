using Godot;

namespace Hullward.Game;

/// <summary>
/// Boss 湮灭pulse（LD Sprint 3 §4.3）：range AOE，预兆后闪避。
/// 预兆期 0.9s 红圈由内向外扩大 → 爆炸对半径内玩家造成damage（shield/hull）。
/// 纯presentationnode；damagestat由call方（Boss 意图执row）按 Boss firepower提供。
/// </summary>
public partial class BossPulse : Node2D
{
    public PlayerShip? Player { get; set; }
    public int Damage { get; set; }
    public float Radius { get; set; } = 170f;

    private const float WarnTime = 0.9f;
    private float _life = WarnTime;
    private bool _exploded;
    private float _grow; // 半径绘制进度 0-1

    public override void _Ready()
    {
        ZIndex = 5;
        _grow = 0f;
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        _grow = 1f - Mathf.Max(0f, _life / WarnTime); // 0 → 1
        QueueRedraw();

        if (_life <= 0f && !_exploded)
        {
            _exploded = true;
            if (Player != null && Player.Position.DistanceTo(Position) <= Radius)
            {
                Player.TakeDamage(Damage, null);
            }
            QueueFree();
        }
    }

    public override void _Draw()
    {
        // 预兆圈：内圈已覆盖区域（淡红填充），外圈收缩（即将爆炸range）
        float coverRadius = Radius * _grow;
        Color inner = new Color(1f, 0.2f, 0.25f, 0.35f * _grow);
        DrawCircle(Vector2.Zero, coverRadius, inner);
        DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, new Color(1f, 0.35f, 0.4f, 0.9f), 3f);
        // 剩余time环（越细越接近爆炸）
        float remain = Mathf.Max(0f, _life / WarnTime);
        DrawArc(Vector2.Zero, Radius + 12f, 0f, Mathf.Tau * (1f - remain), 48, new Color(1f, 0.9f, 0.4f, 0.8f), 2f);
    }
}
