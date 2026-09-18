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
using Hullward.Domain.WorldGen;
using Hullward.Game;

namespace Hullward;

/// <summary>
/// Hullward 入口节点（UI 规格 v0.2 任务制循环）：
/// 主菜单 → 命名/选档 → 星图（随机任务） → 任务战斗 → 结算 → 星图全重随机。
/// 章节随母舰等级解锁；任务完成获得母舰经验；F5 存档 / F9 读档（文件制+命名制）。
/// </summary>
public partial class Main : Node
{
    private enum GameState { Menu, Naming, Starmap, Mothership, Battle, Settlement }

    [Export] public float JumpDelay = 2.5f;

    /// <summary>当前章节（= 母舰等级，1-4）。</summary>
    public int ZoneLevel => Math.Clamp(_mothershipLevel, 1, 4);

    /// <summary>母舰经验：每 3 点升 1 级。</summary>
    public const int ExpPerLevel = 3;

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
    private int _modulesPicked;

    // UI 流程（主菜单 → 命名/选档 → 星图 → 战斗 → 结算）
    private GameState _state = GameState.Menu;
    private CanvasLayer _uiLayer = null!;
    private string _namingError = "";

    // 存档（文件制 + 命名制：主角名为唯一标识）
    private SaveService _saveService = null!;
    private string _captainName = "captain";

    // 星图 / 任务
    private StarMap _starMap = null!;
    private StarMapNode? _currentTask;
    private int _mothershipLevel = 1;
    private int _mothershipExp;
    private int _pendingHull;
    private int _taskStartAlloy;
    private int _taskStartModules;
    private bool _taskIsBoss;

    // 母舰内部（LD §6：装配槽位 / 配装方案 / 出战船体；船坞 §船坞：四档旗舰切换）
    private ShipBase _mothershipShip = null!;
    private List<ModuleDrop?> _equippedSlots = null!;
    private readonly ShipPresets _presets = new();
    private ShipClass _shipClass = ShipClass.Scout;

    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");
        _saveService = new SaveService(ProjectSettings.GlobalizePath("user://saves"));

