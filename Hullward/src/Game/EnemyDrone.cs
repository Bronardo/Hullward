using System;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Enemies;
using Hullward.Domain.WorldGen;

namespace Hullward.Game;

/// <summary>
/// 敌舰表现层节点（渲染桥接）：
/// 持有域层 EnemyShip 对象（多态行为在域层），每帧调用 UpdateBehavior 并同步位置。
/// Boss（GuardianBoss）额外执行域层技能意图（召唤/点射/湮灭脉冲/冲锋碰撞），
/// 视觉/受击反馈留在表现层；逻辑全部走域层（ULO2/ULO3 证据）。
/// Sprint 4 线 A：视觉由方块占位替换为 CC0 像素船（Kenney Space Shooter Redux，按船型选纹理）。
/// </summary>
public partial class EnemyDrone : Node2D, ITargetable
{
    public EnemyShip Ship { get; private set; } = null!;
    public PlayerShip? Player { get; set; }
    public event Action<EnemyDrone>? Destroyed;

    /// <summary>召唤请求（Main 注入）：Boss 召唤侦察机/突击舰。</summary>
    public Action<EnemyKind, Vector2>? SummonRequested { get; set; }

    /// <summary>HUD 提示请求（Main 订阅，转发 _hud.ShowToast）。</summary>
    public event Action<string>? ToastRequested;

    /// <summary>受击音效请求（Main 订阅）。</summary>
    public event Action? HitTaken;

    /// <summary>Boss 阶段切换音效请求（Main 订阅，阶段 2/3 警示）。</summary>
    public event Action? BossWarnRequested;

    private float _flashTimer;
    private float _attackCooldown;
    private Sprite2D _visual = null!;

    // Boss 点射 3 连（salvo 状态机：每 0.16s 一发）
    private int _salvoLeft;
    private float _salvoTimer;
    private Vector2 _salvoDir;

    private const float AttackRange = 45f;
    private const float AttackInterval = 0.8f;
    private const float ChargeHitRange = 62f;  // 冲锋路径伤害判定半径
    private const float ChargeHitInterval = 0.2f;

    public float X => Ship.X;
    public float Y => Ship.Y;
    public int Hull => Ship.Hull;

    /// <summary>注入域层敌舰 + 视觉（船型决定纹理，visualSize 决定显示尺寸）。</summary>
    public void Setup(EnemyShip ship, Color color, Vector2 visualSize)
    {
        Ship = ship;

        // 域对象是行为真源：用节点出生点初始化域位置，避免首帧被覆盖回原点
        ship.X = Position.X;
        ship.Y = Position.Y;

        var (path, canvasW) = TextureFor(ship);
        float scale = canvasW > 0f ? visualSize.X / canvasW : 1f;
        // 纹理本色显示（CC0 红紫系敌船），Main 传入的方块占位色不再参与渲染
        _ = color;
        _visual = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(path),
            Scale = new Vector2(scale, scale),
            SelfModulate = Colors.White,
            Centered = true
        };
        AddChild(_visual);

