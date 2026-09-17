using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Game;

/// <summary>
/// 母舰内部面板（LD UI 规格 v0.2 §6 + 空间站清单 §4）：
/// 装配（槽位装卸/2 套方案/属性预览）· 仓库（品质筛选）· 工坊（拆解/洗练）· 维修 · 商店。
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
    private readonly ShipBase _ship;
    private readonly int _mothershipLevel;
    private readonly int _mothershipExp;
    private readonly ShipPresets _presets;
    private readonly Random _rng;
    private readonly Action _onClose;
    private readonly Action _onChanged;

    private int _tab;                 // 0 装配 / 1 仓库 / 2 工坊 / 3 维修 / 4 商店
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
        Action onChanged)
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
    }

    public override void _Ready()
    {
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
            CustomMinimumSize = new Vector2(0, 0)
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
        string[] names = { "装配", "仓库", "工坊", "维修", "商店" };
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
        1 => BuildStorageTab(),
        2 => BuildWorkshopTab(),
        3 => BuildRepairTab(),
        4 => BuildShopTab(),
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

    // ---------- 装配页 ----------

    private Control BuildEquipTab()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);

        // 属性预览
        ShipFittingService.ApplyToShip(_ship, _slots);
        var stats = new Label
        {
            Text = $"属性预览 — 火力 {_ship.Firepower:0}｜护盾 {_ship.Shield}/{_ship.MaxShield}｜耐久 {_ship.Hull}/{_ship.MaxHull}" +
                   $"｜攻速 ×{_ship.FireRateMultiplier:0.00}｜抗性 {_ship.Armor}｜MF {_ship.MagicFind}",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stats.AddThemeFontSizeOverride("font_size", 16);
        stats.AddThemeColorOverride("font_color", new Color(TitleColor));
        box.AddChild(stats);

        // 槽位列表（点击选中/卸下）
        var slotRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        slotRow.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < _slots.Count; i++)
        {
            int slotIndex = i;
            ModuleDrop? drop = _slots[i];
            string label = drop == null ? $"[{i + 1}] 空槽" : $"[{i + 1}] {drop.Name}";
            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(190, 44),
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
            var info = new Label
            {
                Text = $"▸ {drop.Name}  {drop.AffixSummary().Replace("\n", " ｜ ")}",
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
            var row = new Label
            {
                Text = $"▸ {entry.M.Name}　{entry.M.AffixSummary().Replace("\n", " ｜ ")}" +
                       (entry.M.Rarity == ItemRarity.Rare || entry.M.Rarity == ItemRarity.Set ? $"　（洗练费用 {entry.M.RerollCost}）" : ""),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddThemeFontSizeOverride("font_size", 14);
            row.AddThemeColorOverride("font_color", RarityColor(entry.M.Rarity));
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
            var info = new Label
            {
                Text = $"▸ {drop.Name}　{drop.AffixSummary().Replace("\n", " ｜ ")}",
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
            var info = new Label
            {
                Text = $"▸ {item.Module.Name}　{item.Module.AffixSummary().Replace("\n", " ｜ ")}",
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
                    _status = $"已购得 {item.Module.Name}";
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
