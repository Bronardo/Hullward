using System;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Enemies;

namespace Hullward.Game;

/// <summary>
/// 敌舰表现层节点（渲染桥接）：
/// 持有域层 EnemyShip 对象（多态行为在域层），每帧调用 UpdateBehavior 并同步位置。
/// 视觉/受击反馈留在表现层；逻辑全部走域层（ULO2/ULO3 证据）。
/// </summary>
public partial class EnemyDrone : Node2D, ITargetable
{
    public EnemyShip Ship { get; private set; } = null!;
    public PlayerShip? Player { get; set; }
    public event Action<EnemyDrone>? Destroyed;

    private Color _baseColor;
    private float _flashTimer;
    private float _attackCooldown;
    private ColorRect _visual = null!;

    private const float AttackRange = 45f;
    private const float AttackInterval = 0.8f;

    public float X => Ship.X;
    public float Y => Ship.Y;
    public int Hull => Ship.Hull;

    /// <summary>注入域层敌舰 + 视觉配色（按船型由 Main 决定尺寸）。</summary>
    public void Setup(EnemyShip ship, Color color, Vector2 visualSize)
    {
        Ship = ship;
        _baseColor = color;

        // 域对象是行为真源：用节点出生点初始化域位置，避免首帧被覆盖回原点
        ship.X = Position.X;
        ship.Y = Position.Y;

        _visual = new ColorRect
        {
            Size = visualSize,
            Color = color,
            Position = -visualSize / 2f
        };
        AddChild(_visual);
    }

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
                _visual.Color = _baseColor;
            }
        }

        // 近身攻击玩家（按域层火力）
        _attackCooldown -= (float)delta;
        if (Player != null && _attackCooldown <= 0f)
        {
            float dx = Player.Position.X - Position.X;
            float dy = Player.Position.Y - Position.Y;
            if (dx * dx + dy * dy < AttackRange * AttackRange)
            {
                Player.TakeDamage(Math.Max(1, (int)Ship.Firepower), this); // 反伤镀层经 TakeDamage 反弹
                _attackCooldown = AttackInterval;
            }
        }

        // 远程射击（LD Sprint 3 §4.2：炮艇类敌舰多态 TryFire）
        if (Player != null && Ship.TryFire((float)delta, Player.Position.X, Player.Position.Y, out float tx, out float ty))
        {
            SpawnEnemyProjectile(tx, ty);
        }

        if (Ship.IsDestroyed)
        {
            Destroyed?.Invoke(this);
            QueueFree();
        }
    }

    private void SpawnEnemyProjectile(float targetX, float targetY)
    {
        var proj = new EnemyProjectile
        {
            Position = Position,
            Player = Player,
            Attacker = this,
            Direction = (new Vector2(targetX, targetY) - Position).Normalized(),
            Damage = Math.Max(1, (int)Ship.Firepower)
        };
        GetTree().CurrentScene.AddChild(proj);
    }

    public void TakeHit(int damage)
    {
        Ship.TakeHit(damage);
        GD.Print($"{Ship.Name} hit -{damage}, hull {Ship.Hull}");
        // 受击反馈：闪白
        _visual.Color = new Color("ffffff");
        _flashTimer = 0.1f;
    }
}
