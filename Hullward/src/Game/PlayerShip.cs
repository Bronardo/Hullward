using System;
using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// 玩家舰船（表现层）：WASD/方向键移动，主炮自动索敌开火（域层 TargetingSystem）。
/// 船体属性来自域层 ShipBase（ScoutShip）：耐久/护盾/火力受模块装配影响。
/// 技能位（迭代 4 接入 ActiveSkill）。
/// Sprint 4 线 A：视觉由方块占位替换为 CC0 像素船（Kenney Space Shooter Redux，按船型选纹理）。
/// </summary>
public partial class PlayerShip : CharacterBody2D
{
    [Export] public float MoveSpeed = 260f;
    [Export] public float FireInterval = 0.25f;
    [Export] public float ProjectileSpeed = 520f;
    [Export] public float WorldHalfWidth = 960f;
    [Export] public float WorldHalfHeight = 540f;

    /// <summary>玩家船体（域层多态：轻巡/突击舰/战列舰/要塞舰，由 Main 按船坞选择注入）。</summary>
    public ShipBase ShipStats { get; private set; } = new ScoutShip();

    /// <summary>船坞切换后注入新船体（同一引用贯穿母舰/出战，装配与耐久即时共享）。</summary>
    public void SetShip(ShipBase ship) => ShipStats = ship;

    /// <summary>手动技能（拍板项）：Q 过载炮 / E 护盾充能。</summary>
    public OverdriveCannon SkillQ { get; } = new();
    public ShieldBurst SkillE { get; } = new();

    private readonly TargetingSystem _targeting = new();
    private readonly List<ITargetable> _targets = new();
    private readonly Random _rng = new();
    private float _fireCooldown;
    private float _hitFlashTimer;

    /// <summary>当前船体耐久（域层数据）。</summary>
    public int Hull => ShipStats.Hull;

    /// <summary>舰船被击毁（任务失败判定，由 Main 接管结算）。</summary>
    public event Action? Died;

    /// <summary>主炮开火（音效：射击轮换）。</summary>
    public event Action? Fired;

    /// <summary>玩家受击（音效：受击）。</summary>
    public event Action? Damaged;

