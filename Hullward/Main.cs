using System;
using System.Collections.Generic;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Enemies;
using Hullward.Domain.Loot;
using Hullward.Game;

namespace Hullward;

/// <summary>
/// Hullward 入口节点：搭建可玩世界（像素星空 + 玩家舰船 + 混合敌舰 + 掉落循环）。
/// 域层逻辑（星域/索敌/战斗/掉落）全部由 src/Domain 提供，本节点只做桥接。
/// </summary>
public partial class Main : Node
{
    [Export] public int ReconCount = 3;
    [Export] public int RaiderCount = 2;
    [Export] public int FortressCount = 1;
    [Export] public int ZoneLevel = 1; // MVP：安全星域

    private readonly LootTable _loot = new();
    private readonly Random _rng = new();
    private readonly List<ITargetable> _targets = new();
    private int _modulesPicked;
    private int _alloyPicked;

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

        // 敌人容器 + 混合敌舰（多态）
        var enemies = new Node2D { Name = "Enemies" };
        AddChild(enemies);

        SpawnWave(enemies, player);
        GD.Print($"World ready: 1 player ship, {_targets.Count} enemies, zone {ZoneLevel}");
    }

    private void SpawnWave(Node2D container, PlayerShip player)
    {
        for (int i = 0; i < ReconCount; i++)
        {
            SpawnEnemy(new ReconDrone(), new Color("3ec6ff"), new Vector2(24, 24), container, player);
        }
        for (int i = 0; i < RaiderCount; i++)
        {
            SpawnEnemy(new RaiderShip(), new Color("ff6b4a"), new Vector2(34, 18), container, player);
        }
        for (int i = 0; i < FortressCount; i++)
        {
            SpawnEnemy(new HeavyFortress(), new Color("b74aff"), new Vector2(42, 42), container, player);
        }
    }

    private void SpawnEnemy(
        EnemyShip ship, Color color, Vector2 size, Node2D container, PlayerShip player)
    {
        var drone = new EnemyDrone
        {
            Player = player,
            Position = RandomSpawnPosition()
        };
        drone.Setup(ship, color, size);

        // 击毁：清理索敌列表 + 掉落
        drone.Destroyed += d =>
        {
            _targets.Remove(d);
            DropLoot(d.Position, player);
        };

        container.AddChild(drone);
        _targets.Add(drone);
    }

    private Vector2 RandomSpawnPosition()
    {
        float x = _rng.NextSingle() * 1400f - 700f;
        float y = _rng.NextSingle() * 800f - 400f;
        return new Vector2(x, y);
    }

    private void DropLoot(Vector2 worldPosition, PlayerShip player)
    {
        // 模块掉落（品质色） + 合金掉落
        ModuleDrop? drop = _loot.RollModule(ZoneLevel, _rng);
        if (drop != null)
        {
            SpawnPickup(Pickup.CreateModule(drop.Name, RarityColor(drop.Rarity)), worldPosition, player);
        }
        int alloy = _loot.RollAlloy(ZoneLevel, _rng);
        SpawnPickup(Pickup.CreateAlloy(alloy), worldPosition, player);
    }

    private void SpawnPickup(Pickup pickup, Vector2 worldPosition, PlayerShip player)
    {
        pickup.Player = player;
        pickup.Position = worldPosition + new Vector2(_rng.NextSingle() * 30f - 15f, _rng.NextSingle() * 30f - 15f);
        pickup.Collected += OnPickupCollected;
        AddChild(pickup);
    }

    private void OnPickupCollected(Pickup pickup)
    {
        if (pickup.Kind == Pickup.PickupKind.Module)
        {
            _modulesPicked++;
        }
        else
        {
            _alloyPicked += ExtractAlloyAmount(pickup.Label);
        }
        GD.Print($"库存: 模块 {_modulesPicked} 件, 合金 {_alloyPicked}");
    }

    private static int ExtractAlloyAmount(string label)
    {
        // "合金×N"
        int idx = label.IndexOf('×');
        return idx >= 0 && int.TryParse(label[(idx + 1)..], out int n) ? n : 0;
    }

    private static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => new Color("c8c8c8"),  // 白
        ItemRarity.Magic => new Color("4da6ff"),    // 蓝
        ItemRarity.Rare => new Color("ffd166"),     // 黄
        ItemRarity.Set => new Color("6ee06e"),      // 绿
        ItemRarity.Ancient => new Color("ff7ad9"),  // 太古
        _ => new Color("ffffff")
    };

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
