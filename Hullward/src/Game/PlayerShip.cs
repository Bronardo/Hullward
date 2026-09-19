using System;
using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// Player ship (presentation): WASD/arrow movement, main gun auto-targets and fires (domain TargetingSystem)。
/// Hull stats from domain ShipBase (ScoutShip): hull/shield/firepower affected by fitted modules。
/// Skill slot (ActiveSkill integrated in iteration 4)。
/// Sprint 4 line A: visuals replaced from placeholder blocks with CC0 pixel ships (Kenney Space Shooter Redux; texture by ship class)。
/// </summary>
public partial class PlayerShip : CharacterBody2D
{
    [Export] public float MoveSpeed = 260f;
    [Export] public float FireInterval = 0.25f;
    [Export] public float ProjectileSpeed = 520f;
    [Export] public float WorldHalfWidth = 960f;
    [Export] public float WorldHalfHeight = 540f;

    /// <summary>Player hull (domain polymorphism: Scout/Assault/Battleship/Fortress; injected by Main per dock selection)。</summary>
    public ShipBase ShipStats { get; private set; } = new ScoutShip();

    /// <summary>New hull injected on dock switch (same reference across mothership/sortie; fit and hull share immediately)。</summary>
    public void SetShip(ShipBase ship) => ShipStats = ship;

    /// <summary>Manual skills (decision): Q overload cannon / E shield boost。</summary>
    public OverdriveCannon SkillQ { get; } = new();
    public ShieldBurst SkillE { get; } = new();

    private readonly TargetingSystem _targeting = new();
    private readonly List<ITargetable> _targets = new();
    private readonly Random _rng = new();
    private float _fireCooldown;
    private float _hitFlashTimer;

    /// <summary>Current hull (domain data)。</summary>
    public int Hull => ShipStats.Hull;

    /// <summary>Ship destroyed (mission fail determination; Main takes over settlement)。</summary>
    public event Action? Died;

    /// <summary>Main gun fired (sfx: shoot)。</summary>
    public event Action? Fired;

    /// <summary>Player hit (sfx: hit)。</summary>
    public event Action? Damaged;

    public override void _Ready()
    {
        AddChild(MakeCamera());
        AddChild(MakeShipVisual(ShipStats));
        GD.Print($"PlayerShip ready - speed {MoveSpeed}, dmg {ShipStats.Firepower}, hull {ShipStats.Hull}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Clear destroyed/released targets to avoid accessing disposed objects
        _targets.RemoveAll(t => t is GodotObject go && !GodotObject.IsInstanceValid(go));

        // Arrow keys/WASD movement (MVP reads keys directly; avoids input map config risk)
        Vector2 input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y += 1f;
        Velocity = input.Normalized() * MoveSpeed;
        MoveAndSlide();

        // World boundary (dev pixel prototype: clamp to world rect)
        Position = new Vector2(
            Mathf.Clamp(Position.X, -WorldHalfWidth, WorldHalfWidth),
            Mathf.Clamp(Position.Y, -WorldHalfHeight, WorldHalfHeight));

        _fireCooldown -= (float)delta;

        // Main gun auto-target fire
        ITargetable? target = _targeting.Acquire(_targets, Position.X, Position.Y);
        if (target != null && _fireCooldown <= 0f)
        {
            FireAt(target);
            _fireCooldown = FireInterval / Math.Max(0.2f, ShipStats.FireRateMultiplier); // "Rapid Loading" affix
        }

        // Flash red on hit recovery
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= (float)delta;
            if (_hitFlashTimer <= 0f)
            {
                Modulate = Colors.White;
            }
        }

        // Energy regen (LD Sprint 4 §3.2: combat 8/s, out of combat 12/s; considered in combat if any live target in target list)
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

        // Tick down on-hit damage reduction window ("Damage Reduction" affix: 2s after being hit)
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

    /// <summary>Enemy attack entry: subtract hull (shield absorbs first) + flash red; "Thorns Plating" reflects melee damage; respawn at zero。</summary>
    public void TakeDamage(int damage, ITargetable? attacker = null)
    {
        // "Thorns Plating" affix: reflect melee damage % back to attacker
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

    /// <summary>Injected by Main with targetable enemies in current sector。</summary>
    public void SetTargets(IEnumerable<ITargetable> targets)
    {
        _targets.Clear();
        _targets.AddRange(targets);
    }

    private void FireAt(ITargetable target)
    {
        // "Critical Hit / Critical Amp" affixes: fire rolls crit chance; crit damage = firepower x crit multiplier
        int damage = CombatCalculator.RollAttackDamage(ShipStats, _rng, out bool isCritical);
        var projectile = new Projectile
        {
            Position = Position,
            Target = target,
            Speed = ProjectileSpeed,
            Damage = damage,
            IsCritical = isCritical, // Presentation flag (crit projectile bigger/brighter)
            ProcessMode = ProcessModeEnum.Pausable, // Projectile freezes when combat paused
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
    /// Player ship visuals (Sprint 4 line A): CC0 pixel ship texture replaces placeholder block by ship class。
    /// Canvas is tight hull (99-112px wide); scaled by tier to control display size：
    /// Scout 33x25 / Assault 39x26 / Battleship 44x34 / Fortress 54x41 (larger tiers show bigger size difference)。
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
