using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hullward.Domain.Combat;
using Hullward.Domain.Enemies;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Save;
using Hullward.Domain.Ships;
using Hullward.Game;

namespace Hullward;

/// <summary>
/// Hullward 入口节点：
/// 4 星域推进（清怪跃迁 → 难度/掉落随区提升 → Boss 波次）、
/// 拾取 → 背包 → 自动装配（属性实时生效）、F5 存档 / F9 读档（JSON）。
/// </summary>
public partial class Main : Node
{
    [Export] public int ZoneLevel = 1;
    [Export] public float JumpDelay = 2.5f;

    private readonly LootTable _loot = new();
    private readonly Random _rng = new();
    private readonly List<ITargetable> _targets = new();
    private Inventory _inventory = new();
    private PlayerShip _player = null!;
    private Node2D _enemies = null!;
    private ColorRect _background = null!;
    private HUD _hud = null!;
    private bool _waveActive;
    private float _jumpTimer;
    private bool _victory;
    private int _modulesPicked;

    // 存档（UI 规格 v0.2 §3：文件制 + 命名制；Step 2 接入主菜单命名后替换固定名）
    private SaveService _saveService = null!;
    private string _captainName = "captain";

    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");
        _saveService = new SaveService(ProjectSettings.GlobalizePath("user://saves"));

