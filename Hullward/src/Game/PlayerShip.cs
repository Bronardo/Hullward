using System;
using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// 玩家舰船（表现层）：WASD/方向键移动，主炮自动索敌开火（域层 TargetingSystem）。
/// 技能位占位（Day 3 接入 ActiveSkill）。
/// </summary>
public partial class PlayerShip : CharacterBody2D
{
    [Export] public float MoveSpeed = 260f;
    [Export] public float FireInterval = 0.25f;
    [Export] public int CannonDamage = 10;
    [Export] public float ProjectileSpeed = 520f;
    [Export] public float WorldHalfWidth = 960f;
    [Export] public float WorldHalfHeight = 540f;
    [Export] public int MaxHull = 100;

    private readonly TargetingSystem _targeting = new();
    private readonly List<ITargetable> _targets = new();
    private float _fireCooldown;
    private float _hitFlashTimer;

    /// <summary>当前船体耐久（受击扣减，归零重生）。</summary>
    public int Hull { get; private set; }

    public override void _Ready()
    {
        Hull = MaxHull;
        AddChild(MakeCamera());
        AddChild(MakeShipVisual());
        GD.Print($"PlayerShip ready - speed {MoveSpeed}, dmg {CannonDamage}, hull {MaxHull}");
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
            _fireCooldown = FireInterval;
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
    }

    /// <summary>敌舰攻击入口：扣耐久 + 闪红反馈；归零重生。</summary>
    public void TakeDamage(int damage)
    {
        Hull = Math.Max(0, Hull - damage);
        Modulate = new Color("ff6b6b");
        _hitFlashTimer = 0.12f;
        GD.Print($"PlayerShip hit -{damage}, hull {Hull}");
        if (Hull <= 0)
        {
            Hull = MaxHull;
            Position = Vector2.Zero;
            GD.Print("PlayerShip destroyed - respawning at origin");
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
        var projectile = new Projectile
        {
            Position = Position,
            Target = target,
            Speed = ProjectileSpeed,
            Damage = CannonDamage
        };
        GetTree().CurrentScene.AddChild(projectile);
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

    /// <summary>像素风玩家舰船视觉（开发期占位：主舰体 + 核心 + 炮口指示）。</summary>
    private static Node2D MakeShipVisual()
    {
        var ship = new Node2D();

        var hull = new ColorRect
        {
            Size = new Vector2(30, 22),
            Color = new Color("7fd8be"),
            Position = new Vector2(-15, -11)
        };
        ship.AddChild(hull);

        var core = new ColorRect
        {
            Size = new Vector2(10, 10),
            Color = new Color("ffe08a"),
            Position = new Vector2(-5, -5)
        };
        ship.AddChild(core);

        // 炮口方向指示（朝右）
        var barrel = new ColorRect
        {
            Size = new Vector2(10, 4),
            Color = new Color("ffffff"),
            Position = new Vector2(15, -2)
        };
        ship.AddChild(barrel);

        return ship;
    }
}
