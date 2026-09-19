using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// mothershippanel（LD UI spec v0.2 §6 + 空间站清单 §4 + dock）：
/// dock（四档flagshipselect/unlock）· fit（slot装卸/2 套方案/attribute预览）· inventory（rarityfilter）· workshop（disassemble/reroll）· repair · shop。
/// codebuild；操作即时修改域state并重建content区。
/// </summary>
public sealed partial class MothershipPanel : Control
{
    private const string TitleColor = "7fd4ff";
    private const string SubColor = "9aa7c0";
    private const string TextColor = "e8ecf4";
    private const string PanelColor = "10131f";

    private readonly Inventory _inventory;
    private readonly List<ModuleDrop?> _slots;
    private ShipBase _ship;
    private readonly int _mothershipLevel;
    private readonly int _mothershipExp;
    private readonly ShipPresets _presets;
    private readonly Random _rng;
    private readonly Action _onClose;
    private readonly Action _onMenu;
    private readonly Action _onChanged;
    private ShipClass _shipClass;
    private readonly Func<ShipClass, (ShipBase Ship, List<ModuleDrop?> Slots)?> _onShipChange;

    private int _tab = 1;             // 0 船坞 / 1 装配 / 2 仓库 / 3 工坊 / 4 维修 / 5 商店（默认进装配）
    private int _selectedSlot = -1;   // 装配页选中槽位
    private ItemRarity? _rarityFilter;
    private string _status = "";

    public MothershipPanel(
        Inventory inventory,
        List<ModuleDrop?> slots,
        ShipBase ship,
        int mothershipLevel,
        int mothershipExp,
        ShipPresets presets,
        Random rng,
        Action onClose,
        Action onMenu,
        Action onChanged,
        ShipClass shipClass,
        Func<ShipClass, (ShipBase Ship, List<ModuleDrop?> Slots)?> onShipChange)
    {
        _inventory = inventory;
        _slots = slots;
        _ship = ship;
        _mothershipLevel = mothershipLevel;
        _mothershipExp = mothershipExp;
        _presets = presets;
        _rng = rng;
        _onClose = onClose;
        _onMenu = onMenu;
        _onChanged = onChanged;
        _shipClass = shipClass;
        _onShipChange = onShipChange;

        // 关键：panel挂到 CanvasLayer（非 Control 父），锚点相对视口。
        // 必须在进树（_Ready）之前设好 FullRect——与 UiScreens.Fullscreen() 的
        // "构造时 SetAnchorsPreset" patterns一致；若拖到 _Ready 才设，进树首帧布局
        // 已按default锚点算过 rect（0x0），content塌缩到左上角 min size 区。
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        // panel自身满屏（与 UiScreens.Fullscreen 一致）：挂到 CanvasLayer 时若无锚点，
        // rect 保持 0x0，FullRect 子node随之塌缩 → content区空白（LD B5 复现fix）。
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ShipFittingService.ApplyToShip(_ship, _slots); // 进入母舰即同步属性（维修/预览口径一致）
        Rebuild();
    }

