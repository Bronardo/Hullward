using System;
using System.Collections.Generic;
using System.Linq;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Loot;

/// <summary>装备品质：白蓝黄绿太古（策划 §3.3）。</summary>
public enum ItemRarity
{
    Common,  // 白
    Magic,   // 蓝
    Rare,    // 黄
    Set,     // 绿
    Ancient  // 太古
}

/// <summary>一次模块掉落结果（含词缀，LD 词缀表 §3）。</summary>
public sealed class ModuleDrop
{
    public ModuleType Slot { get; }
    public ItemRarity Rarity { get; }
    public string Name { get; }

    /// <summary>词缀列表（白装为空）。</summary>
    public List<Affix> Affixes { get; } = new();

    /// <summary>洗练次数（费用递增 5×2^n，太古不可洗）。</summary>
    public int RerollCount { get; set; }

    /// <summary>本次洗练费用（LD §4：5/10/20/40…）。</summary>
    public int RerollCost => 5 * (1 << RerollCount);

    public ModuleDrop(ModuleType slot, ItemRarity rarity)
    {
        Slot = slot;
        Rarity = rarity;
        Name = $"{rarity}{slot}模块";
    }

    /// <summary>词缀摘要（多行，用于 UI/存档显示）。</summary>
    public string AffixSummary() => Affixes.Count == 0 ? "（无词缀）" : string.Join("\n", Affixes.Select(a => a.Describe()));
}

/// <summary>
/// 掉落表（纯 C# 域层，可单测）。
/// 星域等级越高，高品质权重越大；合金随等级增产。
/// </summary>
public sealed class LootTable
{
    /// <summary>按星域等级（1-4）的模块品质权重：Safe/Contested/DeepVoid/CollapseZone。</summary>
    private static readonly int[][] ZoneRarityWeights =
    {
        new[] { 60, 25, 12, 3, 0 },  // 安全
        new[] { 35, 35, 22, 7, 1 },  // 争议
        new[] { 15, 30, 32, 18, 5 }, // 无人深空
        new[] { 0, 15, 30, 35, 20 }  // 坍缩禁区
    };

    private static readonly ModuleType[] Slots =
    {
        ModuleType.Weapon, ModuleType.Armor, ModuleType.Power, ModuleType.Special
    };

    public ModuleDrop? RollModule(int zoneLevel, Random rng)
    {
        if (zoneLevel < 1 || zoneLevel > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneLevel), "星域等级 1-4");
        }

        int[] weights = ZoneRarityWeights[zoneLevel - 1];
        ItemRarity rarity = (ItemRarity)WeightedPick(weights, rng);
        if (rarity == ItemRarity.Common && zoneLevel == 4)
        {
            // 坍缩禁区不掉白装（权重为 0，不会到这里）
        }

        ModuleType slot = Slots[rng.Next(Slots.Length)];
        var drop = new ModuleDrop(slot, rarity);
        ModuleRoller.RollAffixes(drop, rng);
        return drop;
    }

    /// <summary>合金掉落量：随等级提升；受打捞增效（MF）加成。</summary>
    public int RollAlloy(int zoneLevel, Random rng, int magicFind = 0)
    {
        if (zoneLevel < 1 || zoneLevel > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneLevel), "星域等级 1-4");
        }
        int baseAmount = zoneLevel switch
        {
            1 => 3,
            2 => 6,
            3 => 10,
            _ => 16
        };
        int rolled = baseAmount + rng.Next(0, zoneLevel + 2);
        return rolled + Math.Max(0, magicFind);
    }

    private static int WeightedPick(int[] weights, Random rng)
    {
        int total = 0;
        foreach (int w in weights)
        {
            total += w;
        }

        int roll = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (roll < acc)
            {
                return i;
            }
        }
        return weights.Length - 1;
    }
}
