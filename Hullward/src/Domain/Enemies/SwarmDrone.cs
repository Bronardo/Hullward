using System;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 虫群（第 3 章·深空 新增）：高速蛇形逼近，极脆低火，
/// 用数量淹没防线 —— 章节新敌人的多态扩展（ULO2 证据增量）。
/// Sprint 4 线 B1：补充"群体冲锋"行为——每只虫按个体随机相位周期性进入
/// 冲锋态（直冲玩家、2.4× 速度、0.7s 持续），整体呈现波次冲锋感。
/// </summary>
public sealed class SwarmDrone : EnemyShip
{
    protected override float BehaviorSpeed => 260f;
    protected override float AggroRange => 650f;

    private float _phase; // 蛇形相位
    private const float SwayAmplitude = 140f;
    private const float SwayFrequency = 3.2f;

    // 群体冲锋（Sprint 4 线 B1）
    private const float ChargeSpeedMult = 2.4f;
    private const float ChargeDuration = 0.7f;
    private const float MinChargeCooldown = 2.5f;
    private const float MaxChargeCooldown = 4.5f;
    private readonly Random _rng;
    private bool _charging;
    private float _chargeTimer;
    private float _chargeCooldown;

    /// <summary>是否处于冲锋态（域层可测）。</summary>
    public bool IsCharging => _charging;

    /// <summary>距下次冲锋剩余秒数（域层可测：个体错开）。</summary>
    public float ChargeCountdown => _chargeCooldown;

    public SwarmDrone() : this(new Random())
    {
    }

    /// <param name="rng">随机源（测试注入固定 seed 获得确定性相位）。</param>
    public SwarmDrone(Random rng)
        : base("虫群", hull: 18, shield: 0, armor: 0, firepower: 3f)
    {
        _rng = rng;
        _chargeCooldown = NextChargeCooldown();
    }

    private float NextChargeCooldown()
        => MinChargeCooldown + (float)_rng.NextDouble() * (MaxChargeCooldown - MinChargeCooldown);

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

        // 冲锋态：直冲玩家（无摆动），0.7s 后退出
        if (_charging)
        {
            X += dx / dist * BehaviorSpeed * ChargeSpeedMult * dt;
            Y += dy / dist * BehaviorSpeed * ChargeSpeedMult * dt;
            _chargeTimer -= dt;
            if (_chargeTimer <= 0f)
            {
                _charging = false;
            }
            ClampToWorld();
            return;
        }

        // 非冲锋：蛇形逼近；冷却到 0 触发下一次冲锋
        _chargeCooldown -= dt;
        if (_chargeCooldown <= 0f)
        {
            _charging = true;
            _chargeTimer = ChargeDuration;
            _chargeCooldown = NextChargeCooldown();
        }

        // 直线逼近 + 垂直摆动（蛇形），相位推进
        _phase += dt * SwayFrequency;

        // 法向偏移：朝玩家行进方向侧向摆动
        float nx = -dy / dist;
        float ny = dx / dist;
        float sway = MathF.Sin(_phase) * SwayAmplitude * dt;

        X += (dx / dist * BehaviorSpeed + nx * sway) * dt;
        Y += (dy / dist * BehaviorSpeed + ny * sway) * dt;
        ClampToWorld();
    }
}
