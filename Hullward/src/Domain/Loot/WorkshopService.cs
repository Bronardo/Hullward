using System;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Loot;

/// <summary>
/// 空间站工坊（LD 空间站清单 §4）：
/// 拆解（白1/蓝3/黄8/绿15/暗金不可拆）、洗练（黄+ 重 roll 词缀，费用 5×2^n 递增）、维修（按受损比例计合金）。
/// </summary>
public static class WorkshopService
{
    /// <summary>拆解回收合金（LD §4：白1/蓝3/黄8/绿15/暗金不可拆）。</summary>
    public static int ScrapValue(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Magic => 3,
        ItemRarity.Rare => 8,
        ItemRarity.Set => 15,
        _ => 0 // 太古不可拆
    };

    public static bool CanDisassemble(ItemRarity rarity) => rarity != ItemRarity.Ancient;

    /// <summary>拆解背包第 index 个模块：移除并回收合金。失败返回 false。</summary>
    public static bool Disassemble(Inventory inventory, int index)
    {
        if (index < 0 || index >= inventory.Modules.Count)
        {
            return false;
        }
        if (!CanDisassemble(inventory.Modules[index].Rarity))
        {
            return false; // 太古不可拆
        }
        if (!inventory.TryRemoveModule(index, out ModuleDrop? drop) || drop == null)
        {
            return false;
        }
        inventory.AddAlloy(ScrapValue(drop.Rarity));
        return true;
    }

    /// <summary>是否可洗练：黄/绿（太古不可洗，LD §3.3）。</summary>
    public static bool CanReroll(ModuleDrop drop) => drop.Rarity is ItemRarity.Rare or ItemRarity.Set;

    /// <summary>洗练：扣费（费用递增）并重 roll 词缀。失败返回 false（不可洗/合金不足）。</summary>
    public static bool Reroll(Inventory inventory, int index, Random rng)
    {
        if (index < 0 || index >= inventory.Modules.Count)
        {
            return false;
        }
        ModuleDrop drop = inventory.Modules[index];
        if (!CanReroll(drop))
        {
            return false;
        }
        if (inventory.Alloy < drop.RerollCost)
        {
            return false;
        }
        inventory.SpendAlloy(drop.RerollCost);
        ModuleRoller.RerollAffixes(drop, rng);
        return true;
    }

    /// <summary>维修费用：按受损比例计（缺失 1 点耐久 = 0.1 合金，向上取整，最低 1）。</summary>
    public static int RepairCost(ShipBase ship)
    {
        int missing = ship.MaxHull - ship.Hull;
        if (missing <= 0)
        {
            return 0;
        }
        return Math.Max(1, (int)Math.Ceiling(missing * 0.1f));
    }

    /// <summary>维修：扣合金恢复耐久至满。失败返回 false（无损伤/合金不足）。</summary>
    public static bool Repair(ShipBase ship, Inventory inventory)
    {
        int cost = RepairCost(ship);
        if (cost == 0 || inventory.Alloy < cost)
        {
            return false;
        }
        inventory.SpendAlloy(cost);
        ship.Hull = ship.MaxHull;
        return true;
    }
}
