using System;
using System.Collections.Generic;
using Hullward.Domain.Loot;

namespace Hullward.Domain.Modules;

/// <summary>词缀属性类型（LD 词缀表 §3.2）。</summary>
public enum AffixStat
{
    // 武器槽
    FirepowerPercent,      // 强化炮击：武器伤害 +%
    AttackSpeedPercent,    // 急速供弹：攻击速度 +%
    CritChance,            // 致命一击：暴击概率 +%
    CritDamage,            // 暴击增幅：暴击伤害 +%
    ShieldPierce,          // 护盾穿透：忽略目标护盾 %
    SlowOnHit,             // 减速磁场：命中减速 %（2s）
    EnergyOnKill,          // 能量反馈：击杀回复能源
    AoeBlast,              // 范围爆破：命中范围爆炸（黄+）
    // 装甲槽
    MaxShieldPercent,      // 护盾扩容：最大护盾 +%
    MaxHullPercent,        // 船体加固：船体耐久 +%
    ResistancePercent,     // 全向抗性：全抗性 +%
    ShieldRegen,           // 纳米修复：护盾每秒恢复 +
    DamageReduction,       // 受击减伤：受击后减伤 %
    Thorns,                // 反伤镀层：反弹近身伤害 %（黄+）
    // 能源槽
    MaxEnergyPercent,      // 能源扩容：能源上限 +%
    EnergyRegenPercent,    // 快速充能：能源回复 +%
    SkillCostPercent,      // 节能模块：技能能耗 -%
    CooldownPercent,       // 冷却缩减：技能冷却 -%
    OverdrivePercent,      // 过载缓冲：过载炮伤害 +%（黄+）
    // 特殊槽
    WarpCooldownPercent,   // 跃迁加速：跃迁冷却 -%
    TargetingRangePercent, // 广域扫描：索敌范围 +%
    MagicFind,             // 打捞增效：MF 寻宝值 +
    EmpDuration,           // 干扰强化：EMP/减速时长 +%
    EmergencyShield        // 应急护盾：濒死触发小护盾（黄+）
}

/// <summary>
/// 模块词缀（LD 词缀表 §3）：一条词缀 = 属性类型 + 数值（百分比或绝对值）。
/// </summary>
public sealed class Affix
{
    public string Name { get; }
    public AffixStat Stat { get; }
    public float Value { get; }

    public Affix(string name, AffixStat stat, float value)
    {
        Name = name;
        Stat = stat;
        Value = value;
    }

    /// <summary>显示文本：词缀名 + 数值（百分比统一四舍五入；触发型词缀无数值只显名）。</summary>
    public string Describe()
    {
        bool triggered = Stat is AffixStat.AoeBlast or AffixStat.EmergencyShield;
        return triggered ? Name : $"{Name} {FormatValue(Stat, Value)}";
    }

    public static string FormatValue(AffixStat stat, float value)
    {
        bool percent = stat switch
        {
            AffixStat.FirepowerPercent or AffixStat.AttackSpeedPercent or AffixStat.CritChance
                or AffixStat.CritDamage or AffixStat.ShieldPierce or AffixStat.SlowOnHit
                or AffixStat.AoeBlast or AffixStat.MaxShieldPercent or AffixStat.MaxHullPercent
                or AffixStat.ResistancePercent or AffixStat.DamageReduction or AffixStat.Thorns
                or AffixStat.MaxEnergyPercent or AffixStat.EnergyRegenPercent or AffixStat.SkillCostPercent
                or AffixStat.CooldownPercent or AffixStat.OverdrivePercent or AffixStat.WarpCooldownPercent
                or AffixStat.TargetingRangePercent or AffixStat.EmpDuration or AffixStat.EmergencyShield
                => true,
            _ => false
        };
        return percent ? $"+{value:0}%" : $"+{value:0}";
    }
}

