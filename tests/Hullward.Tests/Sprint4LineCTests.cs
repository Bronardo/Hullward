using System;
using System.Collections.Generic;
using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// Sprint 4 线 C（迭代 13）测试：C2 暴击数值三档上调（蓝 5-8 / 黄 8-12 / 暗金 15-18，
/// LD Sprint4 v0.4.0）+ C3 洗练费用曲线复核（5/10/20/40 递增，经济曲线不崩）。
/// 固定 Random seed 保证确定性（ULO3）。
/// </summary>
public class Sprint4LineCTests
{
    private static readonly Random Seed = new(20260918);

    // ---------- C2：致命一击三档区间 ----------

    [Fact]
    public void CritChance_MagicRolls_InBlueRange()
    {
        var values = RollCritChanceValues(ItemRarity.Magic);
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.InRange(v, 5f, 8f));
    }

    [Fact]
    public void CritChance_RareRolls_InYellowRange()
    {
        var values = RollCritChanceValues(ItemRarity.Rare);
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.InRange(v, 8f, 12f));
    }

    [Fact]
    public void CritChance_SetRolls_InYellowRange()
    {
        var values = RollCritChanceValues(ItemRarity.Set);
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.InRange(v, 8f, 12f));
    }

    [Fact]
    public void CritChance_AncientRolls_InAncientRange()
    {
        var values = RollCritChanceValues(ItemRarity.Ancient);
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.InRange(v, 15f, 18f));
    }

    /// <summary>黄档未设置的词缀（如强化炮击）在 Rare 品质下回退普通（蓝）区间——其他词缀行为不变。</summary>
    [Fact]
    public void AffixWithoutRareTier_RareRolls_FallbackToBlueRange()
    {
        var rng = new Random(Seed.Next());
        var values = new List<float>();
        for (int i = 0; i < 300 && values.Count < 20; i++)
        {
            var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
            ModuleRoller.RollAffixes(drop, rng);
            foreach (var affix in drop.Affixes.Where(a => a.Stat == AffixStat.FirepowerPercent))
            {
                values.Add(affix.Value);
            }
        }
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.InRange(v, 5f, 8f));
    }

    // ---------- C3：洗练费用曲线复核（5/10/20/40… 递增） ----------

    [Fact]
    public void RerollCost_Curve_MatchLdSpec()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        Assert.Equal(5, drop.RerollCost);   // 首次
        drop.RerollCount = 1;
        Assert.Equal(10, drop.RerollCost);
        drop.RerollCount = 2;
        Assert.Equal(20, drop.RerollCost);
        drop.RerollCount = 3;
        Assert.Equal(40, drop.RerollCost);
        drop.RerollCount = 4;
        Assert.Equal(80, drop.RerollCost);  // 第 5 次起 80，继续 ×2 递增（不封顶、可玩性数据驱动）
    }

    [Fact]
    public void Reroll_RejectsWhenAlloyInsufficient()
    {
        var inventory = new Inventory();
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        inventory.AddModule(drop);
        inventory.AddAlloy(4); // 不足 5
        Assert.False(WorkshopService.Reroll(inventory, 0, new Random(1)));
        Assert.Equal(0, drop.RerollCount); // 未扣费未重 roll
    }

    // ---------- helpers ----------

    private static List<float> RollCritChanceValues(ItemRarity rarity)
    {
        var rng = new Random(Seed.Next());
        var values = new List<float>();
        for (int i = 0; i < 400 && values.Count < 20; i++)
        {
            var drop = new ModuleDrop(ModuleType.Weapon, rarity);
            ModuleRoller.RollAffixes(drop, rng);
            foreach (var affix in drop.Affixes.Where(a => a.Stat == AffixStat.CritChance))
            {
                values.Add(affix.Value);
            }
        }
        return values;
    }
}
