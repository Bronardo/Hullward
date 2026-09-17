using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Modules;

/// <summary>
/// 舰船装配（纯 C# 域层）：
/// 将背包模块按品质降序自动装配到船体空闲槽位，模块数值随品质递增。
/// </summary>
public static class ShipFitting
{
    /// <summary>品质 → 火力加成（武器/特种）。</summary>
    public static float FirepowerBonus(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 2f,
        ItemRarity.Magic => 5f,
        ItemRarity.Rare => 10f,
        ItemRarity.Set => 18f,
        ItemRarity.Ancient => 30f,
        _ => 2f
    };

    /// <summary>品质 → 护盾加成（装甲）。</summary>
    public static int ShieldBonus(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 10,
        ItemRarity.Magic => 25,
        ItemRarity.Rare => 50,
        ItemRarity.Set => 90,
        ItemRarity.Ancient => 150,
        _ => 10
    };

    /// <summary>把背包最高品质模块逐个装入空闲槽位（装配即消耗）。</summary>
    public static void AutoEquipBest(ShipBase ship, Inventory inventory)
    {
        while (ship.Modules.Count < ship.ModuleSlots)
        {
            int bestIndex = -1;
            ItemRarity bestRarity = ItemRarity.Common;

            for (int i = 0; i < inventory.Modules.Count; i++)
            {
                // bestIndex < 0：首个模块兜底选中，避免"只有 Common 时永远不装"的边界 bug
                if (bestIndex < 0 || inventory.Modules[i].Rarity > bestRarity)
                {
                    bestRarity = inventory.Modules[i].Rarity;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return; // 背包为空
            }

            if (inventory.TryRemoveModule(bestIndex, out ModuleDrop? drop) && drop != null)
            {
                ship.EquipModule(CreateModule(drop));
            }
        }
    }

    /// <summary>模块掉落 → 具体模块实现（数值随品质；词缀按 LD §3 并入加成）。</summary>
    public static IShipModule CreateModule(ModuleDrop drop)
    {
        float fp = FirepowerBonus(drop.Rarity);
        float fr = 0f;
        int shield = ShieldBonus(drop.Rarity);
        int armor = ShieldBonus(drop.Rarity) / 5;
        int hull = 0;
        int magicFind = 0;
        float critChance = 0f;
        float critDamage = 0f;
        float damageReduction = 0f;
        float thorns = 0f;

        foreach (var affix in drop.Affixes)
        {
            switch (affix.Stat)
            {
                case AffixStat.FirepowerPercent:
                    fp *= 1f + affix.Value / 100f;
                    break;
                case AffixStat.AttackSpeedPercent:
                    fr += affix.Value / 100f;
                    break;
                case AffixStat.MaxShieldPercent:
                    shield += (int)(shield * affix.Value / 100f);
                    break;
                case AffixStat.MaxHullPercent:
                    hull += (int)(shield * affix.Value / 100f); // 以品质护盾为基数（口径见设计文档）
                    break;
                case AffixStat.ResistancePercent:
                    armor += (int)(armor * affix.Value / 100f);
                    break;
                case AffixStat.MagicFind:
                    magicFind += (int)affix.Value;
                    break;
                case AffixStat.CritChance:
                    critChance += affix.Value / 100f; // 3-12% → 0.03-0.12
                    break;
                case AffixStat.CritDamage:
                    critDamage += affix.Value / 100f; // 10-40% → 倍率加值
                    break;
                case AffixStat.DamageReduction:
                    damageReduction += affix.Value / 100f;
                    break;
                case AffixStat.Thorns:
                    thorns += affix.Value / 100f;
                    break;
            }
        }

        return drop.Slot switch
        {
            ModuleType.Armor => new ArmorModule(drop.Name, shield, armor, hull, damageReduction, thorns),
            ModuleType.Power => new PowerModule(drop.Name),
            ModuleType.Special => new SpecialModule(drop.Name, magicFind),
            _ => new WeaponModule(drop.Name, fp, fr, critChance, critDamage)
        };
    }
}
