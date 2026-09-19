using System;

namespace Hullward.Domain.Enemies;

/// <summary>
/// swarm（第 3 章·deep space new）：high速蛇形逼近，极脆low火，
/// 用数量淹没防线 —— sector新enemy的polymorphismextension（ULO2 证据增量）。
/// Sprint 4 线 B1：补充"群体charge"row为——每只虫按个体random相位周期性enter
/// charge态（直冲玩家、2.4× speed、0.7s duration），整体呈现wavecharge感。
/// </summary>
public sealed class SwarmDrone : EnemyShip
{
    protected override float BehaviorSpeed => 260f;
    protected override float AggroRange => 650f;

    private float _phase; // 蛇形相位
    private const float SwayAmplitude = 140f;
    private const float SwayFrequency = 3.2f;

    // 群体charge（Sprint 4 线 B1）
    private const float ChargeSpeedMult = 2.4f;
    private const float ChargeDuration = 0.7f;
    private const float MinChargeCooldown = 2.5f;
    private const float MaxChargeCooldown = 4.5f;
    private readonly Random _rng;
    private bool _charging;
    private float _chargeTimer;
    private float _chargeCooldown;

    /// <summary>是否处于charge态（域层可测）。</summary>
    public bool IsCharging => _charging;

    /// <summary>距下次charge剩余秒数（域层可测：个体错开）。</summary>
    public float ChargeCountdown => _chargeCooldown;

    public SwarmDrone() : this(new Random())
    {
    }

    /// <param name="rng">random源（test注入fixed seed 获得deterministic相位）。</param>
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

        // charge态：直冲玩家（无摆动），0.7s 后exit
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

        // 非charge：蛇形逼近；cooldown到 0 trigger下一次charge
        _chargeCooldown -= dt;
        if (_chargeCooldown <= 0f)
        {
            _charging = true;
            _chargeTimer = ChargeDuration;
            _chargeCooldown = NextChargeCooldown();
        }

        // 直线逼近 + 垂直摆动（蛇形），相位推进
        _phase += dt * SwayFrequency;

        // 法向偏移：朝玩家row进direction侧向摆动
        float nx = -dy / dist;
        float ny = dx / dist;
        float sway = MathF.Sin(_phase) * SwayAmplitude * dt;

        X += (dx / dist * BehaviorSpeed + nx * sway) * dt;
        Y += (dy / dist * BehaviorSpeed + ny * sway) * dt;
        ClampToWorld();
    }
}