        // Boss：阶段切换提示
        if (ship is GuardianBoss boss)
        {
            boss.PhaseChanged += phase =>
            {
                ToastRequested?.Invoke(phase switch
                {
                    BossPhase.Phase2 => "禁区守卫进入阶段 2：湮灭脉冲！",
                    BossPhase.Phase3 => "禁区守卫狂暴：湮灭脉冲双发，召唤突击舰！",
                    _ => "禁区守卫：三阶段技能启动"
                });
                if (phase != BossPhase.Phase1)
                {
                    BossWarnRequested?.Invoke();
                }
            };
        }
    }

    /// <summary>船型 → 精灵纹理与画布宽（等比缩放基准）。</summary>
    private static (string Path, float CanvasWidth) TextureFor(EnemyShip ship) => ship switch
    {
        RaiderShip => ("res://assets/ships/enemy_raider.png", 104f),
        HeavyFortress => ("res://assets/ships/enemy_fortress.png", 82f),
        GunboatShip => ("res://assets/ships/enemy_gunboat.png", 103f),
        EliteGuardShip => ("res://assets/ships/enemy_elite.png", 97f),
        GuardianBoss => ("res://assets/ships/boss.png", 97f),
        SwarmDrone => ("res://assets/ships/enemy_swarm.png", 93f),
        _ => ("res://assets/ships/enemy_recon.png", 93f)
    };

    public override void _PhysicsProcess(double delta)
    {
        if (Player != null)
        {
            Ship.UpdateBehavior((float)delta, Player.Position.X, Player.Position.Y);
            Position = new Vector2(Ship.X, Ship.Y);
        }

        if (_flashTimer > 0f)
        {
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0f)
            {
                _visual.SelfModulate = Colors.White;
            }
        }

        if (Ship is GuardianBoss boss && Player != null)
        {
            ExecuteBossIntent(boss.TickSkills((float)delta, Player.Position.X, Player.Position.Y), boss);
        }

        // 近身攻击玩家（按域层火力）；Boss 冲锋时路径持续伤害（更短间隔、更大范围）
        _attackCooldown -= (float)delta;
        if (Player != null && _attackCooldown <= 0f)
        {
            bool charging = Ship is GuardianBoss { IsCharging: true };
            float range = charging ? ChargeHitRange : AttackRange;
            float dx = Player.Position.X - Position.X;
            float dy = Player.Position.Y - Position.Y;
            if (dx * dx + dy * dy < range * range)
            {
                Player.TakeDamage(Math.Max(1, (int)Ship.Firepower), this); // 反伤镀层经 TakeDamage 反弹
                _attackCooldown = charging ? ChargeHitInterval : AttackInterval;
            }
        }

        // 远程射击（LD Sprint 3 §4.2：炮艇类敌舰多态 TryFire）
        if (Player != null && Ship.TryFire((float)delta, Player.Position.X, Player.Position.Y, out float tx, out float ty))
        {
            SpawnEnemyProjectile(tx, ty);
        }

        // Boss 点射 3 连（salvo）
        if (_salvoLeft > 0)
        {
            _salvoTimer -= (float)delta;
            if (_salvoTimer <= 0f)
            {
                SpawnEnemyProjectileAt(Position + _salvoDir * 30f, _salvoDir);
                _salvoLeft--;
                _salvoTimer = 0.16f;
            }
        }

        if (Ship.IsDestroyed)
        {
            Destroyed?.Invoke(this);
            QueueFree();
        }
    }

    /// <summary>执行 Boss 技能意图（召唤/点射/湮灭脉冲/冲锋视觉）。</summary>
    private void ExecuteBossIntent(BossIntent intent, GuardianBoss boss)
    {
        switch (intent.Kind)
        {
            case BossSkillKind.PhaseCharge:
                // 冲锋视觉：高亮
                _visual.SelfModulate = new Color("ff9aa8");
                _flashTimer = 0.12f;
                break;
            case BossSkillKind.SummonScouts:
                if (SummonRequested != null)
                {
                    // P3 狂暴召唤突击舰，否则召唤侦察机（LD §4.3）
                    EnemyKind kind = boss.Phase == BossPhase.Phase3 ? EnemyKind.Raider : EnemyKind.Recon;
                    for (int i = 0; i < intent.Payload; i++)
                    {
                        SummonRequested(kind, Position + new Vector2((i - 0.5f) * 60f, -40f));
                    }
                    ToastRequested?.Invoke(kind == EnemyKind.Raider ? "禁区守卫召唤突击舰！" : "禁区守卫召唤侦察机！");
                }
                break;
            case BossSkillKind.PointFire:
                // 3 连弹幕：锁定玩家方向，逐发间隔 0.16s
                _salvoLeft = intent.Payload;
                _salvoTimer = 0f;
                _salvoDir = Player != null
                    ? (Player.Position - Position).Normalized()
                    : Vector2.Right;
                break;
            case BossSkillKind.AnnihilationPulse:
                for (int i = 0; i < intent.Payload; i++)
                {
                    var pulse = new BossPulse
                    {
                        Position = Position + new Vector2(i * 24f, 0f),
                        Player = Player,
                        Damage = Math.Max(2, (int)(Ship.Firepower * 1.6f)), // AOE 重击
                        ProcessMode = ProcessModeEnum.Pausable // 战斗暂停时脉冲冻结
                    };
                    GetTree().CurrentScene.AddChild(pulse);
                }
                ToastRequested?.Invoke(intent.Payload > 1 ? "湮灭脉冲双发——闪避！" : "湮灭脉冲——闪避！");
                break;
        }
    }

    private void SpawnEnemyProjectile(float targetX, float targetY)
    {
        var dir = (new Vector2(targetX, targetY) - Position).Normalized();
        SpawnEnemyProjectileAt(Position, dir);
    }

    private void SpawnEnemyProjectileAt(Vector2 from, Vector2 dir)
    {
        var proj = new EnemyProjectile
        {
            Position = from,
            Player = Player,
            Attacker = this,
            Direction = dir,
            Damage = Math.Max(1, (int)Ship.Firepower),
            ProcessMode = ProcessModeEnum.Pausable // 战斗暂停时敌方弹丸冻结
        };
        GetTree().CurrentScene.AddChild(proj);
    }

    public void TakeHit(int damage)
    {
        Ship.TakeHit(damage);
        GD.Print($"{Ship.Name} hit -{damage}, hull {Ship.Hull}");
        // 受击反馈：提亮闪烁（>1 分量让 Sprite2D 超白高亮，方块占位时是闪白）
        _visual.SelfModulate = new Color(2.6f, 2.6f, 2.6f);
        _flashTimer = 0.1f;
        HitTaken?.Invoke();
    }
}