        // 像素星空背景（按星域变色）
        _background = new ColorRect
        {
            Color = ZoneColor(ZoneLevel),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500)
        };
        AddChild(_background);
        SpawnStars();

        _player = new PlayerShip { Position = Vector2.Zero };
        AddChild(_player);

        _enemies = new Node2D { Name = "Enemies" };
        AddChild(_enemies);

        _hud = new HUD();
        AddChild(_hud);

        SpawnWave();
        GD.Print($"World ready: 1 player ship, {_targets.Count} enemies, zone {ZoneLevel}");
    }

    public override void _Process(double delta)
    {
        if (_hud == null)
        {
            return;
        }

        string qState = _player.SkillQ.IsReady ? "就绪" : $"{_player.SkillQ.Remaining:0.0}s";
        string eState = _player.SkillE.IsReady ? "就绪" : $"{_player.SkillE.Remaining:0.0}s";
        string zoneLabel = _victory ? "已通关" : $"星域 {ZoneLevel}/4";

        _hud.UpdateStatus(
            $"{zoneLabel}  |  耐久 {_player.ShipStats.Hull}  护盾 {_player.ShipStats.Shield}/{_player.ShipStats.MaxShield}" +
            $"  |  火力 {_player.ShipStats.Firepower:0}  |  Q过载炮[{qState}]  E护盾[{eState}]" +
            $"  |  合金 {_inventory.Alloy}  模块 {_modulesPicked}  |  F5存 F9读");
    }

    public override void _PhysicsProcess(double delta)
    {
        // 清怪 → 短暂延迟 → 跃迁下一星域
        if (_waveActive && _targets.Count == 0 && !_victory)
        {
            _jumpTimer += (float)delta;
            if (_jumpTimer >= JumpDelay)
            {
                _jumpTimer = 0f;
                AdvanceZone();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F5)
            {
                SaveGame();
            }
            else if (key.Keycode == Key.F9)
            {
                LoadGame();
            }
        }
    }

    // ---------- 星域推进 ----------

    private void AdvanceZone()
    {
        if (ZoneLevel >= 4)
        {
            _victory = true;
            GD.Print("=== 通关！坍缩禁区已肃清，深空暗骸战役结束 ===");
            return;
        }

        ZoneLevel++;
        _background.Color = ZoneColor(ZoneLevel);
        GD.Print($"=== 跃迁到星域 {ZoneLevel}（难度提升） ===");
        SpawnWave();
    }

    private void SpawnWave()
    {
        ClearEnemies();

        switch (ZoneLevel)
        {
            case 1:
                AddEnemies(() => new ReconDrone(), 3, new Color("3ec6ff"), new Vector2(24, 24));
                AddEnemies(() => new RaiderShip(), 2, new Color("ff6b4a"), new Vector2(34, 18));
                AddEnemies(() => new HeavyFortress(), 1, new Color("b74aff"), new Vector2(42, 42));
                break;
            case 2:
                AddEnemies(() => new ReconDrone(), 4, new Color("3ec6ff"), new Vector2(24, 24));
                AddEnemies(() => new RaiderShip(), 3, new Color("ff6b4a"), new Vector2(34, 18));
                AddEnemies(() => new HeavyFortress(), 2, new Color("b74aff"), new Vector2(42, 42));
                break;
            case 3:
                AddEnemies(() => new ReconDrone(), 5, new Color("3ec6ff"), new Vector2(24, 24));
                AddEnemies(() => new RaiderShip(), 4, new Color("ff6b4a"), new Vector2(34, 18));
                AddEnemies(() => new HeavyFortress(), 2, new Color("b74aff"), new Vector2(42, 42));
                break;
            case 4:
                AddEnemies(() => new GuardianBoss(), 1, new Color("ff3b6b"), new Vector2(64, 64));
                AddEnemies(() => new ReconDrone(), 4, new Color("3ec6ff"), new Vector2(24, 24));
                break;
        }

        _player.SetTargets(_targets);
        _waveActive = true;
        GD.Print($"星域 {ZoneLevel} 波次就绪: {_targets.Count} 敌舰");
    }

    private void AddEnemies(Func<EnemyShip> factory, int count, Color color, Vector2 size)
    {
        for (int i = 0; i < count; i++)
        {
            var ship = factory();
            ship.ScaleForZone(ZoneLevel);
            var drone = new EnemyDrone
            {
                Player = _player,
                Position = RandomSpawnPosition()
            };
            drone.Setup(ship, color, size);
            drone.Destroyed += d =>
            {
                _targets.Remove(d);
                DropLoot(d.Position, _player);
            };
            _enemies.AddChild(drone);
            _targets.Add(drone);
        }
    }

    private void ClearEnemies()
    {
        foreach (var node in _enemies.GetChildren().OfType<EnemyDrone>())
        {
            node.QueueFree();
        }
        _targets.Clear();
    }

    private Vector2 RandomSpawnPosition()
    {
        Vector2 pos;
        do
        {
            pos = new Vector2(
                _rng.NextSingle() * 1000f - 500f,
                _rng.NextSingle() * 600f - 300f);
        } while (pos.Length() < 150f);
        return pos;
    }

    private static Color ZoneColor(int zone) => zone switch
    {
        1 => new Color("0b1c2c"), // 安全：深蓝
        2 => new Color("1a1030"), // 争议：深紫
        3 => new Color("301018"), // 无人深空：暗红
        _ => new Color("0a0a0f")  // 坍缩禁区：黑
    };

    // ---------- 掉落 / 背包 / 装配 ----------

    private void DropLoot(Vector2 worldPosition, PlayerShip player)
    {
        ModuleDrop? drop = _loot.RollModule(ZoneLevel, _rng);
        if (drop != null)
        {
            SpawnPickup(Pickup.CreateModule(drop, RarityColor(drop.Rarity)), worldPosition, player);
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
        if (pickup.Kind == Pickup.PickupKind.Module && pickup.ModuleData != null)
        {
            _modulesPicked++;
            _inventory.AddModule(pickup.ModuleData);
            ShipFitting.AutoEquipBest(_player.ShipStats, _inventory);
            GD.Print($"拾取模块: {pickup.ModuleData.Name} | 装配后火力 {_player.ShipStats.Firepower}, 护盾 {_player.ShipStats.Shield}");
        }
        else
        {
            int alloy = ExtractAlloyAmount(pickup.Label);
            _inventory.AddAlloy(alloy);
            GD.Print($"拾取合金×{alloy} | 合金总量 {_inventory.Alloy}");
        }
    }

    private static int ExtractAlloyAmount(string label)
    {
        int idx = label.IndexOf('×');
        return idx >= 0 && int.TryParse(label[(idx + 1)..], out int n) ? n : 0;
    }

    // ---------- 存档 ----------

    private void SaveGame()
    {
        var data = new SaveData
        {
            ZoneLevel = ZoneLevel,
            Alloy = _inventory.Alloy,
            PlayerHull = _player.ShipStats.Hull,
            ModulesPicked = _modulesPicked
        };
        foreach (var module in _inventory.Modules)
        {
            data.Modules.Add(new ModuleDropData { Slot = module.Slot, Rarity = module.Rarity });
        }
        _saveService.Save(_captainName, data);
        GD.Print($"已存档 -> {_saveService.SavePathFor(_captainName)} (星域 {data.ZoneLevel}, 合金 {data.Alloy}, 背包 {data.Modules.Count})");
    }

    private void LoadGame()
    {
        SaveData? data = _saveService.Load(_captainName);
        if (data == null)
        {
            GD.Print("无存档，按 F5 可创建");
            return;
        }

        ZoneLevel = Math.Clamp(data.ZoneLevel, 1, 4);
        _modulesPicked = data.ModulesPicked;
        _inventory = new Inventory();
        _inventory.AddAlloy(data.Alloy);
        foreach (var module in data.Modules)
        {
            _inventory.AddModule(new ModuleDrop(module.Slot, module.Rarity));
        }

        _player.ShipStats.ResetCombatState();
        _player.ShipStats.Hull = Math.Max(1, data.PlayerHull);
        ShipFitting.AutoEquipBest(_player.ShipStats, _inventory);
        _player.Position = Vector2.Zero;
        _victory = false;
        _background.Color = ZoneColor(ZoneLevel);
        SpawnWave();

        GD.Print($"已读档: 星域 {ZoneLevel}, 合金 {_inventory.Alloy}, 背包 {_inventory.Modules.Count}, 火力 {_player.ShipStats.Firepower}");
    }

    // ---------- 环境 ----------

    private static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => new Color("c8c8c8"),
        ItemRarity.Magic => new Color("4da6ff"),
        ItemRarity.Rare => new Color("ffd166"),
        ItemRarity.Set => new Color("6ee06e"),
        ItemRarity.Ancient => new Color("ff7ad9"),
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
