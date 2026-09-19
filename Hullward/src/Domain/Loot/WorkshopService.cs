using System;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Loot;

/// <summary>
/// 空间站workshop（LD 空间站清单 §4）：
/// disassemble（白1/蓝3/黄8/绿15/暗金不可拆）、reroll（黄+ 重 roll affix，费用 5×2^n 递增）、repair（按受损比例计alloy）。
/// </summary>
public static class WorkshopService
{
    /// <summary>disassemblesalvagealloy（LD §4：白1/蓝3/黄8/绿15/暗金不可拆）。</summary>
    public static int ScrapValue(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Magic => 3,
        ItemRarity.Rare => 8,
        ItemRarity.Set => 15,
        _ => 0 // 太古不可拆
    };

    public static bool CanDisassemble(ItemRarity rarity) => rarity != ItemRarity.Ancient;

    /// <summary>disassembleinventory第 index 个module：remove并salvagealloy。failback false。</summary>
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

    /// <summary>是否可reroll：黄/绿（太古不可洗，LD §3.3）。</summary>
    public static bool CanReroll(ModuleDrop drop) => drop.Rarity is ItemRarity.Rare or ItemRarity.Set;

    /// <summary>reroll：扣费（费用递增）并重 roll affix。failback false（不可洗/alloy不足）。</summary>
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

    /// <summary>repair费用：按受损比例计（缺失 1 点hull = 0.1 alloy，向上取整，最low 1）。</summary>
    public static int RepairCost(ShipBase ship)
    {
        int missing = ship.MaxHull - ship.Hull;
        if (missing <= 0)
        {
            return 0;
        }
        return Math.Max(1, (int)Math.Ceiling(missing * 0.1f));
    }

    /// <summary>repair：扣alloyrecoveryhull至满。failback false（无损伤/alloy不足）。</summary>
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
