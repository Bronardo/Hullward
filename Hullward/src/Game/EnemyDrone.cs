using System;
using Godot;
using Hullward.Domain.Combat;

namespace Hullward.Game;

/// <summary>
/// 暗骸侦察机（表现层占位）：实现 ITargetable 供自动索敌与受击。
/// 简单巡游移动；AI 状态机在 Day 3 迭代接入 EnemyShip 域层次。
/// </summary>
public partial class EnemyDrone : CharacterBody2D, ITargetable
{
    [Export] public float PatrolSpeed = 70f;
    [Export] public int MaxHull = 30;
    [Export] public float WorldHalfWidth = 960f;
    [Export] public float WorldHalfHeight = 540f;

    private Vector2 _patrolDir = Vector2.Right;

    public float X => Position.X;
    public float Y => Position.Y;
    public int Hull { get; private set; }

    public override void _Ready()
    {
        Hull = MaxHull;
        // 简单像素绘制：方块 + 边框色（深空暗骸蓝）
        var body = new ColorRect
        {
            Size = new Vector2(24, 24),
            Color = new Color("3ec6ff"),
            Position = new Vector2(-12, -12)
        };
        AddChild(body);
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = _patrolDir * PatrolSpeed;
        MoveAndSlide();

        // 碰世界边界折返
        if (Position.X > WorldHalfWidth - 20 || Position.X < -WorldHalfWidth + 20)
        {
            _patrolDir = new Vector2(-_patrolDir.X, _patrolDir.Y);
        }
        if (Position.Y > WorldHalfHeight - 20 || Position.Y < -WorldHalfHeight + 20)
        {
            _patrolDir = new Vector2(_patrolDir.X, -_patrolDir.Y);
        }
    }

    public void TakeHit(int damage)
    {
        Hull = Math.Max(0, Hull - damage);
        GD.Print($"EnemyDrone hit -{damage}, hull {Hull}");
        if (Hull <= 0)
        {
            QueueFree();
        }
    }
}
