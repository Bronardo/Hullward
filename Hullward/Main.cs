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
    private TextureRect _nebula = null!;
    private Sfx _sfx = null!;
    private HUD _hud = null!;
    private bool _waveActive;
    private float _jumpTimer;
    private int _modulesPicked;
    private bool _paused;
    private CanvasLayer? _pauseOverlay;

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
    private string? _taskGateLabel; // 章节门专属名（终章"坍缩禁区 · 遗迹守护"，Sprint 4 线 B2）

    // 母舰内部（LD §6：装配槽位 / 配装方案 / 出战船体；船坞 §船坞：四档旗舰切换）
    private ShipBase _mothershipShip = null!;
    private List<ModuleDrop?> _equippedSlots = null!;
    private readonly ShipPresets _presets = new();
    private ShipClass _shipClass = ShipClass.Scout;

    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");
        // 主节点始终处理输入：战斗暂停（Esc）时仍需响应按键恢复
        ProcessMode = ProcessModeEnum.Always;
        _saveService = new SaveService(ProjectSettings.GlobalizePath("user://saves"));

        // 像素星空背景（按章节变色）
        _background = new ColorRect
        {
            Color = ZoneColor(ZoneLevel),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500)
        };
        AddChild(_background);
        SpawnNebula();
        SpawnStars();

        // Sprint 4 线 A：音效/BGM 管理器 + UI 点击音效钩子（UiScreens.ActionButton 统一触发）
        _sfx = new Sfx { Name = "Sfx" };
        AddChild(_sfx);
        UiScreens.ClickSound = () => _sfx.PlayClick();

        _uiLayer = new CanvasLayer { Name = "UILayer" };
        AddChild(_uiLayer);
        ShowMenu();
        GD.Print("UI ready: 主菜单");
    }

    /// <summary>技能状态文本：冷却中 → 剩余秒；冷却就绪但能量不足 → "Low Energy"（灰态）；否则"Ready"。</summary>
    private string SkillState(Domain.Combat.ActiveSkill skill)
        => !skill.IsReady
            ? $"{skill.Remaining:0.0}s"
            : _player.ShipStats.HasEnergyFor(skill.EnergyCost) ? "Ready" : "Low Energy";

    public override void _Process(double delta)
    {
        if (_state != GameState.Battle || _hud == null || _paused)
        {
            return;
        }

        // Sprint 6 迭代 19：分区战斗 HUD（LD v0.6.0 §3）
        string task = _taskIsBoss ? (_taskGateLabel ?? "BOSS 讨伐") : $"清剿任务（剩余 {_targets.Count}）";
        var ship = _player.ShipStats;

        var blips = new List<HUD.RadarBlip>();
        foreach (var n in _enemies.GetChildren())
        {
            if (n is EnemyDrone ed && IsInstanceValid(ed))
            {
                blips.Add(new HUD.RadarBlip(ed.Position, ed.Ship is EliteGuardShip, ed.Ship is GuardianBoss));
            }
        }

        _hud.UpdateBattle(new BattleHudData
        {
            TaskTitle = $"第{ZoneLevel}章·{task}",
            WaveTitle = _taskIsBoss ? "BOSS 讨伐" : "清剿任务",
            Shield = (int)ship.Shield, MaxShield = ship.MaxShield,
            Hull = ship.Hull, MaxHull = ship.MaxHull,
            Energy = ship.Energy, MaxEnergy = ship.MaxEnergy,
            Alloy = _inventory.Alloy, ModulesPicked = _modulesPicked,
            EnemiesLeft = _targets.Count,
            MissionHint = _taskIsBoss ? "Destroy the Relic Guardian" : "Destroy all hostiles",
            SkillQReady = _player.SkillQ.IsReady,
            SkillQEnough = ship.HasEnergyFor(_player.SkillQ.EnergyCost),
            SkillQRemain = _player.SkillQ.Remaining,
            SkillQCooldown = _player.SkillQ.Cooldown,
            SkillEReady = _player.SkillE.IsReady,
            SkillEEnough = ship.HasEnergyFor(_player.SkillE.EnergyCost),
            SkillERemain = _player.SkillE.Remaining,
            SkillECooldown = _player.SkillE.Cooldown,
            ShipName = ship.Name, ShipLevel = _mothershipLevel,
            Firepower = ship.Firepower, FireRate = ship.FireRateMultiplier,
            PlayerPos = _player.Position,
            Hostiles = blips
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state != GameState.Battle || _paused)
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
            if (key.Keycode == Key.Escape)
            {
                TogglePause(); // 战斗中 Esc 暂停/继续（截图用）
            }
            else if (_paused)
            {
                return; // 暂停时仅 Esc 有效
            }
            else if (key.Keycode == Key.F5)
            {
                SaveGame();
            }
            else if (key.Keycode == Key.F9)
            {
                LoadGame();
            }
        }
    }

    /// <summary>战斗暂停/恢复：冻结场景全部逻辑（GetTree().Paused），叠加半透明遮罩便于截图。</summary>
    private void TogglePause()
    {
        if (_state != GameState.Battle)
        {
            return;
        }
        _paused = !_paused;
        GetTree().Paused = _paused;
        GD.Print(_paused ? "战斗暂停" : "战斗继续");

        if (_paused)
        {
            _pauseOverlay = new CanvasLayer { Layer = 100, ProcessMode = ProcessModeEnum.Always };
            var shade = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, 0.45f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var box = new VBoxContainer { ProcessMode = ProcessModeEnum.Always };
            box.SetAnchorsPreset(Control.LayoutPreset.Center);
            var label = new Label
            {
                Text = "⏸ 已暂停 — 按 Esc 继续",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_color", new Color("#d8ecff"));
            box.AddChild(label);

            var toMenu = new Button { Text = "返回主菜单（自动保存）", CustomMinimumSize = new Vector2(260, 44) };
            toMenu.AddThemeFontSizeOverride("font_size", 16);
            toMenu.Pressed += () => { ReturnToMenu(); };
            box.AddChild(toMenu);

            var quit = new Button { Text = "退出游戏", CustomMinimumSize = new Vector2(260, 44) };
            quit.AddThemeFontSizeOverride("font_size", 16);
            quit.Pressed += () => { GetTree().Quit(); };
            box.AddChild(quit);

            _pauseOverlay.AddChild(shade);
            _pauseOverlay.AddChild(box);
            AddChild(_pauseOverlay);
        }
        else
        {
            _pauseOverlay?.QueueFree();
            _pauseOverlay = null;
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
        _sfx.PlayLoungeBgm();
        ClearUi();
        _state = GameState.Starmap;
        if (_hud != null) _hud.Visible = false; // 战斗 HUD 仅战斗中显示
        _background.Color = ZoneColor(ZoneLevel);
        _starMap = new StarMapGenerator().Generate(ZoneLevel, _mothershipLevel, _rng);
        _uiLayer.AddChild(UiScreens.Starmap(_starMap, OnTaskPicked, ShowMothership, ReturnToMenu));
        GD.Print($"星图就绪: 第{_starMap.Chapter}章 母舰Lv{_mothershipLevel} {_starMap.Nodes.Count} 个任务");
    }

    /// <summary>进入母舰内部（LD §6 船坞/仓库/装配/工坊/维修/商店）。</summary>
    private void ShowMothership()
    {
        ClearUi();
        _state = GameState.Mothership;
        if (_hud != null) _hud.Visible = false; // 战斗 HUD 仅战斗中显示
        ShipFittingService.ApplyToShip(_mothershipShip, _equippedSlots);
        _uiLayer.AddChild(new MothershipPanel(
            _inventory, _equippedSlots, _mothershipShip, _mothershipLevel, _mothershipExp, _presets, _rng,
            onClose: ShowStarmap,
            onMenu: ReturnToMenu,
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

    /// <summary>迭代 22：返回主菜单（自动保存，LD 补丁 v0.6.1）。</summary>
    private void ReturnToMenu()
    {
        _sfx.PlayLoungeBgm();
        SaveGame();
        GetTree().Paused = false;
        _paused = false;
        if (_pauseOverlay != null) { _pauseOverlay.QueueFree(); _pauseOverlay = null; }
        ShowMenu();
    }

    private void OnQuit() => GetTree().Quit();

    private void OnTaskPicked(StarMapNode node)
    {
        _currentTask = node;
        _taskIsBoss = node.IsBoss;
        _taskGateLabel = node.GateLabel; // 章节门专属名（终章"坍缩禁区 · 遗迹守护"）
        StartBattle();
    }

    private void StartBattle()
    {
        ClearUi();
        _sfx.PlayBattleBgm();
        _state = GameState.Battle;
        _jumpTimer = 0f;

        // Sprint 5 P0-B：战斗背景按章节换星云 + 底色
        _background.Color = ZoneColor(ZoneLevel);
        _nebula.Texture = GD.Load<Texture2D>(ZoneNebulaPath(ZoneLevel));

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

        _player = new PlayerShip { Position = Vector2.Zero, ProcessMode = ProcessModeEnum.Pausable };
        _player.SetShip(_mothershipShip); // 出战旗舰 = 母舰当前旗舰（同引用：装配/耐久共享）
        _player.Died += () => ShowSettlement(false);
        _player.Fired += _sfx.PlayShot;      // 主炮射击（双资源轮换）
        _player.Damaged += _sfx.PlayHit;     // 玩家受击
        AddChild(_player);

        _enemies = new Node2D { Name = "Enemies", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_enemies);

        _hud = new HUD { ProcessMode = ProcessModeEnum.Pausable };
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
        _sfx.PlayWarp(); // 跃迁进入任务区
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
            ? $"{( _taskGateLabel ?? "BOSS 讨伐" )} — 第 {ZoneLevel} 章守关旗舰"
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
                case EnemyKind.Elite:
                    AddEnemies(() => new EliteGuardShip(), entry.Count, new Color("ffd166"), new Vector2(52, 44));
                    break;
                case EnemyKind.Boss:
                    AddEnemies(() => new GuardianBoss(), entry.Count, new Color("ff3b6b"), new Vector2(64, 64), isBoss: true);
                    break;
            }
        }

        _player.SetTargets(_targets);
        _waveActive = true;
    }

    private void AddEnemies(Func<EnemyShip> factory, int count, Color color, Vector2 size, bool isBoss = false)
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
            if (isBoss)
            {
                drone.SummonRequested = SpawnSummon; // Boss 召唤（侦察机/突击舰）
                drone.ToastRequested += _hud != null ? _hud.ShowToast : _ => { }; // 阶段切换/技能提示
                drone.BossWarnRequested += _sfx.PlayBossWarn; // Boss 阶段 2/3 警示音
            }
            drone.Setup(ship, color, size);
            drone.HitTaken += _sfx.PlayHit; // 敌舰受击
            drone.Destroyed += d =>
            {
                _targets.Remove(d);
                _sfx.PlayExplosion(); // 击毁爆炸音
                SpawnExplosionFx(d.Position);
                DropLoot(d.Position, _player, d.Ship is GuardianBoss); // Boss 必掉黄+ / 暗金
            };
            _enemies.AddChild(drone);
            _targets.Add(drone);
        }
    }

    /// <summary>Boss 召唤：按种类生成新敌舰（P3 突击舰 / 其余侦察机），接入目标列表与掉落。</summary>
    private void SpawnSummon(EnemyKind kind, Vector2 position)
    {
        var (factory, color, size) = kind switch
        {
            EnemyKind.Raider => ((Func<EnemyShip>)(() => new RaiderShip()), new Color("ff6b4a"), new Vector2(34, 18)),
            _ => (() => new ReconDrone(), new Color("3ec6ff"), new Vector2(24, 24))
        };
        var ship = factory();
        ship.ScaleForZone(ZoneLevel);
        var drone = new EnemyDrone
        {
            Player = _player,
            Position = position
        };
        drone.Setup(ship, color, size);
        drone.HitTaken += _sfx.PlayHit;
        drone.Destroyed += d =>
        {
            _targets.Remove(d);
            _sfx.PlayExplosion();
            SpawnExplosionFx(d.Position);
            DropLoot(d.Position, _player);
        };
        _enemies.AddChild(drone);
        _targets.Add(drone);
        _player.SetTargets(_targets);
        GD.Print($"Boss 召唤: {ship.Name} @({position.X:0},{position.Y:0})");
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

    private void DropLoot(Vector2 worldPosition, PlayerShip player, bool isBoss = false)
    {
        if (isBoss)
        {
            // LD §4.3 Boss 奖励：必掉黄+（Rare/Set/Ancient），暗金 1-3% 受 MF 加成；合金 ×3
            ModuleDrop bossDrop = _loot.RollBossModule(ZoneLevel, _rng, _player.ShipStats.MagicFind);
            SpawnPickup(Pickup.CreateModule(bossDrop, RarityColor(bossDrop.Rarity)), worldPosition, player);
            int bossAlloy = _loot.RollAlloy(ZoneLevel, _rng, _player.ShipStats.MagicFind) * 3;
            SpawnPickup(Pickup.CreateAlloy(bossAlloy), worldPosition, player);
            GD.Print($"Boss 掉落: {RarityLabel(bossDrop.Rarity)} {bossDrop.Name}（{bossDrop.Affixes.Count} 词缀）合金×{bossAlloy}");
            return;
        }
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
        pickup.ProcessMode = ProcessModeEnum.Pausable; // 战斗暂停时掉落物静止
        // 挂到战斗容器：StartBattle 重建容器时残留掉落物一并清理（否则上局未拾取道具会遗留到新战斗且无法拾取）
        _enemies.AddChild(pickup);
    }

    private void OnPickupCollected(Pickup pickup)
    {
        _sfx.PlayPickup(); // 拾取音效（模块/合金统一）
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
            _hud.ShowToast($"Alloy +{alloy}");
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

    /// <summary>章节星云纹理（Sprint 5 P0-B：zone1-4 平铺，四章配色 冷蓝/青绿/紫红/暗红）。</summary>
    private static string ZoneNebulaPath(int zone) => $"res://assets/background/zone{Math.Clamp(zone, 1, 4)}.png";

    private void SpawnNebula()
    {
        // LD §2.3 章节星云纹理平铺，叠加在章节底色之上（StarMap 背景亦复用该素材）
        _nebula = new TextureRect
        {
            Texture = GD.Load<Texture2D>(ZoneNebulaPath(ZoneLevel)),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500),
            StretchMode = TextureRect.StretchModeEnum.Tile,
            Modulate = new Color(1f, 1f, 1f, 0.6f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_nebula);
    }

    /// <summary>击毁爆炸特效（CC0 fire 帧序列，播放一次自毁）。</summary>
    private void SpawnExplosionFx(Vector2 worldPosition)
    {
        var fx = new ExplosionFx
        {
            Position = worldPosition,
            ProcessMode = ProcessModeEnum.Pausable // 暂停时爆炸动画冻结
        };
        AddChild(fx);
    }

    private void SpawnStars()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        // Sprint 4 线 A：CC0 星点纹理（25×24，随机缩放出大小层次），叠加在星云之上
        var tex = GD.Load<Texture2D>("res://assets/effects/star.png");
        for (int i = 0; i < 120; i++)
        {
            float s = rng.RandfRange(0.06f, 0.16f);
            var star = new Sprite2D
            {
                Texture = tex,
                Scale = new Vector2(s, s),
                Modulate = new Color(0.75f, 0.9f, 1f, rng.RandfRange(0.5f, 0.9f)),
                Position = new Vector2(rng.RandfRange(-960, 960), rng.RandfRange(-540, 540))
            };
            AddChild(star);
        }
    }
}
