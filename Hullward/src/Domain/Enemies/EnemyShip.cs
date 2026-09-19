using System;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 暗骸敌舰抽象基类（ULO2 多态核心：行为差异由子类 UpdateBehavior 提供，杜绝 if-else 分支）。
/// 位置与耐久均为纯 C# 域数据，表现层节点仅做渲染桥接。
/// </summary>
public abstract class EnemyShip : ShipBase, ITargetable
{
    /// <summary>域层位置（世界坐标，表现层每帧同步到节点）。</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>行为速度上限（子类定）。</summary>
    protected abstract float BehaviorSpeed { get; }

    /// <summary>索敌触发距离（玩家进入后开始追击/攻击）。</summary>
    protected abstract float AggroRange { get; }

    public const float WorldHalfWidth = 950f;
    public const float WorldHalfHeight = 530f;

    protected EnemyShip(string name, int hull, int shield, int armor, float firepower)
        : base(name, hull, shield, armor, firepower, speed: 0f, moduleSlots: 0)
    {
    }

    /// <summary>多态行为入口：子类实现各自战术。</summary>
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);

    /// <summary>
    /// 远程开火入口（LD Sprint 3 §4.2：远程炮艇弹幕）。
    /// 基类默认不开火（近战敌舰）；子类覆写：冷却到且射程内返回 true 并给出弹道目标点。
    /// </summary>
    public virtual bool TryFire(float dt, float playerX, float playerY, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        return false;
    }

    /// <summary>按星域等级缩放强度（Zone4 = 2.5× 耐久 / 1.75× 火力）。</summary>
    public void ScaleForZone(int zoneLevel)
    {
        if (zoneLevel < 1)
        {
            zoneLevel = 1;
        }
        float hullMult = 1f + 0.5f * (zoneLevel - 1);
        float dmgMult = 1f + 0.25f * (zoneLevel - 1);
        Hull = (int)(Hull * hullMult);
        Shield = (int)(Shield * hullMult);
        Firepower *= dmgMult;
    }

    /// <summary>向目标方向移动（子类调用）。</summary>
    protected void MoveToward(float tx, float ty, float speed, float dt)
    {
        float dx = tx - X;
        float dy = ty - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001f)
        {
            return;
        }
        X += dx / dist * speed * dt;
        Y += dy / dist * speed * dt;
        ClampToWorld();
    }

    protected void ClampToWorld()
    {
        X = Math.Clamp(X, -WorldHalfWidth, WorldHalfWidth);
        Y = Math.Clamp(Y, -WorldHalfHeight, WorldHalfHeight);
    }

    protected static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>侦察机：高速直线追击，轻甲轻火。</summary>
public sealed class ReconDrone : EnemyShip
{
    protected override float BehaviorSpeed => 190f;
    protected override float AggroRange => 600f;