    // ---------- build ----------

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var root = new ColorRect
        {
            Color = new Color(PanelColor),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Sprint 5 P0-B：mothership深色金属科技grid底纹（LD §6 色板 #16121F 系），平铺于六页
        var gridTex = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/background/mothership_grid.png"),
            StretchMode = TextureRect.StretchModeEnum.Tile,
            Modulate = new Color(1f, 1f, 1f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        gridTex.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(gridTex);

        var layout = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layout.OffsetLeft = 24;
        layout.OffsetRight = -24;
        layout.OffsetTop = 16;
        layout.OffsetBottom = -16;
        layout.AddThemeConstantOverride("separation", 10);
        root.AddChild(layout);

        layout.AddChild(BuildHeader());
        layout.AddChild(BuildTabs());
        layout.AddChild(BuildStatus());

        var contentHost = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 200) // 保证内容区最小可见高度（防容器分配 0 高度）
        };
        contentHost.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("151a28"), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        contentHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        layout.AddChild(contentHost);
        contentHost.AddChild(BuildContent());
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var title = new Label
        {
            Text = $"⚙ Starship — Lv.{_mothershipLevel} (XP {_mothershipExp})　Hull: {_ship.Name}　Alloy: {_inventory.Alloy}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(TitleColor));
        header.AddChild(title);

        var close = new Button { Text = "Back to Map", CustomMinimumSize = new Vector2(140, 40) };
        close.AddThemeFontSizeOverride("font_size", 16);
        close.AddThemeColorOverride("font_color", new Color("10131f"));
        close.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("6ee06e"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("6ee06e").Lightened(0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("6ee06e").Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        close.Pressed += _onClose;
        header.AddChild(close);

        // iteration 22：main menubutton（backstarmap旁）
        var menu = new Button { Text = "⌂ Main Menu", CustomMinimumSize = new Vector2(120, 40) };
        menu.AddThemeFontSizeOverride("font_size", 16);
        menu.AddThemeColorOverride("font_color", new Color("#d8ecff"));
        menu.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("#3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        menu.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("#4a5a80"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        menu.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        menu.Pressed += _onMenu;
        header.AddChild(menu);
        return header;
    }

    private Control BuildTabs()
    {
        string[] names = { "Dock", "Equip", "Inventory", "Workshop", "Repair", "Shop" };
        var tabs = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tabs.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < names.Length; i++)
        {
            int tab = i;
            var btn = new Button
            {
                Text = names[i],
                CustomMinimumSize = new Vector2(120, 40),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            btn.AddThemeFontSizeOverride("font_size", 18);
            bool active = i == _tab;
            btn.AddThemeColorOverride("font_color", active ? new Color("10131f") : new Color(TextColor));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = active ? new Color(TitleColor) : new Color("1b2233"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = active ? new Color(TitleColor).Lightened(0.1f) : new Color("2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.Pressed += () =>
            {
                _tab = tab;
                _status = "";
                Rebuild();
            };
            tabs.AddChild(btn);
        }
        return tabs;
    }

    private Control BuildStatus()
    {
        var status = new Label
        {
            Text = _status,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        status.AddThemeFontSizeOverride("font_size", 15);
        status.AddThemeColorOverride("font_color", new Color("ffd166"));
        return status;
    }

    private Control BuildContent() => _tab switch
    {
        0 => BuildDockTab(),
        2 => BuildStorageTab(),
        3 => BuildWorkshopTab(),
        4 => BuildRepairTab(),
        5 => BuildShopTab(),
        _ => BuildEquipTab()
    };

    private static ScrollContainer MakeScroll(VBoxContainer box)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddThemeConstantOverride("separation", 0);
        box.CustomMinimumSize = new Vector2(0, 0);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(box);
        return scroll;
    }

    // ---------- dock页（shipselect） ----------

    private Control BuildDockTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        var hint = new Label
        {
            Text = "Dock — four hull classes unlock with Starship level; slots auto-reset on switch, overflow returns to inventory",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        box.AddChild(hint);

        foreach (ShipClass shipClass in ShipCatalog.All())
        {
            ShipBase preview = ShipCatalog.Create(shipClass);
            bool current = shipClass == _shipClass;
            bool unlocked = ShipCatalog.IsUnlocked(shipClass, _mothershipLevel);

            var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = current ? new Color("1e3a52") : new Color("151a28"),
                BorderColor = current ? new Color(TitleColor) : new Color("2a3550"),
                BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
            });

            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 12);

            var info = new Label
            {
                Text = $"{ShipCatalog.DisplayName(shipClass)}　{ShipCatalog.Role(shipClass)}" +
                       (current ? "\n◆ Active Flagship" : "") +
                       (unlocked ? "" : $"\n· Unlocks at Starship Lv.{(int)shipClass}"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 16);
            info.AddThemeColorOverride("font_color", current ? new Color(TitleColor) : new Color(TextColor));

            var stats = new Label
            {
                Text = $"Hull {preview.MaxHull}｜Shield {preview.MaxShield}｜Firepower {preview.Firepower:0}｜Armor {preview.Armor}｜Speed {preview.Speed:0}｜Slots {preview.ModuleSlots}",
                HorizontalAlignment = HorizontalAlignment.Right,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            stats.AddThemeFontSizeOverride("font_size", 14);
            stats.AddThemeColorOverride("font_color", new Color(SubColor));

            var switchBtn = new Button
            {
                Text = current ? "Active" : unlocked ? "Switch" : "Locked",
                CustomMinimumSize = new Vector2(140, 40),
                Disabled = current || !unlocked,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            switchBtn.AddThemeFontSizeOverride("font_size", 14);
            if (unlocked && !current)
            {
                StyleSmall(switchBtn);
            }
            else
            {
                switchBtn.AddThemeColorOverride("font_color", new Color(SubColor));
                switchBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("1b2233"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                switchBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            }
            switchBtn.Pressed += () =>
            {
                var result = _onShipChange(shipClass);
                if (result != null)
                {
                    _ship = result.Value.Ship;
                    _shipClass = shipClass;
                    _slots.Clear();
                    _slots.AddRange(result.Value.Slots);
                    _status = $"Flagship switched: {ShipCatalog.DisplayName(shipClass)} ({_slots.Count} slots)";
                    _onChanged();
                    Rebuild();
                }
            };

            row.AddChild(info);
            row.AddChild(stats);
            row.AddChild(switchBtn);
            card.AddChild(row);
            box.AddChild(card);
        }

        return MakeScroll(box);
    }

    // ---------- fit页 ----------

    private Control BuildEquipTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        // attribute预览（含affixbonusattribute，LD Sprint 3 §4.6 B3：affix明细可见）
        ShipFittingService.ApplyToShip(_ship, _slots);
        var affixStats = new List<string> { $"Firepower {_ship.Firepower:0}", $"Shield {_ship.Shield}/{_ship.MaxShield}", $"Hull {_ship.Hull}/{_ship.MaxHull}", $"Atk Speed ×{_ship.FireRateMultiplier:0.00}", $"Resist {_ship.Armor}" };
        if (_ship.MagicFind > 0)
        {
            affixStats.Add($"MF {_ship.MagicFind}");
        }
        if (_ship.CritChance > 0f)
        {
            affixStats.Add($"Crit {_ship.CritChance * 100f:0}%");
        }
        if (_ship.CritDamage > 2f)
        {
            affixStats.Add($"Crit Dmg ×{_ship.CritDamage:0.00}");
        }
        if (_ship.DamageReductionPct > 0f)
        {
            affixStats.Add($"DR {_ship.DamageReductionPct * 100f:0}%");
        }
        if (_ship.ThornsPct > 0f)
        {
            affixStats.Add($"Thorns {_ship.ThornsPct * 100f:0}%");
        }
        // Sprint 5 iteration 17 C2：attribute预览三栏分组（攻击/防御/skill，LD §7）
        var attackRows = new List<string> { $"Firepower {_ship.Firepower:0}", $"Atk Speed ×{_ship.FireRateMultiplier:0.00}", $"Crit {_ship.CritChance * 100f:0}%", $"Crit Dmg ×{_ship.CritDamage:0.00}" };
        var defenseRows = new List<string> { $"Shield {_ship.Shield}/{_ship.MaxShield}", $"Hull {_ship.Hull}/{_ship.MaxHull}", $"Resist {_ship.Armor}" };
        if (_ship.DamageReductionPct > 0f) { defenseRows.Add($"DR {_ship.DamageReductionPct * 100f:0}%"); }
        if (_ship.ThornsPct > 0f) { defenseRows.Add($"Thorns {_ship.ThornsPct * 100f:0}%"); }
        var skillRows = new List<string> { $"Energy {_ship.Energy}/{_ship.MaxEnergy}", $"Regen {_ship.EnergyRegenBonus:0}·s", $"Q Cost {_ship.EffectiveSkillCost(30)} / E Cost {_ship.EffectiveSkillCost(40)}" };
        if (_ship.SkillCooldownMultiplier < 1f) { skillRows.Add($"CD ×{_ship.SkillCooldownMultiplier:0.00}"); }
        if (_ship.MagicFind > 0) { skillRows.Add($"MF +{_ship.MagicFind}"); }

        var statsRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        statsRow.AddThemeConstantOverride("separation", 8);
        statsRow.AddChild(StatColumn("⚔ Attack", attackRows));
        statsRow.AddChild(StatColumn("🛡 Defense", defenseRows));
        statsRow.AddChild(StatColumn("✦ Skills", skillRows));
        box.AddChild(statsRow);

        // slotlist（点击选medium/卸下；showaffix count，LD §4.6 B3）
        var slotRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        slotRow.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < _slots.Count; i++)
        {
            int slotIndex = i;
            ModuleDrop? drop = _slots[i];
            string label = drop == null
                ? $"[{i + 1}] Empty Slot"
                : $"[{i + 1}] {drop.DisplayName}" + (drop.Affixes.Count > 0 ? $" ({drop.Affixes.Count} affixes)" : "");
            // slotrow：IconBox fixed 20×20（与list项一致，不using Button.Icon avoidance拉伸）+ 文字button
            var slotBox = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            slotBox.AddThemeConstantOverride("separation", 6);
            if (drop != null)
            {
                slotBox.AddChild(IconBox(drop.Slot, drop.Rarity));
            }
            var btn = new Button
            {
                Text = label,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(210, 44),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            btn.AddThemeFontSizeOverride("font_size", 14);
            bool selected = i == _selectedSlot;
            btn.AddThemeColorOverride("font_color", selected ? new Color("10131f") : new Color(TextColor));
            Color bg = drop == null ? new Color("1b2233") : RarityColor(drop.Rarity);
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = selected ? new Color(TitleColor) : bg, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = bg.Lightened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.Pressed += () =>
            {
                _selectedSlot = _selectedSlot == slotIndex ? -1 : slotIndex;
                Rebuild();
            };
            slotBox.AddChild(btn);
            slotRow.AddChild(slotBox);
        }

        // 卸下选medium槽
        var unequip = new Button { Text = "Unequip Slot", CustomMinimumSize = new Vector2(140, 44) };
        unequip.AddThemeFontSizeOverride("font_size", 15);
        unequip.AddThemeColorOverride("font_color", new Color(TextColor));
        unequip.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        unequip.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("4a5a80"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        unequip.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        unequip.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        unequip.Pressed += () =>
        {
            if (_selectedSlot >= 0 && ShipFittingService.TryUnequip(_inventory, _slots, _selectedSlot))
            {
                _selectedSlot = -1;
                _status = "Module returned to inventory";
                _onChanged();
                Rebuild();
            }
            else
            {
                _status = "Select an equipped slot first";
                Rebuild();
            }
        };
        slotRow.AddChild(unequip);

        // 配装方案
        var presetRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        presetRow.AddThemeConstantOverride("separation", 8);
        var saveA = new Button { Text = _presets.IsSaved('A') ? $"Loadout A（{_presets.NameOf('A')}）" : "Save as Loadout A", CustomMinimumSize = new Vector2(200, 40) };
        var applyA = new Button { Text = "Apply Loadout A", CustomMinimumSize = new Vector2(130, 40) };
        var saveB = new Button { Text = _presets.IsSaved('B') ? $"Loadout B（{_presets.NameOf('B')}）" : "Save as Loadout B", CustomMinimumSize = new Vector2(200, 40) };
        var applyB = new Button { Text = "Apply Loadout B", CustomMinimumSize = new Vector2(130, 40) };
        StyleSmall(saveA); StyleSmall(saveB); StyleSmall(applyA); StyleSmall(applyB);
        saveA.Pressed += () => { _presets.Save('A', "Loadout A", _slots); _status = "Loadout A saved (current equip snapshot)"; Rebuild(); };
        applyA.Pressed += () => ApplyPreset('A');
        saveB.Pressed += () => { _presets.Save('B', "Loadout B", _slots); _status = "Loadout B saved (current equip snapshot)"; Rebuild(); };
        applyB.Pressed += () => ApplyPreset('B');
        presetRow.AddChild(saveA); presetRow.AddChild(applyA);
        presetRow.AddChild(saveB); presetRow.AddChild(applyB);
        box.AddChild(slotRow);
        box.AddChild(presetRow);

        // 选mediumslotaffix明细（LD §4.6 B3：fitUI可见affix name称+stat）
        if (_selectedSlot >= 0 && _slots[_selectedSlot] != null)
        {
            ModuleDrop selected = _slots[_selectedSlot]!;
            var detail = new Label
            {
                Text = $"◆ Slot {_selectedSlot + 1}: {selected.DisplayName}\n{selected.AffixSummary()}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            detail.AddThemeFontSizeOverride("font_size", 14);
            detail.AddThemeColorOverride("font_color", RarityColor(selected.Rarity));
            box.AddChild(detail);
        }

        box.AddChild(new Label { Text = "Click a slot to select, then click a module below to equip it (auto-fills first empty slot if none selected)", HorizontalAlignment = HorizontalAlignment.Left, MouseFilter = Control.MouseFilterEnum.Ignore });

        // inventorymodulelist（点击装入）
        var bagBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bagBox.AddThemeConstantOverride("separation", 6);
        if (_inventory.Modules.Count == 0)
        {
            bagBox.AddChild(InfoLabel("Inventory is empty — go on sortie and bring modules back"));
        }
        for (int i = 0; i < _inventory.Modules.Count; i++)
        {
            int moduleIndex = i;
            ModuleDrop drop = _inventory.Modules[i];
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(IconBox(drop.Slot, drop.Rarity));
            var info = new Label
            {
                Text = $"{drop.DisplayName}  {drop.AffixSummary().Replace("\n", " ｜ ")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 14);
            info.AddThemeColorOverride("font_color", RarityColor(drop.Rarity));
            var equip = new Button { Text = "Equip", CustomMinimumSize = new Vector2(70, 34) };
            StyleSmall(equip);
            equip.Pressed += () =>
            {
                int target = _selectedSlot >= 0 ? _selectedSlot : FirstEmptySlot();
                if (target >= 0 && ShipFittingService.TryEquip(_inventory, _slots, moduleIndex, target))
                {
                    _status = $"Equipped to slot {target + 1}";
                    _selectedSlot = -1;
                    _onChanged();
                    Rebuild();
                }
                else
                {
                    _status = "Slot full or no empty slot available";
                    Rebuild();
                }
            };
            row.AddChild(info);
            row.AddChild(equip);
            bagBox.AddChild(ModuleCard(row, drop.Rarity));
        }
        box.AddChild(bagBox);

        return MakeScroll(box);
    }

    private void ApplyPreset(char id)
    {
        if (_presets.TryApply(id, _inventory, _slots))
        {
            _status = $"Applied Loadout {id}";
            _onChanged();
            Rebuild();
        }
        else
        {
            _status = $"Loadout {id} not saved or module missing";
            Rebuild();
        }
    }

    private int FirstEmptySlot()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    // ---------- inventory页 ----------

    private Control BuildStorageTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        var filterRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        filterRow.AddThemeConstantOverride("separation", 6);
        string[] options = { "All", "Common", "Magic", "Rare", "Set", "Ancient" };
        ItemRarity?[] rarities = { null, ItemRarity.Common, ItemRarity.Magic, ItemRarity.Rare, ItemRarity.Set, ItemRarity.Ancient };
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text = options[i],
                CustomMinimumSize = new Vector2(70, 36),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            StyleSmall(btn);
            bool active = _rarityFilter == rarities[i];
            btn.AddThemeColorOverride("font_color", active ? new Color("10131f") : new Color(TextColor));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = active ? new Color(TitleColor) : new Color("1b2233"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.Pressed += () => { _rarityFilter = rarities[idx]; Rebuild(); };
            filterRow.AddChild(btn);
        }
        box.AddChild(filterRow);

        var count = new Label
        {
            Text = $"Inventory: {_inventory.Modules.Count} modules｜Alloy: {_inventory.Alloy}",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        count.AddThemeFontSizeOverride("font_size", 15);
        count.AddThemeColorOverride("font_color", new Color(SubColor));
        box.AddChild(count);

        var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 6);
        var filtered = _inventory.Modules
            .Select((m, i) => new { M = m, I = i })
            .Where(x => _rarityFilter == null || x.M.Rarity == _rarityFilter)
            .ToList();
        if (filtered.Count == 0)
        {
            list.AddChild(InfoLabel("No modules match the filter"));
        }
        foreach (var entry in filtered)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(IconBox(entry.M.Slot, entry.M.Rarity));
            var info = new Label
            {
                Text = $"{entry.M.DisplayName}　{entry.M.AffixSummary().Replace("\n", " ｜ ")}" +
                       (entry.M.Rarity == ItemRarity.Rare || entry.M.Rarity == ItemRarity.Set ? $"　 (Reroll cost {entry.M.RerollCost})" : ""),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 14);
            info.AddThemeColorOverride("font_color", RarityColor(entry.M.Rarity));
            row.AddChild(info);
            list.AddChild(ModuleCard(row, entry.M.Rarity));
        }
        box.AddChild(list);
        return MakeScroll(box);
    }

    // ---------- workshop页 ----------

    private Control BuildWorkshopTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        var hint = new Label
        {
            Text = "Disassemble: Common 1 / Magic 3 / Rare 8 / Set 15 Alloy (Ancient cannot be scrapped)｜Reroll: Rare+ re-rolls affixes, cost rises 5/10/20/40…",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        box.AddChild(hint);

        var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 6);
        if (_inventory.Modules.Count == 0)
        {
            list.AddChild(InfoLabel("Inventory is empty"));
        }
        for (int i = 0; i < _inventory.Modules.Count; i++)
        {
            int moduleIndex = i;
            ModuleDrop drop = _inventory.Modules[i];
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(IconBox(drop.Slot, drop.Rarity));
            var info = new Label
            {
                Text = $"{drop.DisplayName}　{drop.AffixSummary().Replace("\n", " ｜ ")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 14);
            info.AddThemeColorOverride("font_color", RarityColor(drop.Rarity));
            row.AddChild(info);

            if (WorkshopService.CanDisassemble(drop.Rarity))
            {
                var scrap = new Button { Text = $"Disassemble +{WorkshopService.ScrapValue(drop.Rarity)}", CustomMinimumSize = new Vector2(130, 36) };
                StyleSmall(scrap);
                scrap.Pressed += () =>
                {
                    if (WorkshopService.Disassemble(_inventory, moduleIndex))
                    {
                        _status = $"Scrapped, recovered {WorkshopService.ScrapValue(drop.Rarity)} Alloy";
                        _onChanged();
                        Rebuild();
                    }
                    else
                    {
                        _status = "Disassemble failed";
                        Rebuild();
                    }
                };
                row.AddChild(scrap);
            }

            if (WorkshopService.CanReroll(drop))
            {
                var reroll = new Button { Text = $"Reroll {drop.RerollCost}", CustomMinimumSize = new Vector2(110, 36) };
                StyleSmall(reroll);
                reroll.Pressed += () =>
                {
                    if (WorkshopService.Reroll(_inventory, moduleIndex, _rng))
                    {
                        _status = $"Rerolled (next cost {drop.RerollCost}) — affixes re-rolled";
                        _onChanged();
                        Rebuild();
                    }
                    else
                    {
                        _status = "Reroll failed: Ancient cannot be rerolled or not enough Alloy";
                        Rebuild();
                    }
                };
                row.AddChild(reroll);
            }
            list.AddChild(ModuleCard(row, drop.Rarity));
        }
        box.AddChild(list);
        return MakeScroll(box);
    }

    // ---------- repair页 ----------

    private Control BuildRepairTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 10);
        ShipFittingService.ApplyToShip(_ship, _slots);

        var info = new Label
        {
            Text = $"Current Hull: {_ship.Hull} / {_ship.MaxHull}　(Shield {_ship.Shield}/{_ship.MaxShield}, shield resets each sortie, only Hull is repaired)",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        info.AddThemeFontSizeOverride("font_size", 18);
        info.AddThemeColorOverride("font_color", new Color(TextColor));
        box.AddChild(info);

        int cost = WorkshopService.RepairCost(_ship);
        var repair = new Button
        {
            Text = cost == 0 ? "Hull intact, no repair needed" : $"Repair to full — costs {cost} Alloy",
            CustomMinimumSize = new Vector2(380, 48),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        repair.AddThemeFontSizeOverride("font_size", 18);
        repair.AddThemeColorOverride("font_color", new Color("10131f"));
        repair.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("6ee06e"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        repair.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("6ee06e").Lightened(0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        repair.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("6ee06e").Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        repair.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        repair.Disabled = cost == 0;
        repair.Pressed += () =>
        {
            if (WorkshopService.Repair(_ship, _inventory))
            {
                _status = "Repair complete, hull restored";
                _onChanged();
                Rebuild();
            }
            else
            {
                _status = "Repair failed: not enough Alloy";
                Rebuild();
            }
        };
        box.AddChild(repair);
        box.AddChild(InfoLabel("Repair pricing: by damage ratio (1 missing Hull = 0.1 Alloy, min 1)"));
        return MakeScroll(box);
    }

    // ---------- shop页 ----------

    private Control BuildShopTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        var hint = new Label
        {
            Text = "Supply Shop — priced in Alloy, sells Common/Magic modules only (no higher tiers)",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        box.AddChild(hint);

        var shop = GetShop();
        shop.Refresh(_rng);
        var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < shop.Items.Count; i++)
        {
            int itemIndex = i;
            var item = shop.Items[i];
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(IconBox(item.Module.Slot, item.Module.Rarity));
            var info = new Label
            {
                Text = $"{item.Module.DisplayName}　{item.Module.AffixSummary().Replace("\n", " ｜ ")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 14);
            info.AddThemeColorOverride("font_color", RarityColor(item.Module.Rarity));
            var buy = new Button { Text = $"Buy {item.Price} Alloy", CustomMinimumSize = new Vector2(140, 36) };
            StyleSmall(buy);
            buy.Pressed += () =>
            {
                if (shop.TryBuy(_inventory, itemIndex))
                {
                    _status = $"Purchased {item.Module.DisplayName}";
                    _onChanged();
                    Rebuild();
                }
                else
                {
                    _status = "Not enough Alloy";
                    Rebuild();
                }
            };
            row.AddChild(info);
            row.AddChild(buy);
            list.AddChild(ModuleCard(row, item.Module.Rarity));
        }
        box.AddChild(list);
        return MakeScroll(box);
    }

    private ShopCatalog? _shop;

    private ShopCatalog GetShop()
    {
        _shop ??= new ShopCatalog();
        return _shop;
    }

    // ---------- 样式辅助 ----------

    private static void StyleSmall(Button btn)
    {
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", new Color(TextColor));
        btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("1b2233"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static Label InfoLabel(string text)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(SubColor));
        return label;
    }

    /// <summary>Sprint 5 iteration 17 C2：attribute三栏column（title + 若干"tab 值"row，深色底 + 细边框）。</summary>
    private static PanelContainer StatColumn(string title, IReadOnlyList<string> rows)
    {
        var inner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inner.AddThemeConstantOverride("separation", 2);
        var head = new Label
        {
            Text = title,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        head.AddThemeFontSizeOverride("font_size", 13);
        head.AddThemeColorOverride("font_color", new Color(TitleColor));
        inner.AddChild(head);
        foreach (var r in rows)
        {
            var line = new Label
            {
                Text = "  " + r,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            line.AddThemeFontSizeOverride("font_size", 13);
            line.AddThemeColorOverride("font_color", new Color(TextColor));
            inner.AddChild(line);
        }
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151a28"),
            BorderColor = new Color("2a3550"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8
        });
        panel.AddChild(inner);
        return panel;
    }

    /// <summary>Sprint 5 iteration 17 C1：list项卡片化（深底 + rarity color细边框 + content留白）。</summary>
    private static PanelContainer ModuleCard(Control row, ItemRarity rarity)
    {
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151a28"),
            BorderColor = RarityColor(rarity),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6
        });
        panel.AddChild(row);
        return panel;
    }

    /// <summary>Sprint 5 P0-A2：slot 16×16 像素图标path（program化生成，LD 命名map §5）。</summary>
    private static string SlotIconPath(ModuleType slot) => slot switch
    {
        ModuleType.Weapon => "res://assets/icons/weapon.png",
        ModuleType.Armor => "res://assets/icons/armor.png",
        ModuleType.Power => "res://assets/icons/power.png",
        _ => "res://assets/icons/special.png"
    };

    private static readonly Dictionary<int, Texture2D> IconCache = new();

    /// <summary>slot图标 + rarity color边框（20×20：16 图标居medium + 2px rarity边框，LD §5 图标spec）。</summary>
    private static Texture2D ModuleIcon(ModuleType slot, ItemRarity rarity)
    {
        int key = (int)slot * 10 + (int)rarity;
        if (IconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var src = GD.Load<Texture2D>(SlotIconPath(slot)).GetImage();
        var img = Image.CreateEmpty(20, 20, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                img.SetPixel(x + 2, y + 2, src.GetPixel(x, y));
            }
        }
        var border = RarityColor(rarity);
        for (int x = 0; x < 20; x++)
        {
            img.SetPixel(x, 0, border);
            img.SetPixel(x, 19, border);
        }
        for (int y = 0; y < 20; y++)
        {
            img.SetPixel(0, y, border);
            img.SetPixel(19, y, border);
        }
        var tex = ImageTexture.CreateFromImage(img);
        IconCache[key] = tex;
        return tex;
    }

    /// <summary>list项左侧slot图标（rarity color边框），20×20 fixed尺寸。</summary>
    private static TextureRect IconBox(ModuleType slot, ItemRarity rarity) => new()
    {
        Texture = ModuleIcon(slot, rarity),
        CustomMinimumSize = new Vector2(20, 20),
        // 垂直direction不随 HBox rowhigh拉伸（同rowbutton 34-44px high会把图标拉变形），保持 20×20 居medium
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => new Color("c8c8c8"),
        ItemRarity.Magic => new Color("4da6ff"),
        ItemRarity.Rare => new Color("ffd166"),
        ItemRarity.Set => new Color("6ee06e"),
        ItemRarity.Ancient => new Color("ff7ad9"),
        _ => new Color("ffffff")
    };
}
