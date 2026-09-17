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
    private ColorRect _visual = null!;

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

        if (Ship.IsDestroyed)
        {
            Destroyed?.Invoke(this);
            QueueFree();
        }
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
