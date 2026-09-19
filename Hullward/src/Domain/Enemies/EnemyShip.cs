using System;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Enemies;

/// <summary>
/// Hullward enemy ship base class (ULO2 polymorphism: per-ship differences provided by subclass UpdateBehavior, no if-else branches)。
/// Position and hull are pure C# domain data; presentation node is just a render bridge。
/// </summary>
public abstract class EnemyShip : ShipBase, ITargetable
{
    /// <summary>Domain-layer position (world position; presentation syncs to node every frame)。</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>Speed max per subclass。</summary>
    protected abstract float BehaviorSpeed { get; }

    /// <summary>Targeting trigger distance (start chasing/attacking when player enters)。</summary>
    protected abstract float AggroRange { get; }

    public const float WorldHalfWidth = 950f;
    public const float WorldHalfHeight = 530f;

    protected EnemyShip(string name, int hull, int shield, int armor, float firepower)
        : base(name, hull, shield, armor, firepower, speed: 0f, moduleSlots: 0)
    {
    }

    /// <summary>Polymorphism entry point: subclasses implement their own tactics。</summary>
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);

    /// <summary>
    /// Ranged fire entry (LD Sprint 3 §4.2: gunboat barrage)。
    /// Base class does not fire by default (melee enemies); subclasses override: return true when cooldown ready and in range, and provide projectile target point。
    /// </summary>
    public virtual bool TryFire(float dt, float playerX, float playerY, out float targetX, out float targetY)
    {
        targetX = 0f;
        targetY = 0f;
        return false;
    }

    /// <summary>Scale stats by sector level (Zone 4 = 2.5x hull / 1.75x firepower)。</summary>
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

    /// <summary>Move toward target direction (called by subclasses)。</summary>
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

/// <summary>Recon: fast straight chase, light armor/weapons。</summary>
public sealed class ReconDrone : EnemyShip
{
    protected override float BehaviorSpeed => 190f;
    protected override float AggroRange => 600f;

    public ReconDrone()
        : base("Recon", hull: 30, shield: 0, armor: 0, firepower: 4f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return; // Player not in targeting range: idle
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>Raider: orbits at medium range, medium firepower。</summary>
public sealed class RaiderShip : EnemyShip
{
    protected override float BehaviorSpeed => 130f;
    protected override float AggroRange => 700f;

    private const float DesiredDistance = 300f;
    private const float Deadband = 40f;
    private int _orbitDir = 1; // Orbit direction

    public RaiderShip()
        : base("Raider", hull: 70, shield: 20, armor: 5, firepower: 8f)
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
            // Too far: approach
            MoveToward(playerX, playerY, BehaviorSpeed, dt);
        }
        else if (dist < DesiredDistance - Deadband)
        {
            // Too close: back off
            MoveToward(X - (playerX - X), Y - (playerY - Y), BehaviorSpeed, dt);
        }
        else
        {
            // Good distance: orbit tangentially
            float tangentX = -(playerY - Y) * _orbitDir;
            float tangentY = (playerX - X) * _orbitDir;
            X += tangentX / dist * BehaviorSpeed * dt;
            Y += tangentY / dist * BehaviorSpeed * dt;
            ClampToWorld();
        }
    }
}

/// <summary>Heavy Bastion: slow approach, heavy armor/weapons, frontline threat。</summary>
public sealed class HeavyFortress : EnemyShip
{
    protected override float BehaviorSpeed => 55f;
    protected override float AggroRange => 500f;

    public HeavyFortress()
        : base("Heavy Bastion", hull: 220, shield: 80, armor: 18, firepower: 18f)
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
/// Gunboat (LD Sprint 3 §4.2, new in sector 2): orbits at medium range, periodic barrage when in range。
/// Per-ship difference = polymorphism extension (TryFire override); melee enemies unaffected。
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
/// Collapse Zone Guardian (Boss, LD Sprint 3 §4.3, 3-phase skill set)：
/// Phase 1 (100%-60%): phase charge + spawn 2 recon + burst of 3；
/// Phase 2 (60%-30%): new annihilation pulse AOE, skill rate +20%；
/// Phase 3 (30%-0%): enrage: charge rate up, annihilation pulse doubled, spawn assault ships。
/// State machine and skill scheduler are pure domain (phase-switch HP conditions unit-testable); presentation executes visuals。
/// </summary>
public sealed class GuardianBoss : EnemyShip
{
    protected override float BehaviorSpeed => 42f;
    protected override float AggroRange => 950f;

