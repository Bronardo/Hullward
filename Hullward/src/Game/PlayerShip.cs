using System;
using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// 玩家ship（presentation）：WASD/direction键移动，主炮auto索敌开火（域层 TargetingSystem）。
/// hullattribute来自域层 ShipBase（ScoutShip）：hull/shield/firepower受modulefitimpact。
/// skill位（iteration 4 接入 ActiveSkill）。
/// Sprint 4 线 A：视觉由方块占位替换为 CC0 像素船（Kenney Space Shooter Redux，按船型选纹理）。
/// </summary>
public partial class PlayerShip : CharacterBody2D
{
    [Export] public float MoveSpeed = 260f;
    [Export] public float FireInterval = 0.25f;
    [Export] public float ProjectileSpeed = 520f;
    [Export] public float WorldHalfWidth = 960f;
    [Export] public float WorldHalfHeight = 540f;

    /// <summary>玩家hull（域层polymorphism：轻巡/assault ship/battleship/fortress，由 Main 按dockselect注入）。</summary>
    public ShipBase ShipStats { get; private set; } = new ScoutShip();

    /// <summary>dockswitch后注入新hull（同一引用贯穿mothership/出战，fit与hull即时共享）。</summary>
    public void SetShip(ShipBase ship) => ShipStats = ship;

    /// <summary>manualskill（拍板项）：Q 过载炮 / E shield boost。</summary>
    public OverdriveCannon SkillQ { get; } = new();
    public ShieldBurst SkillE { get; } = new();

    private readonly TargetingSystem _targeting = new();
    private readonly List<ITargetable> _targets = new();
    private readonly Random _rng = new();
    private float _fireCooldown;
    private float _hitFlashTimer;

    /// <summary>currenthull（域层data）。</summary>
    public int Hull => ShipStats.Hull;

    /// <summary>ship被击毁（missionfail判定，由 Main 接管结算）。</summary>
    public event Action? Died;

    /// <summary>主炮开火（sfx：射击rotate）。</summary>
    public event Action? Fired;

    /// <summary>玩家受击（sfx：受击）。</summary>
    public event Action? Damaged;

    public override void _Ready()
    {
        AddChild(MakeCamera());
        AddChild(MakeShipVisual(ShipStats));
        GD.Print($"PlayerShip ready - speed {MoveSpeed}, dmg {ShipStats.Firepower}, hull {ShipStats.Hull}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // 清理已击毁/释放的target，avoidance访问 disposed object
        _targets.RemoveAll(t => t is GodotObject go && !GodotObject.IsInstanceValid(go));

        // direction键/WASD 移动（MVP 直接read键，avoidance input map configrisk）
        Vector2 input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y += 1f;
        Velocity = input.Normalized() * MoveSpeed;
        MoveAndSlide();

        // 世界boundary（开发期像素prototype：Clamp 到世界矩形）
        Position = new Vector2(
            Mathf.Clamp(Position.X, -WorldHalfWidth, WorldHalfWidth),
            Mathf.Clamp(Position.Y, -WorldHalfHeight, WorldHalfHeight));

        _fireCooldown -= (float)delta;

        // 主炮auto索敌开火
        ITargetable? target = _targeting.Acquire(_targets, Position.X, Position.Y);
        if (target != null && _fireCooldown <= 0f)
        {
            FireAt(target);
            _fireCooldown = FireInterval / Math.Max(0.2f, ShipStats.FireRateMultiplier); // affix"急速供弹"
        }

        // 受击闪红recovery
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= (float)delta;
            if (_hitFlashTimer <= 0f)
            {
                Modulate = Colors.White;
            }
        }

        // energy regen（LD Sprint4 §3.2：combatmedium 8/s、脱战 12/s；索敌list有alivetarget即combat）
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

        // skillcooldown
        SkillQ.Tick((float)delta);
        SkillE.Tick((float)delta);

        // 受击damage reductionwindow递减（affix"受击damage reduction"：受击后 2s）
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

    /// <summary>enemy ship攻击入口：扣hull（shield先吸收）+ 闪红反馈；thorns镀层反弹近身damage；归零重生。</summary>
    public void TakeDamage(int damage, ITargetable? attacker = null)
    {
        // affix"thorns镀层"：反弹近身damage % 给攻击者
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

    /// <summary>由 Main 注入current星域内的可索敌target。</summary>
    public void SetTargets(IEnumerable<ITargetable> targets)
    {
        _targets.Clear();
        _targets.AddRange(targets);
    }

    private void FireAt(ITargetable target)
    {
        // affix"fatal一击/crit增幅"：开火按crit chance判定，crit damage = firepower × 暴伤倍率
        int damage = CombatCalculator.RollAttackDamage(ShipStats, _rng, out bool isCritical);
        var projectile = new Projectile
        {
            Position = Position,
            Target = target,
            Speed = ProjectileSpeed,
            Damage = damage,
            IsCritical = isCritical, // presentation标注（crit弹丸更大/更亮）
            ProcessMode = ProcessModeEnum.Pausable, // combatpause时弹丸冻结
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
    /// 玩家ship视觉（Sprint 4 线 A）：CC0 像素船纹理按船型替换方块占位。
    /// canvas为紧凑hull（99-112px 宽），按档位缩放控制show尺寸：
    /// 轻巡 33×25 / 突击 39×26 / 战column 44×34 / 要塞 54×41（档位越大体积差越明显）。
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
