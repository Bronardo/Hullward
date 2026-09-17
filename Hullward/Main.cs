using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Game;

namespace Hullward;

/// <summary>
/// Hullward 入口节点：搭建最小可玩世界（像素星空 + 玩家舰船 + 暗骸侦察机 + 相机）。
/// 域层逻辑（星域生成/索敌）已由 src/Domain 提供并被表现层复用。
/// </summary>
public partial class Main : Node
{
    [Export] public int EnemyCount = 5;

    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");

        // 像素星空背景（深空底色 + 随机星点）
        var bg = new ColorRect
        {
            Color = new Color("0b1c2c"),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500)
        };
        AddChild(bg);
        SpawnStars();

        // 玩家舰船（自动索敌开火）
        var player = new PlayerShip { Position = Vector2.Zero };
        AddChild(player);

        // 敌人容器 + 注入索敌目标
        var enemies = new Node2D { Name = "Enemies" };
        AddChild(enemies);

        var targets = new List<ITargetable>();
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = 0; i < EnemyCount; i++)
        {
            var drone = new EnemyDrone
            {
                Position = new Vector2(rng.RandfRange(-700, 700), rng.RandfRange(-400, 400))
            };
            enemies.AddChild(drone);
            targets.Add(drone);
        }

        player.SetTargets(targets);
        GD.Print($"World ready: 1 player ship, {targets.Count} enemy drones");
    }

    private void SpawnStars()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = 0; i < 120; i++)
        {
            var star = new ColorRect
            {
                Color = new Color(0.6f, 0.85f, 1f, 0.7f),
                Size = new Vector2(2, 2),
                Position = new Vector2(rng.RandfRange(-960, 960), rng.RandfRange(-540, 540))
            };
            AddChild(star);
        }
    }
}