        // 像素星空背景（按章节变色）
        _background = new ColorRect
        {
            Color = ZoneColor(ZoneLevel),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500)
        };
        AddChild(_background);
        SpawnStars();

        _uiLayer = new CanvasLayer { Name = "UILayer" };
        AddChild(_uiLayer);
        ShowMenu();
        GD.Print("UI ready: 主菜单");
    }

    public override void _Process(double delta)
    {
        if (_state != GameState.Battle || _hud == null)
        {
            return;
        }

        string qState = _player.SkillQ.IsReady ? "就绪" : $"{_player.SkillQ.Remaining:0.0}s";
        string eState = _player.SkillE.IsReady ? "就绪" : $"{_player.SkillE.Remaining:0.0}s";
        string task = _taskIsBoss ? "BOSS 讨伐" : $"清剿任务（剩余 {_targets.Count}）";

        var ship = _player.ShipStats;
        string affixHud = ship.CritChance > 0f ? $"  暴击 {ship.CritChance * 100f:0}%" : "";
        if (ship.ThornsPct > 0f)
        {
            affixHud += $"  反伤 {ship.ThornsPct * 100f:0}%";
        }
        if (ship.DamageReductionPct > 0f)
        {
            affixHud += $"  减伤 {ship.DamageReductionPct * 100f:0}%";
        }

        _hud.UpdateStatus(
            $"[第{ZoneLevel}章·{task}]  耐久 {ship.Hull}  护盾 {ship.Shield}/{ship.MaxShield}" +
            $"  |  火力 {ship.Firepower:0}{affixHud}" +
            $"  |  Q过载炮[{qState}]  E护盾[{eState}]" +
            $"  |  合金 {_inventory.Alloy}  模块 {_modulesPicked}  |  F5快存 F9读档·结算自动保存");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state != GameState.Battle)
        {
            return;
        }

        // 清怪 → 短暂延迟 → 任务胜利结算
        if (_waveActive && _targets.Count == 0)
        {
            _jumpTimer += (float)delta;
            if (_jumpTimer >= JumpDelay)
            {
                _jumpTimer = 0f;
                ShowSettlement(true);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_state != GameState.Battle)
        {
            return;
        }
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

    // ---------- UI 流程（UI 规格 v0.2 §1-4） ----------

    private void ClearUi()
    {
        foreach (var child in _uiLayer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void ShowMenu()
    {
        ClearUi();
        _state = GameState.Menu;
        _uiLayer.AddChild(UiScreens.Menu(OnNewGame, ShowSaveList, OnQuit));
    }

    private void ShowNaming()
    {
        ClearUi();
        _state = GameState.Naming;
        _uiLayer.AddChild(UiScreens.Naming(_namingError, OnNameConfirmed, ShowMenu));
    }

    private void ShowSaveList()
    {
        ClearUi();
        _state = GameState.Naming; // 选档属菜单层
        _uiLayer.AddChild(UiScreens.SaveList(_saveService.List(), OnSavePicked, ShowMenu));
    }

    private void ShowStarmap()
    {
        ClearUi();
        _state = GameState.Starmap;
        _background.Color = ZoneColor(ZoneLevel);
        _starMap = new StarMapGenerator().Generate(ZoneLevel, _mothershipLevel, _rng);
        _uiLayer.AddChild(UiScreens.Starmap(_starMap, OnTaskPicked, ShowMothership));
        GD.Print($"星图就绪: 第{_starMap.Chapter}章 母舰Lv{_mothershipLevel} {_starMap.Nodes.Count} 个任务");
    }

    /// <summary>进入母舰内部（LD §6 船坞/仓库/装配/工坊/维修/商店）。</summary>
    private void ShowMothership()
    {
        ClearUi();
        _state = GameState.Mothership;
        ShipFittingService.ApplyToShip(_mothershipShip, _equippedSlots);
        _uiLayer.AddChild(new MothershipPanel(
            _inventory, _equippedSlots, _mothershipShip, _mothershipLevel, _mothershipExp, _presets, _rng,
            onClose: ShowStarmap,
            onChanged: () => { },
            shipClass: _shipClass,
            onShipChange: SwitchShip));
        GD.Print($"母舰内部: 旗舰 {ShipCatalog.DisplayName(_shipClass)}, 合金 {_inventory.Alloy}, 背包 {_inventory.Modules.Count}, 装配 {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count}");
    }

    /// <summary>船坞切换旗舰：换船体 + 槽位重排（保留前 N、超出退回背包、不足补空）。返回新船与槽位；同船型返回 null。</summary>
    private (ShipBase Ship, List<ModuleDrop?> Slots)? SwitchShip(ShipClass shipClass)
    {
        if (_shipClass == shipClass)
        {
            return null;
        }
        ShipBase next = ShipCatalog.Create(shipClass);
        List<ModuleDrop?> newSlots = ShipFittingService.RebaseSlots(_inventory, _equippedSlots, next.ModuleSlots);
        _mothershipShip = next;
        _equippedSlots = newSlots;
        _shipClass = shipClass;
        SaveGame(); // 切换即持久（母舰内无战斗，SaveGame 已兼容取 _mothershipShip.Hull）
        GD.Print($"船坞切换: {ShipCatalog.DisplayName(shipClass)} 槽位 {_equippedSlots.Count}, 背包 {_inventory.Modules.Count}");
        return (next, newSlots);
    }

    private void OnNewGame()
    {
        _namingError = "";
        ShowNaming();
    }

    private void OnNameConfirmed(string name)
    {
        name = name.Trim();
        if (!SaveNameValidator.IsValid(name))
        {
            _namingError = "名字需为 1-12 个字符，且不含 / \\ : * ? \" < > |";
            ShowNaming();
            return;
        }
        if (_saveService.Exists(name))
        {
            _namingError = "这个名字已存在，请换一个（或选继续游戏）";
            ShowNaming();
            return;
        }

        _captainName = name;
        _inventory = new Inventory();
        _modulesPicked = 0;
        _mothershipLevel = 1;
        _mothershipExp = 0;
        _pendingHull = 0;
        _shipClass = ShipClass.Scout;
        _mothershipShip = ShipCatalog.Create(_shipClass);
        _equippedSlots = ShipFittingService.EmptySlots(_mothershipShip.ModuleSlots);
        ShowStarmap();
    }

    private void OnSavePicked(string name)
    {
        SaveData? data = _saveService.Load(name);
        if (data == null)
        {
            GD.Print($"存档 {name} 读取失败，返回主菜单");
            ShowMenu();
            return;
        }

        _captainName = name;
        _modulesPicked = data.ModulesPicked;
        _mothershipLevel = Math.Clamp(data.MothershipLevel, 1, 4);
        _mothershipExp = data.MothershipExp;
        _pendingHull = data.PlayerHull;
        _shipClass = data.ShipClass;
        _mothershipShip = ShipCatalog.Create(_shipClass);
        _inventory = new Inventory();
        _inventory.AddAlloy(data.Alloy);
        foreach (var module in data.Modules)
        {
            _inventory.AddModule(SaveDataMapper.ToDomain(module));
        }
        // 读档槽位：旧存档长度可能与当前船型槽数不一致 → RebaseSlots 保留/退回/补空
        _equippedSlots = data.EquippedSlots.Count > 0
            ? ShipFittingService.RebaseSlots(_inventory, SaveDataMapper.ToDomainSlots(data.EquippedSlots), _mothershipShip.ModuleSlots)
            : ShipFittingService.EmptySlots(_mothershipShip.ModuleSlots);
        GD.Print($"已读档: 章节 {ZoneLevel}, 母舰 Lv{_mothershipLevel}, 旗舰 {ShipCatalog.DisplayName(_shipClass)}, 合金 {_inventory.Alloy}, 背包 {_inventory.Modules.Count}, 装配 {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count}");
        ShowStarmap();
    }

    private void OnQuit() => GetTree().Quit();

    private void OnTaskPicked(StarMapNode node)
    {
        _currentTask = node;
        _taskIsBoss = node.IsBoss;
        StartBattle();
    }

    private void StartBattle()
    {
        ClearUi();
        _state = GameState.Battle;
        _jumpTimer = 0f;

        // 清理上一场战斗节点（防重入累积多艘玩家船：现象=多船同步移动、仅最新一艘有索敌开火）
        if (_player != null && IsInstanceValid(_player))
        {
            _player.QueueFree();
        }
        if (_hud != null && IsInstanceValid(_hud))
        {
            _hud.QueueFree();
        }
        if (_enemies != null && IsInstanceValid(_enemies))
        {
            foreach (var node in _enemies.GetChildren())
            {
                node.QueueFree();
            }
            _enemies.QueueFree();
        }
        _targets.Clear();

        _player = new PlayerShip { Position = Vector2.Zero };
        _player.SetShip(_mothershipShip); // 出战旗舰 = 母舰当前旗舰（同引用：装配/耐久共享）
        _player.Died += () => ShowSettlement(false);
        AddChild(_player);

        _enemies = new Node2D { Name = "Enemies" };
        AddChild(_enemies);

        _hud = new HUD();
        AddChild(_hud);

        // 应用读档耐久
        if (_pendingHull > 0)
        {
            _player.ShipStats.ResetCombatState();
            _player.ShipStats.Hull = Math.Max(1, _pendingHull);
            _pendingHull = 0;
        }
        // 出战装配：空槽自动装入背包最优，再按槽位应用到出战船体
        AutoFit.AutoEquipIntoSlots(_inventory, _equippedSlots);
        ShipFittingService.ApplyToShip(_player.ShipStats, _equippedSlots);

        _taskStartAlloy = _inventory.Alloy;
        _taskStartModules = _modulesPicked;
        SpawnWave();
        GD.Print($"任务开始: 章节{ZoneLevel} 强度{_currentTask!.Strength} Boss={_taskIsBoss} 敌舰 {_targets.Count}");
    }

    // ---------- 任务结算 ----------

    private void ShowSettlement(bool victory)
    {
        if (_state != GameState.Battle)
        {
            return; // 防重复结算
        }
        ClearUi();
        _state = GameState.Settlement;

        int alloyGain = _inventory.Alloy - _taskStartAlloy;
        int moduleGain = _modulesPicked - _taskStartModules;
        string missionSummary = _taskIsBoss
            ? $"BOSS 讨伐 — 第 {ZoneLevel} 章守关旗舰"
            : $"清剿任务 — 强度 {_currentTask!.Strength}（危险 ★{_currentTask.DangerStars}）";

        string lootText;
        if (victory)
        {
            lootText = $"战利品：模块 ×{moduleGain}　合金 +{alloyGain}";
            int expGain = _taskIsBoss ? 2 : 1;
            _mothershipExp += expGain;
            int oldLevel = _mothershipLevel;
            while (_mothershipExp >= ExpPerLevel)
            {
                _mothershipExp -= ExpPerLevel;
                _mothershipLevel++;
            }
            if (_mothershipLevel > oldLevel)
            {
                lootText += $"\n◆ 母舰升级 Lv.{oldLevel} → Lv.{_mothershipLevel}（章节解锁）";
            }
            else
            {
                lootText += $"\n母舰经验 +{expGain}（{_mothershipExp}/{ExpPerLevel}）";
            }
        }
        else
        {
            lootText = "舰长阵亡，远征记录保留 —— 残骸已回收";
            _player.ShipStats.Hull = 1; // 失败存档耐久按最低记录
        }

        SaveGame(); // 任务结算自动存档（远征记录留存，重开可继续）
        lootText += "\n✓ 远征记录已自动保存";
        _uiLayer.AddChild(UiScreens.Settlement(victory, lootText, missionSummary, ShowStarmap));
        GD.Print($"结算: 胜利={victory} 合金+{alloyGain} 模块+{moduleGain} 母舰Lv{_mothershipLevel}");
    }

    // ---------- 波次生成（按任务强度） ----------

    private void SpawnWave()
    {
        ClearEnemies();

        int strength = _currentTask!.Strength;
        // 波次构成域层化（WaveComposer：章节差异化规则可单测，LD Sprint 3 §4.2）
        foreach (var entry in WaveComposer.Compose(ZoneLevel, strength, _taskIsBoss))
        {
            switch (entry.Kind)
            {
                case EnemyKind.Recon:
                    AddEnemies(() => new ReconDrone(), entry.Count, new Color("3ec6ff"), new Vector2(24, 24));
                    break;
                case EnemyKind.Raider:
                    AddEnemies(() => new RaiderShip(), entry.Count, new Color("ff6b4a"), new Vector2(34, 18));
                    break;
                case EnemyKind.Heavy:
                    AddEnemies(() => new HeavyFortress(), entry.Count, new Color("b74aff"), new Vector2(42, 42));
                    break;
                case EnemyKind.Gunboat:
                    AddEnemies(() => new GunboatShip(), entry.Count, new Color("ff9f43"), new Vector2(30, 14));
                    break;
                case EnemyKind.Swarm:
                    AddEnemies(() => new SwarmDrone(), entry.Count, new Color("8dff5a"), new Vector2(14, 14));
                    break;
                case EnemyKind.Boss:
                    AddEnemies(() => new GuardianBoss(), entry.Count, new Color("ff3b6b"), new Vector2(64, 64));
                    break;
            }
        }

        _player.SetTargets(_targets);
        _waveActive = true;
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
        if (_enemies != null)
        {
            foreach (var node in _enemies.GetChildren().OfType<EnemyDrone>())
            {
                node.QueueFree();
            }
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
        1 => new Color("0b1c2c"), // 第1章 航标：深蓝
        2 => new Color("1a1030"), // 第2章 星港：深紫
        3 => new Color("301018"), // 第3章 深空：暗红
        _ => new Color("0a0a0f")  // 终章 坍缩：黑
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
            // 新模块自动装入空槽并即时应用到出战船体
            AutoFit.AutoEquipIntoSlots(_inventory, _equippedSlots);
            ShipFittingService.ApplyToShip(_player.ShipStats, _equippedSlots);
            // 掉落反馈（LD Sprint 3 §4.6 B4）：品质 + 模块名 + 词缀数
            _hud.ShowToast($"获得 {RarityLabel(pickup.ModuleData.Rarity)} {pickup.ModuleData.Name}（{pickup.ModuleData.Affixes.Count} 词缀）");
            GD.Print($"拾取模块: {pickup.ModuleData.Name} | 装配后火力 {_player.ShipStats.Firepower}, 护盾 {_player.ShipStats.Shield}");
        }
        else
        {
            int alloy = ExtractAlloyAmount(pickup.Label);
            _inventory.AddAlloy(alloy);
            _hud.ShowToast($"获得 合金 ×{alloy}");
            GD.Print($"拾取合金×{alloy} | 合金总量 {_inventory.Alloy}");
        }
    }

    private static string RarityLabel(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => "白色",
        ItemRarity.Magic => "蓝色",
        ItemRarity.Rare => "黄色",
        ItemRarity.Set => "绿色",
        ItemRarity.Ancient => "太古",
        _ => "未知"
    };

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
            PlayerHull = _player != null && IsInstanceValid(_player) ? _player.ShipStats.Hull : _mothershipShip.Hull,
            ModulesPicked = _modulesPicked,
            MothershipLevel = _mothershipLevel,
            MothershipExp = _mothershipExp,
            ShipClass = _shipClass
        };
        foreach (var module in _inventory.Modules)
        {
            data.Modules.Add(SaveDataMapper.ToData(module));
        }
        data.EquippedSlots = SaveDataMapper.ToDataSlots(_equippedSlots);
        _saveService.Save(_captainName, data);
        GD.Print($"已存档 -> {_saveService.SavePathFor(_captainName)} (章节 {data.ZoneLevel}, 母舰 Lv{data.MothershipLevel}, 合金 {data.Alloy}, 背包 {data.Modules.Count}, 装配 {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count})");
    }

    private void LoadGame()
    {
        SaveData? data = _saveService.Load(_captainName);
        if (data == null)
        {
            GD.Print("无存档");
            return;
        }
        _mothershipLevel = Math.Clamp(data.MothershipLevel, 1, 4);
        _mothershipExp = data.MothershipExp;
        _inventory.AddAlloy(data.Alloy);
        foreach (var module in data.Modules)
        {
            _inventory.AddModule(SaveDataMapper.ToDomain(module));
        }
        if (data.EquippedSlots.Count > 0)
        {
            _equippedSlots = SaveDataMapper.ToDomainSlots(data.EquippedSlots);
        }
        _player.ShipStats.ResetCombatState();
        _player.ShipStats.Hull = Math.Max(1, data.PlayerHull);
        ShipFittingService.ApplyToShip(_player.ShipStats, _equippedSlots);
        GD.Print($"战斗中读档: 章节 {ZoneLevel}, 合金 {_inventory.Alloy}, 火力 {_player.ShipStats.Firepower}");
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