    public override void _Ready()
    {
        AddChild(MakeCamera());
        AddChild(MakeShipVisual(ShipStats));
        GD.Print($"PlayerShip ready - speed {MoveSpeed}, dmg {ShipStats.Firepower}, hull {ShipStats.Hull}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // 清理已击毁/释放的目标，避免访问 disposed 对象
        _targets.RemoveAll(t => t is GodotObject go && !GodotObject.IsInstanceValid(go));

        // 方向键/WASD 移动（MVP 直接读键，避免 input map 配置风险）
        Vector2 input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y += 1f;
        Velocity = input.Normalized() * MoveSpeed;
        MoveAndSlide();

        // 世界边界（开发期像素原型：Clamp 到世界矩形）
        Position = new Vector2(
            Mathf.Clamp(Position.X, -WorldHalfWidth, WorldHalfWidth),
            Mathf.Clamp(Position.Y, -WorldHalfHeight, WorldHalfHeight));

        _fireCooldown -= (float)delta;

        // 主炮自动索敌开火
        ITargetable? target = _targeting.Acquire(_targets, Position.X, Position.Y);
        if (target != null && _fireCooldown <= 0f)
        {
            FireAt(target);
            _fireCooldown = FireInterval / Math.Max(0.2f, ShipStats.FireRateMultiplier); // 词缀"急速供弹"
        }

        // 受击闪红恢复
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= (float)delta;
            if (_hitFlashTimer <= 0f)
            {
                Modulate = Colors.White;
            }
        }

        // 能量回复（LD Sprint4 §3.2：战斗中 8/s、脱战 12/s；索敌列表有存活目标即战斗）
        bool inCombat = false;
        foreach (var t in _targets)
        {
            if (t.Hull > 0)
            {
                inCombat = true;
                break;
            }
        }
        ShipStats.RegenEnergy((float)delta, inCombat);

        // 技能冷却
        SkillQ.Tick((float)delta);
        SkillE.Tick((float)delta);

        // 受击减伤窗口递减（词缀"受击减伤"：受击后 2s）
        ShipStats.TickTimers((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            var context = new PlayerContext
            {
                Ship = ShipStats,
                LockedTarget = _targeting.Acquire(_targets, Position.X, Position.Y)
            };

            if (key.Keycode == Key.Q && SkillQ.TryUse(context))
            {
                GD.Print($"Skill Q Overload Cannon: {ShipStats.Firepower * OverdriveCannon.DamageMultiplier:0} dmg");
            }
            else if (key.Keycode == Key.E && SkillE.TryUse(context))
            {
                GD.Print($"Skill E Shield Boost: {ShipStats.Shield}/{ShipStats.MaxShield}");
            }
        }
    }

    /// <summary>敌舰攻击入口：扣耐久（护盾先吸收）+ 闪红反馈；反伤镀层反弹近身伤害；归零重生。</summary>
    public void TakeDamage(int damage, ITargetable? attacker = null)
    {
        // 词缀"反伤镀层"：反弹近身伤害 % 给攻击者
        if (attacker != null && ShipStats.ThornsPct > 0f)
        {
            int thorns = Math.Max(1, (int)(damage * ShipStats.ThornsPct));
            attacker.TakeHit(thorns);
            GD.Print($"PlayerShip thorns rebound {thorns} to attacker");
        }

        ShipStats.TakeHit(damage);
        Modulate = new Color("ff6b6b");
        _hitFlashTimer = 0.12f;
        Damaged?.Invoke();
        GD.Print($"PlayerShip hit -{damage}, hull {ShipStats.Hull}, shield {ShipStats.Shield}");
        if (ShipStats.IsDestroyed)
        {
            ShipStats.ResetCombatState();
            Died?.Invoke();
            GD.Print("PlayerShip destroyed - mission failed");
        }
    }

    /// <summary>由 Main 注入当前星域内的可索敌目标。</summary>
    public void SetTargets(IEnumerable<ITargetable> targets)
    {
        _targets.Clear();
        _targets.AddRange(targets);
    }

    private void FireAt(ITargetable target)
    {
        // 词缀"致命一击/暴击增幅"：开火按暴击率判定，暴击伤害 = 火力 × 暴伤倍率
        int damage = CombatCalculator.RollAttackDamage(ShipStats, _rng, out bool isCritical);
        var projectile = new Projectile
        {
            Position = Position,
            Target = target,
            Speed = ProjectileSpeed,
            Damage = damage,
            IsCritical = isCritical, // 表现层标注（暴击弹丸更大/更亮）
            ProcessMode = ProcessModeEnum.Pausable, // 战斗暂停时弹丸冻结
        };
        GetTree().CurrentScene.AddChild(projectile);
        Fired?.Invoke();
    }

    private static Camera2D MakeCamera()
    {
        var camera = new Camera2D
        {
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 6f,
            Zoom = new Vector2(1f, 1f)
        };
        return camera;
    }

    /// <summary>
    /// 玩家舰船视觉（Sprint 4 线 A）：CC0 像素船纹理按船型替换方块占位。
    /// 画布为紧凑船体（99-112px 宽），按档位缩放控制显示尺寸：
    /// 轻巡 33×25 / 突击 39×26 / 战列 44×34 / 要塞 54×41（档位越大体积差越明显）。
    /// </summary>
    private static Node2D MakeShipVisual(ShipBase ship)
    {
        var (path, scale) = ship switch
        {
            AssaultShip => ("res://assets/ships/player_assault.png", 0.35f),
            Battleship => ("res://assets/ships/player_battleship.png", 0.45f),
            FortressShip => ("res://assets/ships/player_fortress.png", 0.55f),
            _ => ("res://assets/ships/player_scout.png", 0.33f)
        };
        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(path),
            Scale = new Vector2(scale, scale),
            Centered = true
        };
        var node = new Node2D();
        node.AddChild(sprite);
        return node;
    }
}
