using System;

namespace Hullward.Domain.Enemies;

/// <summary>
/// elite·elite guard（Sprint 4 线 B1 · LD：第 3 章elite，highstat，row为可感知）。
/// locate：介于gunboat与重装堡垒between的压制型elite——medium速逼近、保持射程环形、
/// 短cooldownburst。highshieldhighfirepower，是第 3 章（deep space）的强度担当。
/// 域层polymorphismextension（ULO2：新enemy类 = inheritance EnemyShip 覆writerow为/stat/射击）。
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
        : base("Elite Guard", hull: 150, shield: 50, armor: 14, firepower: 13f)
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

        // 保持 300 distance环形游走（射程内），过近退后、过远逼近（elite guard式压迫）
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
            // 环形（沿切线direction）
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
