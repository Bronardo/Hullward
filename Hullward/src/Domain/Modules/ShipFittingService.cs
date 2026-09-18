using System.Collections.Generic;
using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Modules;

/// <summary>
/// 手动装配服务（LD 空间站清单 §4）：
/// 槽位可视化 + 模块装卸；装配状态 = 槽位列表（长度 = 船体槽数，元素可为 null）。
/// 域层不直接持有船体，装卸只移动背包与槽位之间的 ModuleDrop 引用。
/// </summary>
public static class ShipFittingService
{
    /// <summary>创建空槽位列表（长度 = 船体槽数）。</summary>
    public static List<ModuleDrop?> EmptySlots(int slotCount)
    {
        var slots = new List<ModuleDrop?>();
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(null);
        }
        return slots;
    }

    /// <summary>把背包第 moduleIndex 个模块装入 slotIndex 槽（原槽模块回背包）。失败返回 false。</summary>
    public static bool TryEquip(Inventory inventory, List<ModuleDrop?> slots, int moduleIndex, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return false;
        }
        if (!inventory.TryRemoveModule(moduleIndex, out ModuleDrop? drop) || drop == null)
        {
            return false;
        }
        if (slots[slotIndex] != null)
        {
            inventory.AddModule(slots[slotIndex]!);
        }
        slots[slotIndex] = drop;
        return true;
    }

    /// <summary>卸下 slotIndex 槽模块回背包。失败返回 false。</summary>
    public static bool TryUnequip(Inventory inventory, List<ModuleDrop?> slots, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
        {
            return false;
        }
        inventory.AddModule(slots[slotIndex]!);
        slots[slotIndex] = null;
        return true;
    }

    /// <summary>清空全部槽位回背包。</summary>
    public static void UnequipAll(Inventory inventory, List<ModuleDrop?> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                inventory.AddModule(slots[i]!);
                slots[i] = null;
            }
        }
    }

    /// <summary>把槽位状态应用到船体（清空重装，属性即时重算）。</summary>
    public static void ApplyToShip(ShipBase ship, List<ModuleDrop?> slots)
    {
        ship.Modules.Clear();
        ship.ResetCombatState();
        foreach (var drop in slots)
        {
            if (drop != null && ship.Modules.Count < ship.ModuleSlots)
            {
                ship.EquipModule(ShipFitting.CreateModule(drop));
            }
        }
    }

    /// <summary>已装配模块数。</summary>
    public static int FilledCount(List<ModuleDrop?> slots) => slots.Count(s => s != null);

    /// <summary>
    /// 换船时槽位重排（船坞切换）：新槽列表长度 = newSlotCount；
    /// 前 newSlotCount 个旧槽保留，超出的已装模块退回背包，不足的补空槽。
    /// </summary>
    public static List<ModuleDrop?> RebaseSlots(Inventory inventory, List<ModuleDrop?> oldSlots, int newSlotCount)
    {
        var slots = EmptySlots(newSlotCount);
        for (int i = 0; i < oldSlots.Count && i < newSlotCount; i++)
        {
            slots[i] = oldSlots[i];
        }
        for (int i = newSlotCount; i < oldSlots.Count; i++)
        {
            if (oldSlots[i] != null)
            {
                inventory.AddModule(oldSlots[i]!);
            }
        }
        return slots;
    }
}

/// <summary>
/// 自动装配（保留迭代 3 行为 + 槽位化）：把背包最高品质模块逐个装入空槽（装配即消耗）。
/// </summary>
public static class AutoFit
{
    /// <summary>自动装入空槽，返回新装入数量。</summary>
    public static int AutoEquipIntoSlots(Inventory inventory, List<ModuleDrop?> slots)
    {
        int equipped = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                continue;
            }
            int bestIndex = -1;
            ItemRarity bestRarity = ItemRarity.Common;
            for (int j = 0; j < inventory.Modules.Count; j++)
            {
                // bestIndex < 0：首个模块兜底选中，避免"只有 Common 时永远不装"的边界 bug
                if (bestIndex < 0 || inventory.Modules[j].Rarity > bestRarity)
                {
                    bestRarity = inventory.Modules[j].Rarity;
                    bestIndex = j;
                }
            }
            if (bestIndex < 0)
            {
                break;
            }
            if (inventory.TryRemoveModule(bestIndex, out ModuleDrop? drop) && drop != null)
            {
                slots[i] = drop;
                equipped++;
            }
        }
        return equipped;
    }
}

/// <summary>
/// 配装方案（LD 空间站清单 §4：保存 2 套）：
/// 方案 = 槽位快照（模块引用）；应用时先卸空当前装配回背包，再从背包按引用取回装入。
/// 方案保存在会话内存（存档序列化 v2.0 接入）。
/// </summary>
public sealed class ShipPresets
{
    public string? PresetAName { get; private set; }
    public string? PresetBName { get; private set; }

    private List<ModuleDrop?>? _presetA;
    private List<ModuleDrop?>? _presetB;

    public bool IsSaved(char id) => id == 'A' ? _presetA != null : _presetB != null;

    public string? NameOf(char id) => id == 'A' ? PresetAName : PresetBName;

    public void Save(char id, string name, List<ModuleDrop?> slots)
    {
        var copy = new List<ModuleDrop?>(slots);
        if (id == 'A')
        {
            _presetA = copy;
            PresetAName = name;
        }
        else
        {
            _presetB = copy;
            PresetBName = name;
        }
    }

    /// <summary>应用方案：卸空当前槽位 → 按引用从背包取回装入。失败（未保存）返回 false。</summary>
    public bool TryApply(char id, Inventory inventory, List<ModuleDrop?> slots)
    {
        var preset = id == 'A' ? _presetA : _presetB;
        if (preset == null)
        {
            return false;
        }
        ShipFittingService.UnequipAll(inventory, slots);
        for (int i = 0; i < preset.Count && i < slots.Count; i++)
        {
            ModuleDrop? m = preset[i];
            if (m == null)
            {
                continue;
            }
            int idx = inventory.Modules.IndexOf(m);
            if (idx >= 0 && inventory.TryRemoveModule(idx, out ModuleDrop? removed) && removed != null)
            {
                slots[i] = removed;
            }
        }
        return true;
    }
}
