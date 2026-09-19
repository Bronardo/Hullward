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
/// Hullward root node (UI spec v0.2 mission loop)：
/// Main menu -> naming/save select -> starmap (random missions) -> mission combat -> settlement -> starmap re-randomizes。
/// Sectors unlock by mothership level; missions grant mothership XP; F5 save / F9 load (file-per-name)。
/// </summary>
public partial class Main : Node
{
    private enum GameState { Menu, Naming, Starmap, Mothership, Battle, Settlement }

    [Export] public float JumpDelay = 2.5f;

    /// <summary>Current sector (= mothership level, 1-4)。</summary>
    public int ZoneLevel => Math.Clamp(_mothershipLevel, 1, 4);

    /// <summary>Mothership XP: 3 points per level。</summary>
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

    // UI flow (main menu -> naming/save select -> starmap -> combat -> settlement)
    private GameState _state = GameState.Menu;
    private CanvasLayer _uiLayer = null!;
    private string _namingError = "";

    // Save system (file-per-name; captain name is the unique key)
    private SaveService _saveService = null!;
    private string _captainName = "captain";

    // Starmap / missions
    private StarMap _starMap = null!;
    private StarMapNode? _currentTask;
    private int _mothershipLevel = 1;
    private int _mothershipExp;
    private int _pendingHull;
    private int _taskStartAlloy;
    private int _taskStartModules;
    private bool _taskIsBoss;
    private string? _taskGateLabel; // Gate-specific label (final sector "Collapse Zone: Relic Guardian", Sprint 4 line B2)

    // Mothership interior (LD §6: fit slots / loadouts / sortie hull; dock §dock: 4-tier flagship switch)
    private ShipBase _mothershipShip = null!;
    private List<ModuleDrop?> _equippedSlots = null!;
    private readonly ShipPresets _presets = new();
    private ShipClass _shipClass = ShipClass.Scout;

    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");
        // Root node always processes input: even when combat paused (Esc), it must respond to resume key
        ProcessMode = ProcessModeEnum.Always;
        _saveService = new SaveService(ProjectSettings.GlobalizePath("user://saves"));

        // Pixel starfield background (color shifts by sector)
        _background = new ColorRect
        {
            Color = ZoneColor(ZoneLevel),
            Size = new Vector2(5000, 5000),
            Position = new Vector2(-2500, -2500)
        };
        AddChild(_background);
        SpawnNebula();
        SpawnStars();

        // Sprint 4 line A: sfx/BGM manager + UI click sound hook (fired by UiScreens.ActionButton)
        _sfx = new Sfx { Name = "Sfx" };
        AddChild(_sfx);
        UiScreens.ClickSound = () => _sfx.PlayClick();

