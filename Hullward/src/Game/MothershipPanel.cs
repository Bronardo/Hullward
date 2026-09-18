using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// 母舰内部面板（LD UI 规格 v0.2 §6 + 空间站清单 §4 + 船坞）：
/// 船坞（四档旗舰选择/解锁）· 装配（槽位装卸/2 套方案/属性预览）· 仓库（品质筛选）· 工坊（拆解/洗练）· 维修 · 商店。
/// 代码构建；操作即时修改域状态并重建内容区。
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
        _onChanged = onChanged;
        _shipClass = shipClass;
        _onShipChange = onShipChange;

        // 关键：面板挂到 CanvasLayer（非 Control 父），锚点相对视口。
        // 必须在进树（_Ready）之前设好 FullRect——与 UiScreens.Fullscreen() 的
        // "构造时 SetAnchorsPreset" 模式一致；若拖到 _Ready 才设，进树首帧布局
        // 已按默认锚点算过 rect（0x0），内容塌缩到左上角 min size 区。
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        // 面板自身满屏（与 UiScreens.Fullscreen 一致）：挂到 CanvasLayer 时若无锚点，
        // rect 保持 0x0，FullRect 子节点随之塌缩 → 内容区空白（LD B5 复现修复）。
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ShipFittingService.ApplyToShip(_ship, _slots); // 进入母舰即同步属性（维修/预览口径一致）
        Rebuild();
    }

    // ---------- 构建 ----------

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

        // Sprint 5 P0-B：母舰深色金属科技网格底纹（LD §6 色板 #16121F 系），平铺于六页
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
            Text = $"⚙ 母舰内部 — Lv.{_mothershipLevel}（经验 {_mothershipExp}）　船体：{_ship.Name}　合金：{_inventory.Alloy}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(TitleColor));
        header.AddChild(title);

        var close = new Button { Text = "返回星图", CustomMinimumSize = new Vector2(140, 40) };
        close.AddThemeFontSizeOverride("font_size", 16);
        close.AddThemeColorOverride("font_color", new Color("10131f"));
        close.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("6ee06e"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("6ee06e").Lightened(0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("6ee06e").Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        close.Pressed += _onClose;
        header.AddChild(close);
        return header;
    }

    private Control BuildTabs()
    {
        string[] names = { "船坞", "装配", "仓库", "工坊", "维修", "商店" };
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

    // ---------- 船坞页（舰船选择） ----------

    private Control BuildDockTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        var hint = new Label
        {
            Text = "船坞 —— 旗舰四档船体随母舰等级解锁；切换后装配槽位自动重排，超出模块退回仓库",
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
                       (current ? "\n◆ 当前旗舰" : "") +
                       (unlocked ? "" : $"\n· 母舰 Lv.{(int)shipClass} 解锁"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 16);
            info.AddThemeColorOverride("font_color", current ? new Color(TitleColor) : new Color(TextColor));

            var stats = new Label
            {
                Text = $"耐久 {preview.MaxHull}｜护盾 {preview.MaxShield}｜火力 {preview.Firepower:0}｜装甲 {preview.Armor}｜速度 {preview.Speed:0}｜槽位 {preview.ModuleSlots}",
                HorizontalAlignment = HorizontalAlignment.Right,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            stats.AddThemeFontSizeOverride("font_size", 14);
            stats.AddThemeColorOverride("font_color", new Color(SubColor));

            var switchBtn = new Button
            {
                Text = current ? "已启用" : unlocked ? "切换到此舰" : "未解锁",
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
                    _status = $"已切换旗舰：{ShipCatalog.DisplayName(shipClass)}（装配槽位 {_slots.Count}）";
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

    // ---------- 装配页 ----------

    private Control BuildEquipTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        // 属性预览（含词缀加成属性，LD Sprint 3 §4.6 B3：词缀明细可见）
        ShipFittingService.ApplyToShip(_ship, _slots);
        var affixStats = new List<string> { $"火力 {_ship.Firepower:0}", $"护盾 {_ship.Shield}/{_ship.MaxShield}", $"耐久 {_ship.Hull}/{_ship.MaxHull}", $"攻速 ×{_ship.FireRateMultiplier:0.00}", $"抗性 {_ship.Armor}" };
        if (_ship.MagicFind > 0)
        {
            affixStats.Add($"MF {_ship.MagicFind}");
        }
        if (_ship.CritChance > 0f)
        {
            affixStats.Add($"暴击 {_ship.CritChance * 100f:0}%");
        }
        if (_ship.CritDamage > 2f)
        {
            affixStats.Add($"暴伤 ×{_ship.CritDamage:0.00}");
        }
        if (_ship.DamageReductionPct > 0f)
        {
            affixStats.Add($"减伤 {_ship.DamageReductionPct * 100f:0}%");
        }
        if (_ship.ThornsPct > 0f)
        {
            affixStats.Add($"反伤 {_ship.ThornsPct * 100f:0}%");
        }
        var stats = new Label
        {
            Text = "属性预览（基础 + 词缀 = 最终）｜ " + string.Join(" ｜ ", affixStats),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stats.AddThemeFontSizeOverride("font_size", 16);
        stats.AddThemeColorOverride("font_color", new Color(TitleColor));
        box.AddChild(stats);

        // 槽位列表（点击选中/卸下；显示词缀数，LD §4.6 B3）
        var slotRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        slotRow.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < _slots.Count; i++)
        {
            int slotIndex = i;
            ModuleDrop? drop = _slots[i];
            string label = drop == null
                ? $"[{i + 1}] 空槽"
                : $"[{i + 1}] {drop.DisplayName}" + (drop.Affixes.Count > 0 ? $"（{drop.Affixes.Count}词缀）" : "");
            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(230, 44),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Icon = drop == null ? null : ModuleIcon(drop.Slot, drop.Rarity),
                IconAlignment = HorizontalAlignment.Left
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
            slotRow.AddChild(btn);
        }

        // 卸下选中槽
        var unequip = new Button { Text = "卸下选中槽", CustomMinimumSize = new Vector2(140, 44) };
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
                _status = "已卸下模块回仓库";
                _onChanged();
                Rebuild();
            }
            else
            {
                _status = "请先选中一个已装配的槽位";
                Rebuild();
            }
        };
        slotRow.AddChild(unequip);

        // 配装方案
        var presetRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        presetRow.AddThemeConstantOverride("separation", 8);
        var saveA = new Button { Text = _presets.IsSaved('A') ? $"保存方案A（{_presets.NameOf('A')}）" : "保存为方案A", CustomMinimumSize = new Vector2(200, 40) };
        var applyA = new Button { Text = "应用方案A", CustomMinimumSize = new Vector2(130, 40) };
        var saveB = new Button { Text = _presets.IsSaved('B') ? $"保存方案B（{_presets.NameOf('B')}）" : "保存为方案B", CustomMinimumSize = new Vector2(200, 40) };
        var applyB = new Button { Text = "应用方案B", CustomMinimumSize = new Vector2(130, 40) };
        StyleSmall(saveA); StyleSmall(saveB); StyleSmall(applyA); StyleSmall(applyB);
        saveA.Pressed += () => { _presets.Save('A', "方案A", _slots); _status = "已保存方案A（当前装配快照）"; Rebuild(); };
        applyA.Pressed += () => ApplyPreset('A');
        saveB.Pressed += () => { _presets.Save('B', "方案B", _slots); _status = "已保存方案B（当前装配快照）"; Rebuild(); };
        applyB.Pressed += () => ApplyPreset('B');
        presetRow.AddChild(saveA); presetRow.AddChild(applyA);
        presetRow.AddChild(saveB); presetRow.AddChild(applyB);
        box.AddChild(slotRow);
        box.AddChild(presetRow);

        // 选中槽位词缀明细（LD §4.6 B3：装配界面可见词缀名称+数值）
        if (_selectedSlot >= 0 && _slots[_selectedSlot] != null)
        {
            ModuleDrop selected = _slots[_selectedSlot]!;
            var detail = new Label
            {
                Text = $"◆ 槽位 {_selectedSlot + 1}：{selected.DisplayName}\n{selected.AffixSummary()}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            detail.AddThemeFontSizeOverride("font_size", 14);
            detail.AddThemeColorOverride("font_color", RarityColor(selected.Rarity));
            box.AddChild(detail);
        }

        box.AddChild(new Label { Text = "点击槽位选中 → 在下方背包模块列表点击模块装入该槽（未选中则装入首个空槽）", HorizontalAlignment = HorizontalAlignment.Left, MouseFilter = Control.MouseFilterEnum.Ignore });

        // 背包模块列表（点击装入）
        var bagBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bagBox.AddThemeConstantOverride("separation", 6);
        if (_inventory.Modules.Count == 0)
        {
            bagBox.AddChild(InfoLabel("仓库空空如也 —— 出击拾取模块后返回这里装配"));
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
            var equip = new Button { Text = "装入", CustomMinimumSize = new Vector2(70, 34) };
            StyleSmall(equip);
            equip.Pressed += () =>
            {
                int target = _selectedSlot >= 0 ? _selectedSlot : FirstEmptySlot();
                if (target >= 0 && ShipFittingService.TryEquip(_inventory, _slots, moduleIndex, target))
                {
                    _status = $"已装入槽位 {target + 1}";
                    _selectedSlot = -1;
                    _onChanged();
                    Rebuild();
                }
                else
                {
                    _status = "槽位已满或无可用空槽";
                    Rebuild();
                }
            };
            row.AddChild(info);
            row.AddChild(equip);
            bagBox.AddChild(row);
        }
        box.AddChild(bagBox);

        return MakeScroll(box);
    }

    private void ApplyPreset(char id)
    {
        if (_presets.TryApply(id, _inventory, _slots))
        {
            _status = $"已应用方案{id}";
            _onChanged();
            Rebuild();
        }
        else
        {
            _status = $"方案{id}未保存或模块缺失";
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

    // ---------- 仓库页 ----------

    private Control BuildStorageTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        var filterRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        filterRow.AddThemeConstantOverride("separation", 6);
        string[] options = { "全部", "白", "蓝", "黄", "绿", "太古" };
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
            Text = $"仓库模块 {_inventory.Modules.Count} 件｜合金 {_inventory.Alloy}",
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
            list.AddChild(InfoLabel("没有符合筛选的模块"));
        }
        foreach (var entry in filtered)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(IconBox(entry.M.Slot, entry.M.Rarity));
            var info = new Label
            {
                Text = $"{entry.M.DisplayName}　{entry.M.AffixSummary().Replace("\n", " ｜ ")}" +
                       (entry.M.Rarity == ItemRarity.Rare || entry.M.Rarity == ItemRarity.Set ? $"　（洗练费用 {entry.M.RerollCost}）" : ""),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 14);
            info.AddThemeColorOverride("font_color", RarityColor(entry.M.Rarity));
            row.AddChild(info);
            list.AddChild(row);
        }
        box.AddChild(list);
        return MakeScroll(box);
    }

    // ---------- 工坊页 ----------

    private Control BuildWorkshopTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        var hint = new Label
        {
            Text = "拆解：白1/蓝3/黄8/绿15 合金（太古不可拆）｜洗练：黄+ 重 roll 词缀，费用递增 5/10/20/40…",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        box.AddChild(hint);

        var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 6);
        if (_inventory.Modules.Count == 0)
        {
            list.AddChild(InfoLabel("仓库空空如也"));
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
                var scrap = new Button { Text = $"拆解 +{WorkshopService.ScrapValue(drop.Rarity)}", CustomMinimumSize = new Vector2(130, 36) };
                StyleSmall(scrap);
                scrap.Pressed += () =>
                {
                    if (WorkshopService.Disassemble(_inventory, moduleIndex))
                    {
                        _status = $"已拆解，回收 {WorkshopService.ScrapValue(drop.Rarity)} 合金";
                        _onChanged();
                        Rebuild();
                    }
                    else
                    {
                        _status = "拆解失败";
                        Rebuild();
                    }
                };
                row.AddChild(scrap);
            }

            if (WorkshopService.CanReroll(drop))
            {
                var reroll = new Button { Text = $"洗练 {drop.RerollCost}", CustomMinimumSize = new Vector2(110, 36) };
                StyleSmall(reroll);
                reroll.Pressed += () =>
                {
                    if (WorkshopService.Reroll(_inventory, moduleIndex, _rng))
                    {
                        _status = $"已洗练（下次费用 {drop.RerollCost}）—— 词缀已重 roll";
                        _onChanged();
                        Rebuild();
                    }
                    else
                    {
                        _status = "洗练失败：太古不可洗 或 合金不足";
                        Rebuild();
                    }
                };
                row.AddChild(reroll);
            }
            list.AddChild(row);
        }
        box.AddChild(list);
        return MakeScroll(box);
    }

    // ---------- 维修页 ----------

    private Control BuildRepairTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 10);
        ShipFittingService.ApplyToShip(_ship, _slots);

        var info = new Label
        {
            Text = $"当前耐久：{_ship.Hull} / {_ship.MaxHull}　（护盾 {_ship.Shield}/{_ship.MaxShield}，护盾随出战重置，仅维修耐久）",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        info.AddThemeFontSizeOverride("font_size", 18);
        info.AddThemeColorOverride("font_color", new Color(TextColor));
        box.AddChild(info);

        int cost = WorkshopService.RepairCost(_ship);
        var repair = new Button
        {
            Text = cost == 0 ? "船体完好，无需维修" : $"维修至满耐久 —— 消耗 {cost} 合金",
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
                _status = "维修完成，船体已恢复";
                _onChanged();
                Rebuild();
            }
            else
            {
                _status = "维修失败：合金不足";
                Rebuild();
            }
        };
        box.AddChild(repair);
        box.AddChild(InfoLabel("维修计价：按受损比例（缺失 1 点耐久 = 0.1 合金，最低 1）"));
        return MakeScroll(box);
    }

    // ---------- 商店页 ----------

    private Control BuildShopTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        var hint = new Label
        {
            Text = "补给商店 —— 合金计价，只售白/蓝模块（不卖高阶）",
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
            var buy = new Button { Text = $"购买 {item.Price} 合金", CustomMinimumSize = new Vector2(140, 36) };
            StyleSmall(buy);
            buy.Pressed += () =>
            {
                if (shop.TryBuy(_inventory, itemIndex))
                {
                    _status = $"已购得 {item.Module.DisplayName}";
                    _onChanged();
                    Rebuild();
                }
                else
                {
                    _status = "合金不足";
                    Rebuild();
                }
            };
            row.AddChild(info);
            row.AddChild(buy);
            list.AddChild(row);
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

    /// <summary>Sprint 5 P0-A2：槽位 16×16 像素图标路径（程序化生成，LD 命名映射 §5）。</summary>
    private static string SlotIconPath(ModuleType slot) => slot switch
    {
        ModuleType.Weapon => "res://assets/icons/weapon.png",
        ModuleType.Armor => "res://assets/icons/armor.png",
        ModuleType.Power => "res://assets/icons/power.png",
        _ => "res://assets/icons/special.png"
    };

    private static readonly Dictionary<int, Texture2D> IconCache = new();

    /// <summary>槽位图标 + 品质色边框（20×20：16 图标居中 + 2px 品质边框，LD §5 图标规格）。</summary>
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

    /// <summary>列表项左侧槽位图标（品质色边框），20×20 固定尺寸。</summary>
    private static TextureRect IconBox(ModuleType slot, ItemRarity rarity) => new()
    {
        Texture = ModuleIcon(slot, rarity),
        CustomMinimumSize = new Vector2(20, 20),
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
