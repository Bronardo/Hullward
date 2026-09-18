using System;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 精英·旗舰护卫（Sprint 4 线 B1 · LD：第 3 章精英，高数值，行为可感知）。
/// 定位：介于远程炮艇与重装堡垒之间的压制型精英——中速逼近、保持射程环形、
/// 短冷却点射。高护盾高火力，是第 3 章（深空）的强度担当。
/// 域层多态扩展（ULO2：新敌人类 = 继承 EnemyShip 覆写行为/数值/射击）。
/// </summary>
public sealed class EliteGuardShip : EnemyShip
{
    protected override float BehaviorSpeed => 70f;
    protected override float AggroRange => 800f;

    private const float PreferredDistance = 300f;
    private const float Deadband = 60f;
    private const float FireRange = 700f;
    private const float FireInterval = 1.5f;

    private int _orbitDir = 1;
    private float _fireCooldown;

    public EliteGuardShip()
        : base("旗舰护卫", hull: 150, shield: 50, armor: 14, firepower: 13f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }

        float dx = playerX - X;
        float dy = playerY - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001f)
        {
            return;
        }

        // 保持 300 距离环形游走（射程内），过近退后、过远逼近（旗舰护卫式压迫）
        if (dist > PreferredDistance + Deadband)
        {
            MoveToward(playerX, playerY, BehaviorSpeed, dt);
        }
        else if (dist < PreferredDistance - Deadband)
        {
            MoveToward(X - dx / dist * 40f, Y - dy / dist * 40f, BehaviorSpeed * 0.6f, dt);
        }
        else
        {
            // 环形（沿切线方向）
            float tx = X + (-dy / dist) * _orbitDir * BehaviorSpeed * dt;
            float ty = Y + (dx / dist) * _orbitDir * BehaviorSpeed * dt;
            X = tx;
            Y = ty;
        }

        _fireCooldown = MathF.Max(0f, _fireCooldown - dt);
        ClampToWorld();
    }

    public override bool TryFire(float dt, float playerX, float playerY, out float targetX, out float targetY)
    {
        targetX = playerX;
        targetY = playerY;
        _fireCooldown = MathF.Max(0f, _fireCooldown - dt);
        if (_fireCooldown > 0f || Distance(X, Y, playerX, playerY) > FireRange)
        {
            return false;
        }
        _fireCooldown = FireInterval;
        return true;
    }
}