        _uiLayer = new CanvasLayer { Name = "UILayer" };
        AddChild(_uiLayer);
        ShowMenu();
        GD.Print("UI ready: main menu");
    }

    /// <summary>Skill status text: cooling -> remaining seconds; ready but low energy -> "Low Energy" (greyed); otherwise "Ready"。</summary>
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

        // Sprint 6 iteration 19: split combat HUD (LD v0.6.0 §3)
        string task = _taskIsBoss ? (_taskGateLabel ?? "Boss Bounty") : $"Cleansing ({_targets.Count} left)";
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
            TaskTitle = $"Sector {ZoneLevel} · {task}",
            WaveTitle = _taskIsBoss ? "Boss Bounty" : "Cleansing",
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

        // Clear enemies -> short delay -> mission victory settlement
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
                TogglePause(); // Toggle pause/resume during combat (for screenshots)
            }
            else if (_paused)
            {
                return; // Only Esc works while paused
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

    /// <summary>Combat pause/resume: freezes all scene logic (GetTree().Paused), overlays translucent mask for screenshots。</summary>
    private void TogglePause()
    {
        if (_state != GameState.Battle)
        {
            return;
        }
        _paused = !_paused;
        GetTree().Paused = _paused;
        GD.Print(_paused ? "Paused" : "Resumed");

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
                Text = "⏸ Paused - press Esc to resume",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_color", new Color("#d8ecff"));
            box.AddChild(label);

            var toMenu = new Button { Text = "Back to Main Menu (auto-saved)", CustomMinimumSize = new Vector2(260, 44) };
            toMenu.AddThemeFontSizeOverride("font_size", 16);
            toMenu.Pressed += () => { ReturnToMenu(); };
            box.AddChild(toMenu);

            var quit = new Button { Text = "Quit Game", CustomMinimumSize = new Vector2(260, 44) };
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

    // ---------- UI flow (UI spec v0.2 §1-4) ----------

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
        _state = GameState.Naming; // Save select is part of menu layer
        _uiLayer.AddChild(UiScreens.SaveList(_saveService.List(), OnSavePicked, ShowMenu));
    }

    private void ShowStarmap()
    {
        _sfx.PlayLoungeBgm();
        ClearUi();
        _state = GameState.Starmap;
        if (_hud != null) _hud.Visible = false; // Combat HUD only visible during combat
        _background.Color = ZoneColor(ZoneLevel);
        _starMap = new StarMapGenerator().Generate(ZoneLevel, _mothershipLevel, _rng);
        _uiLayer.AddChild(UiScreens.Starmap(_starMap, OnTaskPicked, ShowMothership, ReturnToMenu));
        GD.Print($"Starmap ready: sector {_starMap.Chapter}, mothership Lv{_mothershipLevel}, {_starMap.Nodes.Count} missions");
    }

    /// <summary>Enter mothership interior (LD §6 dock/inventory/fit/workshop/repair/shop)。</summary>
    private void ShowMothership()
    {
        ClearUi();
        _state = GameState.Mothership;
        if (_hud != null) _hud.Visible = false; // Combat HUD only visible during combat
        ShipFittingService.ApplyToShip(_mothershipShip, _equippedSlots);
        _uiLayer.AddChild(new MothershipPanel(
            _inventory, _equippedSlots, _mothershipShip, _mothershipLevel, _mothershipExp, _presets, _rng,
            onClose: ShowStarmap,
            onMenu: ReturnToMenu,
            onChanged: () => { },
            shipClass: _shipClass,
            onShipChange: SwitchShip));
        GD.Print($"Mothership: flagship {ShipCatalog.DisplayName(_shipClass)}, alloy {_inventory.Alloy}, inventory {_inventory.Modules.Count}, fitted {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count}");
    }

    /// <summary>Dock flagship switch: swap hull + rebuild slots (keep first N, overflow to inventory, fill missing with empty). Returns new ship and slots; null if same class。</summary>
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
        SaveGame(); // Persist immediately on switch (no combat in mothership; SaveGame already reads _mothershipShip.Hull)
        GD.Print($"Dock switch: {ShipCatalog.DisplayName(shipClass)} slots {_equippedSlots.Count}, inventory {_inventory.Modules.Count}");
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
            _namingError = "Name must be 1-12 chars and not contain / \\ : * ? \" < > |";
            ShowNaming();
            return;
        }
        if (_saveService.Exists(name))
        {
            _namingError = "That name already exists, pick another (or use Continue)";
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
            GD.Print($"Save {name} failed to load, back to main menu");
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
        // Loading slots: old save length may differ from current ship slot count -> RebaseSlots keeps/returns/fills
        _equippedSlots = data.EquippedSlots.Count > 0
            ? ShipFittingService.RebaseSlots(_inventory, SaveDataMapper.ToDomainSlots(data.EquippedSlots), _mothershipShip.ModuleSlots)
            : ShipFittingService.EmptySlots(_mothershipShip.ModuleSlots);
        GD.Print($"Loaded: sector {ZoneLevel}, mothership Lv{_mothershipLevel}, flagship {ShipCatalog.DisplayName(_shipClass)}, alloy {_inventory.Alloy}, inventory {_inventory.Modules.Count}, fitted {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count}");
        ShowStarmap();
    }

    /// <summary>Iteration 22: back to main menu (auto-save, LD patch v0.6.1)。</summary>
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
        _taskGateLabel = node.GateLabel; // Gate-specific label (final sector "Collapse Zone: Relic Guardian")
        StartBattle();
    }

    private void StartBattle()
    {
        ClearUi();
        _sfx.PlayBattleBgm();
        _state = GameState.Battle;
        _jumpTimer = 0f;

        // Sprint 5 P0-B: combat background swaps nebula + base color by sector
        _background.Color = ZoneColor(ZoneLevel);
        _nebula.Texture = GD.Load<Texture2D>(ZoneNebulaPath(ZoneLevel));

        // Clean up previous battle nodes (prevent re-entry accumulating multiple player ships: symptom = ships move in sync, only latest targets/fires)
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
        _player.SetShip(_mothershipShip); // Sortie flagship = current mothership flagship (same reference: fit/hull shared)
        _player.Died += () => ShowSettlement(false);
        _player.Fired += _sfx.PlayShot;      // Main gun fire (alternate two resources)
        _player.Damaged += _sfx.PlayHit;     // Player damaged
        AddChild(_player);

        _enemies = new Node2D { Name = "Enemies", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_enemies);

        _hud = new HUD { ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_hud);

        // Apply loaded hull
        if (_pendingHull > 0)
        {
            _player.ShipStats.ResetCombatState();
            _player.ShipStats.Hull = Math.Max(1, _pendingHull);
            _pendingHull = 0;
        }
        // Sortie fit: empty slots auto-equip best from inventory, then apply to sortie hull
        AutoFit.AutoEquipIntoSlots(_inventory, _equippedSlots);
        ShipFittingService.ApplyToShip(_player.ShipStats, _equippedSlots);

        _taskStartAlloy = _inventory.Alloy;
        _taskStartModules = _modulesPicked;
        _sfx.PlayWarp(); // Warp into mission zone
        SpawnWave();
        GD.Print($"Mission start: sector {ZoneLevel} power {_currentTask!.Strength} boss={_taskIsBoss} hostiles {_targets.Count}");
    }

    // ---------- Mission settlement ----------

    private void ShowSettlement(bool victory)
    {
        if (_state != GameState.Battle)
        {
            return; // Prevent duplicate settlement
        }
        ClearUi();
        _state = GameState.Settlement;

        int alloyGain = _inventory.Alloy - _taskStartAlloy;
        int moduleGain = _modulesPicked - _taskStartModules;
        string missionSummary = _taskIsBoss
            ? $"{( _taskGateLabel ?? "Boss Bounty" )} — Sector {ZoneLevel} Guardian"
            : $"Cleansing — Power {_currentTask!.Strength} (Danger ★{_currentTask.DangerStars})";

        string lootText;
        if (victory)
        {
            lootText = $"Loot: {moduleGain} modules  +{alloyGain} alloy";
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
                lootText += $"\n◆ Starship leveled Lv.{oldLevel} → Lv.{_mothershipLevel} (new sector unlocked)";
            }
            else
            {
                lootText += $"\nStarship XP +{expGain} ({_mothershipExp}/{ExpPerLevel})";
            }
        }
        else
        {
            lootText = "Ship lost. Log preserved — debris salvaged";
            _player.ShipStats.Hull = 1; // Failure save records hull at minimum
        }

        SaveGame(); // Auto-save on settlement (run record persists; continue on reload)
        lootText += "\n✓ Run auto-saved";
        _uiLayer.AddChild(UiScreens.Settlement(victory, lootText, missionSummary, ShowStarmap));
        GD.Print($"Settlement: victory={victory} alloy+{alloyGain} modules+{moduleGain} mothership Lv{_mothershipLevel}");
    }

    // ---------- Wave composition (by mission power) ----------

    private void SpawnWave()
    {
        ClearEnemies();

        int strength = _currentTask!.Strength;
        // Wave composition in domain layer (WaveComposer: sector-specific rules unit-testable, LD Sprint 3 §4.2)
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
                drone.SummonRequested = SpawnSummon; // Boss summon (Recon/Assault)
                drone.ToastRequested += _hud != null ? _hud.ShowToast : _ => { }; // Phase switch / skill toast
                drone.BossWarnRequested += _sfx.PlayBossWarn; // Boss phase 2/3 warning sfx
            }
            drone.Setup(ship, color, size);
            drone.HitTaken += _sfx.PlayHit; // Enemy hit
            drone.Destroyed += d =>
            {
                _targets.Remove(d);
                _sfx.PlayExplosion(); // Destroyed explosion sfx
                SpawnExplosionFx(d.Position);
                DropLoot(d.Position, _player, d.Ship is GuardianBoss); // Boss guaranteed Rare+ / Ancient drop
            };
            _enemies.AddChild(drone);
            _targets.Add(drone);
        }
    }

    /// <summary>Boss summon: spawn new enemy by type (P3 Assault / others Recon), hook into target list and loot。</summary>
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
        GD.Print($"Boss spawn: {ship.Name} @({position.X:0},{position.Y:0})");
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
        1 => new Color("0b1c2c"), // Sector 1 Beacon: deep blue
        2 => new Color("1a1030"), // Sector 2 Starport: deep purple
        3 => new Color("301018"), // Sector 3 Deep Space: dark red
        _ => new Color("0a0a0f")  // Final sector Collapse: black
    };

    // ---------- Drop / inventory / fit ----------

    private void DropLoot(Vector2 worldPosition, PlayerShip player, bool isBoss = false)
    {
        if (isBoss)
        {
            // LD §4.3 Boss reward: guaranteed Rare+ (Rare/Set/Ancient), Ancient 1-3% boosted by MF; alloy x3
            ModuleDrop bossDrop = _loot.RollBossModule(ZoneLevel, _rng, _player.ShipStats.MagicFind);
            SpawnPickup(Pickup.CreateModule(bossDrop, RarityColor(bossDrop.Rarity)), worldPosition, player);
            int bossAlloy = _loot.RollAlloy(ZoneLevel, _rng, _player.ShipStats.MagicFind) * 3;
            SpawnPickup(Pickup.CreateAlloy(bossAlloy), worldPosition, player);
            GD.Print($"Boss drop: {RarityLabel(bossDrop.Rarity)} {bossDrop.Name} ({bossDrop.Affixes.Count} affixes) alloyx{bossAlloy}");
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
        pickup.ProcessMode = ProcessModeEnum.Pausable; // Drops freeze when combat paused
        // Attached to battle container: StartBattle recreates container so leftover drops are cleaned (otherwise un-picked items from previous battle persist and are un-pickable)
        _enemies.AddChild(pickup);
    }

    private void OnPickupCollected(Pickup pickup)
    {
        _sfx.PlayPickup(); // Pickup sfx (module/alloy unified)
        if (pickup.Kind == Pickup.PickupKind.Module && pickup.ModuleData != null)
        {
            _modulesPicked++;
            _inventory.AddModule(pickup.ModuleData);
            // New module auto-fits into empty slot and applies immediately to sortie hull
            AutoFit.AutoEquipIntoSlots(_inventory, _equippedSlots);
            ShipFittingService.ApplyToShip(_player.ShipStats, _equippedSlots);
            // Drop toast (LD Sprint 3 §4.6 B4): rarity + module name + affix count
            _hud.ShowToast($"Dropped {RarityLabel(pickup.ModuleData.Rarity)} {pickup.ModuleData.DisplayName} ({pickup.ModuleData.Affixes.Count} affixes)");
            GD.Print($"Module pickup: {pickup.ModuleData.DisplayName} | firepower {_player.ShipStats.Firepower}, shield {_player.ShipStats.Shield}");
        }
        else
        {
            int alloy = ExtractAlloyAmount(pickup.Label);
            _inventory.AddAlloy(alloy);
            _hud.ShowToast($"Alloy +{alloy}");
            GD.Print($"Alloy pickup x{alloy} | total {_inventory.Alloy}");
        }
    }

    private static string RarityLabel(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => "Common",
        ItemRarity.Magic => "Magic",
        ItemRarity.Rare => "Rare",
        ItemRarity.Set => "Set",
        ItemRarity.Ancient => "Ancient",
        _ => "Unknown"
    };

    private static int ExtractAlloyAmount(string label)
    {
        int idx = label.IndexOf('×');
        return idx >= 0 && int.TryParse(label[(idx + 1)..], out int n) ? n : 0;
    }

    // ---------- Save ----------

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
        GD.Print($"Saved -> {_saveService.SavePathFor(_captainName)} (sector {data.ZoneLevel}, mothership Lv{data.MothershipLevel}, alloy {data.Alloy}, inventory {data.Modules.Count}, fitted {ShipFittingService.FilledCount(_equippedSlots)}/{_equippedSlots.Count})");
    }

    private void LoadGame()
    {
        SaveData? data = _saveService.Load(_captainName);
        if (data == null)
        {
            GD.Print("No save");
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
        GD.Print($"Mid-battle load: sector {ZoneLevel}, alloy {_inventory.Alloy}, firepower {_player.ShipStats.Firepower}");
    }

    // ---------- Environment ----------

    private static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => new Color("c8c8c8"),
        ItemRarity.Magic => new Color("4da6ff"),
        ItemRarity.Rare => new Color("ffd166"),
        ItemRarity.Set => new Color("6ee06e"),
        ItemRarity.Ancient => new Color("ff7ad9"),
        _ => new Color("ffffff")
    };

    /// <summary>Sector nebula texture (Sprint 5 P0-B: zones 1-4 tiled, four-sector palette cool blue/teal/purple-red/dark red)。</summary>
    private static string ZoneNebulaPath(int zone) => $"res://assets/background/zone{Math.Clamp(zone, 1, 4)}.png";

    private void SpawnNebula()
    {
        // LD §2.3 sector nebula texture tiled over sector base color (reused by Starmap background)
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

    /// <summary>Destroyed explosion fx (CC0 fire frame sequence, plays once and frees itself)。</summary>
    private void SpawnExplosionFx(Vector2 worldPosition)
    {
        var fx = new ExplosionFx
        {
            Position = worldPosition,
            ProcessMode = ProcessModeEnum.Pausable // Explosion animation freezes when paused
        };
        AddChild(fx);
    }

    private void SpawnStars()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        // Sprint 4 line A: CC0 starfield texture (25x24, random scale for depth), layered over nebula
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