    public ReconDrone()
        : base("侦察机", hull: 30, shield: 0, armor: 0, firepower: 4f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return; // 未进入索敌范围：待机
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>劫掠舰：保持中距环形游走，中等火力。</summary>
public sealed class RaiderShip : EnemyShip
{
    protected override float BehaviorSpeed => 130f;
    protected override float AggroRange => 700f;

    private const float DesiredDistance = 300f;
    private const float Deadband = 40f;
    private int _orbitDir = 1; // 环形方向

    public RaiderShip()
        : base("劫掠舰", hull: 70, shield: 20, armor: 5, firepower: 8f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        float dist = Distance(X, Y, playerX, playerY);
        if (dist > AggroRange)
        {
            return;
        }

        if (dist > DesiredDistance + Deadband)
        {
            // 太远：逼近
            MoveToward(playerX, playerY, BehaviorSpeed, dt);
        }
        else if (dist < DesiredDistance - Deadband)
        {
            // 太近：后退
            MoveToward(X - (playerX - X), Y - (playerY - Y), BehaviorSpeed, dt);
        }
        else
        {
            // 距离合适：切向环绕
            float tangentX = -(playerY - Y) * _orbitDir;
            float tangentY = (playerX - X) * _orbitDir;
            X += tangentX / dist * BehaviorSpeed * dt;
            Y += tangentY / dist * BehaviorSpeed * dt;
            ClampToWorld();
        }
    }
}

/// <summary>重装堡垒：慢速逼近，重甲重火，正面威胁核心。</summary>
public sealed class HeavyFortress : EnemyShip
{
    protected override float BehaviorSpeed => 55f;
    protected override float AggroRange => 500f;

    public HeavyFortress()
        : base("重装堡垒", hull: 220, shield: 80, armor: 18, firepower: 18f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>
/// 远程炮艇（LD Sprint 3 §4.2 第 2 章新敌人）：保持中距环形游走，射程内周期性弹幕。
/// 行为差异 = 多态扩展（TryFire 覆写），近战敌舰不受影响。
/// </summary>
public sealed class GunboatShip : EnemyShip
{
    protected override float BehaviorSpeed => 95f;
    protected override float AggroRange => 850f;

    private const float PreferredDistance = 300f;
    private const float Deadband = 60f;
    private const float FireRange = 620f;
    private const float FireInterval = 1.8f;

    private int _orbitDir = 1;
    private float _fireCooldown;

    public GunboatShip()
        : base("Gunboat", hull: 90, shield: 30, armor: 8, firepower: 9f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        float dist = Distance(X, Y, playerX, playerY);
        if (dist > AggroRange)
        {
            return;
        }

        if (dist > PreferredDistance + Deadband)
        {
            MoveToward(playerX, playerY, BehaviorSpeed, dt);
        }
        else if (dist < PreferredDistance - Deadband)
        {
            MoveToward(X - (playerX - X), Y - (playerY - Y), BehaviorSpeed, dt);
        }
        else
        {
            float tangentX = -(playerY - Y) * _orbitDir;
            float tangentY = (playerX - X) * _orbitDir;
            X += tangentX / dist * BehaviorSpeed * dt;
            Y += tangentY / dist * BehaviorSpeed * dt;
            ClampToWorld();
        }
    }

    public override bool TryFire(float dt, float playerX, float playerY, out float targetX, out float targetY)
    {
        targetX = playerX;
        targetY = playerY;
        _fireCooldown -= dt;
        if (_fireCooldown > 0f)
        {
            return false;
        }
        if (Distance(X, Y, playerX, playerY) > FireRange)
        {
            return false;
        }
        _fireCooldown = FireInterval;
        return true;
    }
}

/// <summary>
/// 坍缩禁区守卫（Boss，LD Sprint 3 §4.3 三阶段技能）：
/// 阶段 1（100%–60%）相位冲锋 + 召唤 2 侦察机 + 点射 3 连；
/// 阶段 2（60%–30%）新增湮灭脉冲 AOE，技能频率 +20%；
/// 阶段 3（30%–0%）狂暴：冲锋频率提升、湮灭脉冲双发、召唤突击舰。
/// 状态机与技能调度全在域层（可单测阶段切换血量条件）；表现层执行实体/视觉。
/// </summary>
public sealed class GuardianBoss : EnemyShip
{
    protected override float BehaviorSpeed => 42f;
    protected override float AggroRange => 950f;

    // 阶段阈值（LD §4.3）
    public const float Phase2Threshold = 0.6f; // 60%
    public const float Phase3Threshold = 0.3f; // 30%

    private BossPhase _phase = BossPhase.Phase1;

    // 技能冷却（秒）
    private float _chargeCd = 5f;
    private float _summonCd = 14f;
    private float _pointCd = 3f;
    private float _pulseCd = 8f;

    // 冲锋状态（域层移动，表现层碰撞判定路径伤害）
    private bool _charging;
    private float _chargeTimer;
    private float _chargeDirX;
    private float _chargeDirY;
    private float _chargeSpeed;

    public GuardianBoss()
        : base("禁区守卫", hull: 500, shield: 180, armor: 25, firepower: 30f)
    {
    }

    /// <summary>当前阶段（按耐久比例实时判定）。</summary>
    public BossPhase Phase => _phase;

    /// <summary>是否处于相位冲锋中（表现层据此做路径碰撞伤害与视觉）。</summary>
    public bool IsCharging => _charging;

    /// <summary>耐久比例（0-1）。</summary>
    public float HullPct => MaxHull > 0 ? (float)Hull / MaxHull : 0f;

    /// <summary>阶段切换事件（表现层弹提示/变色；单测验证阈值）。</summary>
    public event Action<BossPhase>? PhaseChanged;

    /// <summary>阶段判定并触发切换事件（每次调用幂等，仅跨阈值时触发一次）。</summary>
    public BossPhase UpdatePhase()
    {
        BossPhase next = HullPct > Phase2Threshold
            ? BossPhase.Phase1
            : HullPct > Phase3Threshold
                ? BossPhase.Phase2
                : BossPhase.Phase3;
        if (next != _phase)
        {
            _phase = next;
            PhaseChanged?.Invoke(_phase);
        }
        return _phase;
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        UpdatePhase();
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }

        if (_charging)
        {
            // 相位冲锋：沿锁定方向高速直线冲撞，路径持续伤害由表现层按 IsCharging 判定
            _chargeTimer -= dt;
            X += _chargeDirX * _chargeSpeed * dt;
            Y += _chargeDirY * _chargeSpeed * dt;
            ClampToWorld();
            if (_chargeTimer <= 0f)
            {
                _charging = false;
            }
            return;
        }

        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }

    /// <summary>
    /// 技能调度器（每帧调用，至多返回一个意图）：
    /// 优先级 脉冲 &gt; 冲锋 &gt; 召唤 &gt; 点射；冷却按阶段频率修正（P2 ×1.2，P3 ×1.6）。
    /// 纯域层逻辑，意图由表现层执行。
    /// </summary>
    public BossIntent TickSkills(float dt, float playerX, float playerY)
    {
        UpdatePhase();
        float freqMult = _phase switch
        {
            BossPhase.Phase2 => 1.2f, // LD：技能频率 +20%
            BossPhase.Phase3 => 1.6f, // LD：狂暴（冲锋频率提升、脉冲双发）
            _ => 1f
        };

        _chargeCd -= dt * freqMult;
        _summonCd -= dt * freqMult;
        _pointCd -= dt * freqMult;
        _pulseCd -= dt * freqMult;

        // 阶段 2 起：湮灭脉冲（P3 双发）
        if (_phase >= BossPhase.Phase2 && _pulseCd <= 0f)
        {
            int bursts = _phase == BossPhase.Phase3 ? 2 : 1;
            _pulseCd = 8f;
            return new BossIntent(BossSkillKind.AnnihilationPulse, bursts);
        }

        // 相位冲锋（P3 冷却更短 → 频率提升）
        if (_chargeCd <= 0f)
        {
            float dx = playerX - X;
            float dy = playerY - Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 0.001f)
            {
                _chargeDirX = dx / dist;
                _chargeDirY = dy / dist;
            }
            else
            {
                _chargeDirX = 1f;
                _chargeDirY = 0f;
            }
            _charging = true;
            _chargeTimer = 1.1f;
            _chargeSpeed = _phase == BossPhase.Phase3 ? 640f : 480f; // P3 冲锋更快
            _chargeCd = _phase == BossPhase.Phase3 ? 3f : 5f;
            return new BossIntent(BossSkillKind.PhaseCharge, 0);
        }

        // 召唤（P1/P2 侦察机 ×2，P3 突击舰，由表现层按 Phase 决定种类）
        if (_summonCd <= 0f)
        {
            _summonCd = _phase == BossPhase.Phase3 ? 12f : 14f;
            return new BossIntent(BossSkillKind.SummonScouts, 2);
        }

        // 点射 3 连弹幕
        if (_pointCd <= 0f)
        {
            _pointCd = 3f;
            return new BossIntent(BossSkillKind.PointFire, 3);
        }

        return BossIntent.None;
    }
}
