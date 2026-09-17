using System;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 虫群（第 3 章·深空 新增）：高速蛇形逼近，极脆低火，
/// 用数量淹没防线 —— 章节新敌人的多态扩展（ULO2 证据增量）。
/// </summary>
public sealed class SwarmDrone : EnemyShip
{
    protected override float BehaviorSpeed => 260f;
    protected override float AggroRange => 650f;

    private float _phase; // 蛇形相位
    private const float SwayAmplitude = 140f;
    private const float SwayFrequency = 3.2f;

    public SwarmDrone()
        : base("虫群", hull: 18, shield: 0, armor: 0, firepower: 3f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }

        // 直线逼近 + 垂直摆动（蛇形），相位推进
        _phase += dt * SwayFrequency;
        float dx = playerX - X;
        float dy = playerY - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001f)
        {
            return;
        }

        // 法向偏移：朝玩家行进方向侧向摆动
        float nx = -dy / dist;
        float ny = dx / dist;
        float sway = MathF.Sin(_phase) * SwayAmplitude * dt;

        X += (dx / dist * BehaviorSpeed + nx * sway) * dt;
        Y += (dy / dist * BehaviorSpeed + ny * sway) * dt;
        ClampToWorld();
    }
}
