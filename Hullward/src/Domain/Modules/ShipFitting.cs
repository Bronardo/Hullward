using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Modules;

/// <summary>
/// shipfit（纯 C# 域层）：
/// 将inventorymodule按rarity降序autofit到hull空闲slot，modulestat随rarity递增。
/// </summary>
public static class ShipFitting
{
    /// <summary>rarity → firepowerbonus（weapon/特种）。</summary>
    public static float FirepowerBonus(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 2f,
        ItemRarity.Magic => 5f,
        ItemRarity.Rare => 10f,
        ItemRarity.Set => 18f,
        ItemRarity.Ancient => 30f,
        _ => 2f
    };

    /// <summary>rarity → shieldbonus（armor）。</summary>
    public static int ShieldBonus(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 10,
        ItemRarity.Magic => 25,
        ItemRarity.Rare => 50,
        ItemRarity.Set => 90,
        ItemRarity.Ancient => 150,
        _ => 10
    };

    /// <summary>把inventory最highraritymodule逐个装入空闲slot（fit即消耗）。</summary>
    public static void AutoEquipBest(ShipBase ship, Inventory inventory)
    {
        while (ship.Modules.Count < ship.ModuleSlots)
        {
            int bestIndex = -1;
            ItemRarity bestRarity = ItemRarity.Common;

            for (int i = 0; i < inventory.Modules.Count; i++)
            {
                // bestIndex < 0：首个module兜底选medium，avoidance"只有 Common 时永远不装"的boundary bug
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

    /// <summary>module drop → 具体moduleimplement（stat随rarity；affix按 LD §3 并入bonus）。</summary>
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
        float maxEnergy = 0f;
        float energyRegen = 0f;
        float skillCost = 0f;
        float cooldown = 0f;
        float overdrive = 0f;

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
                case AffixStat.MaxEnergyPercent:
                    maxEnergy += affix.Value; // % 直接传（ShipBase 按当前上限复合扩容）
                    break;
                case AffixStat.EnergyRegenPercent:
                    energyRegen += affix.Value;
                    break;
                case AffixStat.SkillCostPercent:
                    skillCost += affix.Value;
                    break;
                case AffixStat.CooldownPercent:
                    cooldown += affix.Value;
                    break;
                case AffixStat.OverdrivePercent:
                    overdrive += affix.Value;
                    break;
            }
        }

        return drop.Slot switch
        {
            ModuleType.Armor => new ArmorModule(drop.Name, shield, armor, hull, damageReduction, thorns),
            ModuleType.Power => new PowerModule(drop.Name, overdrive, maxEnergy, energyRegen, skillCost, cooldown),
            ModuleType.Special => new SpecialModule(drop.Name, magicFind),
            _ => new WeaponModule(drop.Name, fp, fr, critChance, critDamage)
        };
    }
}