/// <summary>
/// 词缀池配置（LD 词缀表 §3.2 原样录入）：
/// 每槽位一个词缀池，条目 = 词缀名 + 属性 + 品质数值区间 + 权重 + 最低品质。
/// </summary>
public sealed class AffixPool
{
    public sealed class AffixEntry
    {
        public string Name { get; }
        public AffixStat Stat { get; }
        public float Min { get; }
        public float Max { get; }
        /// <summary>黄/绿（Rare/Set）专属区间；-1 = 未设置，回退普通区间（蓝）。</summary>
        public float RareMin { get; }
        public float RareMax { get; }
        public float AncientMin { get; }
        public float AncientMax { get; }
        public int Weight { get; }
        public ItemRarity MinRarity { get; }

        public AffixEntry(string name, AffixStat stat, float min, float max, float ancientMin, float ancientMax, int weight, ItemRarity minRarity, float rareMin = -1f, float rareMax = -1f)
        {
            Name = name;
            Stat = stat;
            Min = min;
            Max = max;
            RareMin = rareMin;
            RareMax = rareMax;
            AncientMin = ancientMin;
            AncientMax = ancientMax;
            Weight = weight;
            MinRarity = minRarity;
        }
    }

    private static readonly AffixEntry[] WeaponAffixes =
    {
        new("强化炮击", AffixStat.FirepowerPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("急速供弹", AffixStat.AttackSpeedPercent, 4, 7, 12, 15, 18, ItemRarity.Magic),
        new("致命一击", AffixStat.CritChance, 5, 8, 15, 18, 15, ItemRarity.Magic, 8, 12),
        new("暴击增幅", AffixStat.CritDamage, 10, 15, 30, 40, 12, ItemRarity.Magic),
        new("护盾穿透", AffixStat.ShieldPierce, 5, 10, 20, 25, 10, ItemRarity.Magic),
        new("减速磁场", AffixStat.SlowOnHit, 10, 15, 30, 30, 10, ItemRarity.Magic),
        new("能量反馈", AffixStat.EnergyOnKill, 2, 4, 8, 10, 7, ItemRarity.Magic),
        new("范围爆破", AffixStat.AoeBlast, 0, 0, 0, 0, 8, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] ArmorAffixes =
    {
        new("护盾扩容", AffixStat.MaxShieldPercent, 8, 12, 25, 30, 20, ItemRarity.Magic),
        new("船体加固", AffixStat.MaxHullPercent, 8, 12, 25, 30, 18, ItemRarity.Magic),
        new("全向抗性", AffixStat.ResistancePercent, 5, 8, 15, 20, 15, ItemRarity.Magic),
        new("纳米修复", AffixStat.ShieldRegen, 2, 4, 8, 10, 15, ItemRarity.Magic),
        new("受击减伤", AffixStat.DamageReduction, 8, 12, 25, 25, 12, ItemRarity.Magic),
        new("反伤镀层", AffixStat.Thorns, 0, 0, 10, 20, 8, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] PowerAffixes =
    {
        new("能源扩容", AffixStat.MaxEnergyPercent, 8, 12, 25, 30, 22, ItemRarity.Magic),
        new("快速充能", AffixStat.EnergyRegenPercent, 8, 12, 25, 30, 22, ItemRarity.Magic),
        new("节能模块", AffixStat.SkillCostPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("冷却缩减", AffixStat.CooldownPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("过载缓冲", AffixStat.OverdrivePercent, 0, 0, 15, 25, 16, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] SpecialAffixes =
    {
        new("跃迁加速", AffixStat.WarpCooldownPercent, 5, 10, 20, 25, 18, ItemRarity.Magic),
        new("广域扫描", AffixStat.TargetingRangePercent, 10, 15, 30, 30, 18, ItemRarity.Magic),
        new("打捞增效", AffixStat.MagicFind, 5, 10, 30, 50, 25, ItemRarity.Magic),
        new("干扰强化", AffixStat.EmpDuration, 10, 10, 30, 30, 15, ItemRarity.Magic),
        new("应急护盾", AffixStat.EmergencyShield, 0, 0, 25, 25, 12, ItemRarity.Rare)
    };

    public static IReadOnlyList<AffixEntry> ForSlot(ModuleType slot) => slot switch
    {
        ModuleType.Weapon => WeaponAffixes,
        ModuleType.Armor => ArmorAffixes,
        ModuleType.Power => PowerAffixes,
        _ => SpecialAffixes
    };
}

/// <summary>
/// 模块词缀生成器（LD 词缀表 §3.3 roll 规则）：
/// 品质词缀数（白0/蓝1-2/黄2-3/绿3/太古2条暗金区间）、按权重抽取、同模块不重复、数值区间均匀随机。
/// 太古不可洗练；套件词缀（绿固定件）暂以随机条数近似（体系 v2.0 细化）。
/// </summary>
public static class ModuleRoller
{
    /// <summary>品质 → 词缀条数范围（LD §3.1：蓝 1-2、黄 2-3；白 0）。</summary>
    public static (int Min, int Max) AffixCountRange(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => (0, 0),
        ItemRarity.Magic => (1, 2),
        ItemRarity.Rare => (2, 3),
        ItemRarity.Set => (3, 3),        // 绿：固定套件 + 1（套件未落表，近似 3 随机）
        ItemRarity.Ancient => (2, 2),    // 太古：固定强力（取暗金区间）
        _ => (0, 0)
    };

    /// <summary>是否为暗金区间（太古专用强数值）。</summary>
    public static bool IsAncientRoll(ItemRarity rarity) => rarity == ItemRarity.Ancient;

    /// <summary>给掉落模块 roll 词缀（LootTable 生成时调用；太古固定 2 条暗金区间）。</summary>
    public static void RollAffixes(ModuleDrop drop, Random rng)
    {
        drop.Affixes.Clear();
        (int min, int max) = AffixCountRange(drop.Rarity);
        if (max == 0)
        {
            return;
        }
        int count = min == max ? min : rng.Next(min, max + 1);
        var pool = AffixPool.ForSlot(drop.Slot);
        // 按权重预展开候选（同模块不重复）
        var candidates = new List<AffixPool.AffixEntry>();
        foreach (var entry in pool)
        {
            if (entry.MinRarity <= drop.Rarity)
            {
                for (int i = 0; i < entry.Weight; i++)
                {
                    candidates.Add(entry);
                }
            }
        }
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = rng.Next(candidates.Count);
            AffixPool.AffixEntry entry = candidates[pick];
            candidates.RemoveAll(e => e.Stat == entry.Stat); // 同模块不重复
            float value = RollValue(entry, drop.Rarity, rng);
            drop.Affixes.Add(new Affix(entry.Name, entry.Stat, value));
        }
    }

    /// <summary>洗练：黄+ 重 roll 词缀数与数值（太古不可洗，调用方校验）。</summary>
    public static void RerollAffixes(ModuleDrop drop, Random rng)
    {
        drop.Affixes.Clear();
        RollAffixes(drop, rng);
        drop.RerollCount++;
    }

    /// <summary>按品质区间均匀随机数值。</summary>
    private static float RollValue(AffixPool.AffixEntry entry, ItemRarity rarity, Random rng)
    {
        float min, max;
        if (rarity == ItemRarity.Ancient)
        {
            min = entry.AncientMin;
            max = entry.AncientMax;
        }
        else if ((rarity is ItemRarity.Rare or ItemRarity.Set) && entry.RareMin >= 0f)
        {
            // 黄/绿专属区间（LD 线 C C2：致命一击蓝 5-8 / 黄 8-12 / 暗金 15-18）
            min = entry.RareMin;
            max = entry.RareMax;
        }
        else
        {
            min = entry.Min;
            max = entry.Max;
        }
        if (min == 0f && max == 0f)
        {
            // 无暗金列的词缀：黄+ 才可 roll，取非暗金区间
            min = entry.Min;
            max = entry.Max;
        }
        if (max <= min)
        {
            return min;
        }
        return min + (float)rng.NextDouble() * (max - min);
    }
}
