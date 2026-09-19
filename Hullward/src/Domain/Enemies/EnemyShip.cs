using System;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 暗骸enemy shipabstraction基类（ULO2 polymorphism核心：row为差异由子类 UpdateBehavior 提供，杜绝 if-else branch）。
/// position与hull均为纯 C# 域data，presentationnode仅做renderbridge。
/// </summary>
public abstract class EnemyShip : ShipBase, ITargetable
{
    /// <summary>域层position（世界position，presentation每帧sync到node）。</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>row为speedmax（子类定）。</summary>
    protected abstract float BehaviorSpeed { get; }

    /// <summary>索敌triggerdistance（玩家enter后start追击/攻击）。</summary>
    protected abstract float AggroRange { get; }

    public const float WorldHalfWidth = 950f;
    public const float WorldHalfHeight = 530f;

    protected EnemyShip(string name, int hull, int shield, int armor, float firepower)
        : base(name, hull, shield, armor, firepower, speed: 0f, moduleSlots: 0)
    {
    }

    /// <summary>polymorphismrow为入口：子类implement各自战术。</summary>
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);

    /// <summary>
    /// remote开火入口（LD Sprint 3 §4.2：gunboat弹幕）。
    /// 基类default不开火（近战enemy ship）；子类覆write：cooldown到且射程内back true 并给出弹道target点。
    /// </summary>
    public virtual bool TryFire(float dt, float playerX, float playerY, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        return false;
    }

    /// <summary>按星域level缩放强度（Zone4 = 2.5× hull / 1.75× firepower）。</summary>
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

    /// <summary>向targetdirection移动（子类call）。</summary>
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

/// <summary>recon：high速直线追击，轻甲轻火。</summary>
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
            return; // 未enter索敌range：待机
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>劫掠舰：保持medium距环形游走，medium等firepower。</summary>
public sealed class RaiderShip : EnemyShip
{
    protected override float BehaviorSpeed => 130f;
    protected override float AggroRange => 700f;

    private const float DesiredDistance = 300f;
    private const float Deadband = 40f;
    private int _orbitDir = 1; // 环形direction

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
            // distance合适：切向环绕
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
/// gunboat（LD Sprint 3 §4.2 第 2 章新enemy）：保持medium距环形游走，射程内周期性弹幕。
/// row为差异 = polymorphismextension（TryFire 覆write），近战enemy ship不受impact。
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
/// collapse禁区守卫（Boss，LD Sprint 3 §4.3 三phaseskill）：
/// phase 1（100%–60%）相位charge + spawn 2 recon + burst 3 连；
/// phase 2（60%–30%）new湮灭pulse AOE，skillrate +20%；
/// phase 3（30%–0%）enrage：chargerate提升、湮灭pulse双发、spawnassault ship。
/// state machine与skillschedule全在域层（可单测phaseswitch血量条件）；presentation执rowentity/视觉。
/// </summary>
public sealed class GuardianBoss : EnemyShip
{
    protected override float BehaviorSpeed => 42f;
    protected override float AggroRange => 950f;

    // phasethreshold（LD §4.3）
    public const float Phase2Threshold = 0.6f; // 60%
    public const float Phase3Threshold = 0.3f; // 30%

    private BossPhase _phase = BossPhase.Phase1;

    // skillcooldown（秒）
    private float _chargeCd = 5f;
    private float _summonCd = 14f;
    private float _pointCd = 3f;
    private float _pulseCd = 8f;

    // chargestate（域层移动，presentationcollision判定pathdamage）
    private bool _charging;
    private float _chargeTimer;
    private float _chargeDirX;
    private float _chargeDirY;
    private float _chargeSpeed;

    public GuardianBoss()
        : base("禁区守卫", hull: 500, shield: 180, armor: 25, firepower: 30f)
    {
    }

    /// <summary>currentphase（按hull比例real-time判定）。</summary>
    public BossPhase Phase => _phase;

    /// <summary>是否处于相位chargemedium（presentation据此做pathcollisiondamage与视觉）。</summary>
    public bool IsCharging => _charging;

    /// <summary>hull比例（0-1）。</summary>
    public float HullPct => MaxHull > 0 ? (float)Hull / MaxHull : 0f;

    /// <summary>phaseswitchevent（presentation弹提示/变色；单测verifythreshold）。</summary>
    public event Action<BossPhase>? PhaseChanged;

    /// <summary>phase判定并triggerswitchevent（每次callidempotent，仅跨threshold时trigger一次）。</summary>
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
            // 相位charge：沿lock定directionhigh速直线冲撞，pathdurationdamage由presentation按 IsCharging 判定
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
    /// skillschedule器（每帧call，至多back一个意图）：
    /// priority pulse &gt; charge &gt; spawn &gt; burst；cooldown按phaseratecorrection（P2 ×1.2，P3 ×1.6）。
    /// 纯域层逻辑，意图由presentation执row。
    /// </summary>
    public BossIntent TickSkills(float dt, float playerX, float playerY)
    {
        UpdatePhase();
        float freqMult = _phase switch
        {
            BossPhase.Phase2 => 1.2f, // LD：skillrate +20%
            BossPhase.Phase3 => 1.6f, // LD：enrage（chargerate提升、pulse双发）
            _ => 1f
        };

        _chargeCd -= dt * freqMult;
        _summonCd -= dt * freqMult;
        _pointCd -= dt * freqMult;
        _pulseCd -= dt * freqMult;

        // phase 2 起：湮灭pulse（P3 双发）
        if (_phase >= BossPhase.Phase2 && _pulseCd <= 0f)
        {
            int bursts = _phase == BossPhase.Phase3 ? 2 : 1;
            _pulseCd = 8f;
            return new BossIntent(BossSkillKind.AnnihilationPulse, bursts);
        }

        // 相位charge（P3 cooldown更短 → rate提升）
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
            _chargeSpeed = _phase == BossPhase.Phase3 ? 640f : 480f; // P3 charge更快
            _chargeCd = _phase == BossPhase.Phase3 ? 3f : 5f;
            return new BossIntent(BossSkillKind.PhaseCharge, 0);
        }

        // spawn（P1/P2 recon ×2，P3 assault ship，由presentation按 Phase 决定种类）
        if (_summonCd <= 0f)
        {
            _summonCd = _phase == BossPhase.Phase3 ? 12f : 14f;
            return new BossIntent(BossSkillKind.SummonScouts, 2);
        }

        // burst 3 连弹幕
        if (_pointCd <= 0f)
        {
            _pointCd = 3f;
            return new BossIntent(BossSkillKind.PointFire, 3);
        }

        return BossIntent.None;
    }
}