    // phasethreshold（LD §4.3）
    public const float Phase2Threshold = 0.6f; // 60%
    public const float Phase3Threshold = 0.3f; // 30%

    private BossPhase _phase = BossPhase.Phase1;

    // Skill cooldown (seconds)
    private float _chargeCd = 5f;
    private float _summonCd = 14f;
    private float _pointCd = 3f;
    private float _pulseCd = 8f;

    // Charge state (domain movement; presentation collision determines path damage)
    private bool _charging;
    private float _chargeTimer;
    private float _chargeDirX;
    private float _chargeDirY;
    private float _chargeSpeed;

    public GuardianBoss()
        : base("Collapse Guardian", hull: 500, shield: 180, armor: 25, firepower: 30f)
    {
    }

    /// <summary>Current phase (real-time by hull ratio)。</summary>
    public BossPhase Phase => _phase;

    /// <summary>Is in phase charge (presentation uses this for path collision damage and visuals)。</summary>
    public bool IsCharging => _charging;

    /// <summary>Hull ratio (0-1)。</summary>
    public float HullPct => MaxHull > 0 ? (float)Hull / MaxHull : 0f;

    /// <summary>Phase switch event (presentation shows popup/color change; unit test verifies threshold)。</summary>
    public event Action<BossPhase>? PhaseChanged;

    /// <summary>Evaluate phase and trigger switch event (idempotent per call; fires once when crossing threshold)。</summary>
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
            // Phase charge: high-speed straight dash along locked direction; path damage duration determined by presentation via IsCharging
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
    /// Skill scheduler (called every frame; returns at most one intent)：
    /// Priority: pulse > charge > spawn > burst; cooldowns scaled by phase rate (P2 x1.2, P3 x1.6)。
    /// Pure domain logic; intents executed by presentation。
    /// </summary>
    public BossIntent TickSkills(float dt, float playerX, float playerY)
    {
        UpdatePhase();
        float freqMult = _phase switch
        {
            BossPhase.Phase2 => 1.2f, // LD：skillrate +20%
            BossPhase.Phase3 => 1.6f, // LD: enrage (charge rate up, pulse doubled)
            _ => 1f
        };

        _chargeCd -= dt * freqMult;
        _summonCd -= dt * freqMult;
        _pointCd -= dt * freqMult;
        _pulseCd -= dt * freqMult;

        // From phase 2: annihilation pulse (doubled in P3)
        if (_phase >= BossPhase.Phase2 && _pulseCd <= 0f)
        {
            int bursts = _phase == BossPhase.Phase3 ? 2 : 1;
            _pulseCd = 8f;
            return new BossIntent(BossSkillKind.AnnihilationPulse, bursts);
        }

        // Phase charge (P3 shorter cooldown -> faster rate)
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
            _chargeSpeed = _phase == BossPhase.Phase3 ? 640f : 480f; // P3 faster charge
            _chargeCd = _phase == BossPhase.Phase3 ? 3f : 5f;
            return new BossIntent(BossSkillKind.PhaseCharge, 0);
        }

        // Spawn (P1/P2 recon x2, P3 assault ships; presentation picks type by phase)
        if (_summonCd <= 0f)
        {
            _summonCd = _phase == BossPhase.Phase3 ? 12f : 14f;
            return new BossIntent(BossSkillKind.SummonScouts, 2);
        }

        // Burst of 3-projectile barrage
        if (_pointCd <= 0f)
        {
            _pointCd = 3f;
            return new BossIntent(BossSkillKind.PointFire, 3);
        }

        return BossIntent.None;
    }
}
