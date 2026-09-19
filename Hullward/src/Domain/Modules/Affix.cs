using System;
using System.Collections.Generic;
using Hullward.Domain.Loot;

namespace Hullward.Domain.Modules;

/// <summary>affixattribute类型（LD affix表 §3.2）。</summary>
public enum AffixStat
{
    // weapon slot
    FirepowerPercent,      // reinforcement炮击：weapondamage +%
    AttackSpeedPercent,    // 急速供弹：攻击speed +%
    CritChance,            // fatal一击：critchance +%
    CritDamage,            // crit增幅：crit damage +%
    ShieldPierce,          // shieldpierce：忽略targetshield %
    SlowOnHit,             // slow磁场：hitslow %（2s）
    EnergyOnKill,          // energy反馈：击杀回复能源
    AoeBlast,              // range爆破：hitrange爆炸（黄+）
    // armor slot
    MaxShieldPercent,      // shieldscale out：maxshield +%
    MaxHullPercent,        // hull加固：hull +%
    ResistancePercent,     // 全向抗性：全抗性 +%
    ShieldRegen,           // 纳米fix：shield每秒recovery +
    DamageReduction,       // 受击damage reduction：受击后damage reduction %
    Thorns,                // thorns镀层：反弹近身damage %（黄+）
    // power slot
    MaxEnergyPercent,      // 能源scale out：能源max +%
    EnergyRegenPercent,    // 快速charge：能源回复 +%
    SkillCostPercent,      // 节能module：skillenergy cost -%
    CooldownPercent,       // cooldown缩减：skillcooldown -%
    OverdrivePercent,      // 过载缓冲：过载炮damage +%（黄+）
    // special slot
    WarpCooldownPercent,   // 跃迁加速：跃迁cooldown -%
    TargetingRangePercent, // 广域扫描：索敌range +%
    MagicFind,             // 打捞增效：MF 寻宝值 +
    EmpDuration,           // 干扰reinforcement：EMP/slow时长 +%
    EmergencyShield        // contingencyshield：濒死trigger小shield（黄+）
}

/// <summary>
/// moduleaffix（LD affix表 §3）：一条affix = attribute类型 + stat（百分比或绝对值）。
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

    private static readonly Dictionary<string, string> LegacyNameMap = new()
    {
        ["强化炮击"] = "Powered Cannons", ["急速供弹"] = "Rapid Loading",
        ["致命一击"] = "Critical Hit", ["暴击增幅"] = "Critical Amp",
        ["护盾穿透"] = "Shield Pierce", ["减速磁场"] = "Slow Field",
        ["能量反馈"] = "Energy Feedback", ["范围爆破"] = "Aoe Blast",
        ["护盾扩容"] = "Shield Capacitor", ["船体加固"] = "Hull Reinforcement",
        ["全向抗性"] = "All-Resist", ["纳米修复"] = "Nano Repair",
        ["受击减伤"] = "Damage Reduction", ["反伤镀层"] = "Thorns Plating",
        ["能源扩容"] = "Energy Capacitor", ["快速充能"] = "Fast Recharge",
        ["节能模块"] = "Skill Cost Down", ["冷却缩减"] = "Cooldown Reduction",
        ["过载缓冲"] = "Overdrive Amp",
        ["跃迁加速"] = "Warp Speed", ["广域扫描"] = "Wide Scanner",
        ["打捞增效"] = "Magic Find", ["干扰强化"] = "EMP Amp",
        ["应急护盾"] = "Emergency Shield",
    };

    private string DisplayName => LegacyNameMap.TryGetValue(Name, out var en) ? en : Name;

    /// <summary>showtext：affix name + stat（百分比统一四舍五入；trigger型affix无stat只显名）。</summary>
    public string Describe()
    {
        bool triggered = Stat is AffixStat.AoeBlast or AffixStat.EmergencyShield;
        return triggered ? DisplayName : $"{DisplayName} {FormatValue(Stat, Value)}";
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
/// affix poolconfig（LD affix表 §3.2 原样录入）：
/// 每slot一个affix pool，entry = affix name + attribute + rarity valuesrange + weight + 最lowrarity。
/// </summary>
public sealed class AffixPool
{
    public sealed class AffixEntry
    {
        public string Name { get; }
        public AffixStat Stat { get; }
        public float Min { get; }
        public float Max { get; }
        /// <summary>黄/绿（Rare/Set）专属range；-1 = 未setting，回退普通range（蓝）。</summary>
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
        new("Powered Cannons", AffixStat.FirepowerPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("Rapid Loading", AffixStat.AttackSpeedPercent, 4, 7, 12, 15, 18, ItemRarity.Magic),
        new("Critical Hit", AffixStat.CritChance, 5, 8, 15, 18, 15, ItemRarity.Magic, 8, 12),
        new("Critical Amp", AffixStat.CritDamage, 10, 15, 30, 40, 12, ItemRarity.Magic),
        new("Shield Pierce", AffixStat.ShieldPierce, 5, 10, 20, 25, 10, ItemRarity.Magic),
        new("Slow Field", AffixStat.SlowOnHit, 10, 15, 30, 30, 10, ItemRarity.Magic),
        new("Energy Feedback", AffixStat.EnergyOnKill, 2, 4, 8, 10, 7, ItemRarity.Magic),
        new("Aoe Blast", AffixStat.AoeBlast, 0, 0, 0, 0, 8, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] ArmorAffixes =
    {
        new("Shield Capacitor", AffixStat.MaxShieldPercent, 8, 12, 25, 30, 20, ItemRarity.Magic),
        new("Hull Reinforcement", AffixStat.MaxHullPercent, 8, 12, 25, 30, 18, ItemRarity.Magic),
        new("All-Resist", AffixStat.ResistancePercent, 5, 8, 15, 20, 15, ItemRarity.Magic),
        new("Nano Repair", AffixStat.ShieldRegen, 2, 4, 8, 10, 15, ItemRarity.Magic),
        new("Damage Reduction", AffixStat.DamageReduction, 8, 12, 25, 25, 12, ItemRarity.Magic),
        new("Thorns Plating", AffixStat.Thorns, 0, 0, 10, 20, 8, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] PowerAffixes =
    {
        new("Energy Capacitor", AffixStat.MaxEnergyPercent, 8, 12, 25, 30, 22, ItemRarity.Magic),
        new("Fast Recharge", AffixStat.EnergyRegenPercent, 8, 12, 25, 30, 22, ItemRarity.Magic),
        new("Skill Cost Down", AffixStat.SkillCostPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("Cooldown Reduction", AffixStat.CooldownPercent, 5, 8, 15, 20, 20, ItemRarity.Magic),
        new("Overdrive Amp", AffixStat.OverdrivePercent, 0, 0, 15, 25, 16, ItemRarity.Rare)
    };

    private static readonly AffixEntry[] SpecialAffixes =
    {
        new("Warp Speed", AffixStat.WarpCooldownPercent, 5, 10, 20, 25, 18, ItemRarity.Magic),
        new("Wide Scanner", AffixStat.TargetingRangePercent, 10, 15, 30, 30, 18, ItemRarity.Magic),
        new("Magic Find", AffixStat.MagicFind, 5, 10, 30, 50, 25, ItemRarity.Magic),
        new("EMP Amp", AffixStat.EmpDuration, 10, 10, 30, 30, 15, ItemRarity.Magic),
        new("Emergency Shield", AffixStat.EmergencyShield, 0, 0, 25, 25, 12, ItemRarity.Rare)
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
/// moduleaffixgenerator（LD affix表 §3.3 roll 规则）：
/// rarityaffix count（白0/蓝1-2/黄2-3/绿3/太古2条暗金range）、按weight抽取、同module不重复、statrange均匀random。
/// 太古不可reroll；套件affix（绿fixed件）暂以random条数approximate（体系 v2.0 细化）。
/// </summary>
public static class ModuleRoller
{
    /// <summary>rarity → affix条数range（LD §3.1：蓝 1-2、黄 2-3；白 0）。</summary>
    public static (int Min, int Max) AffixCountRange(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => (0, 0),
        ItemRarity.Magic => (1, 2),
        ItemRarity.Rare => (2, 3),
        ItemRarity.Set => (3, 3),        // 绿：fixed套件 + 1（套件未落表，approximate 3 random）
        ItemRarity.Ancient => (2, 2),    // 太古：fixed强force（取暗金range）
        _ => (0, 0)
    };

    /// <summary>是否为暗金range（太古专用强stat）。</summary>
    public static bool IsAncientRoll(ItemRarity rarity) => rarity == ItemRarity.Ancient;

    /// <summary>给dropmodule roll affix（LootTable 生成时call；太古fixed 2 条暗金range）。</summary>
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
        // 按weight预展开候选（同module不重复）
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
            candidates.RemoveAll(e => e.Stat == entry.Stat); // 同module不重复
            float value = RollValue(entry, drop.Rarity, rng);
            drop.Affixes.Add(new Affix(entry.Name, entry.Stat, value));
        }
    }

    /// <summary>reroll：黄+ 重 roll affix count与stat（太古不可洗，call方validate）。</summary>
    public static void RerollAffixes(ModuleDrop drop, Random rng)
    {
        drop.Affixes.Clear();
        RollAffixes(drop, rng);
        drop.RerollCount++;
    }

    /// <summary>按rarity range均匀randomstat。</summary>
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
            // 黄/绿专属range（LD 线 C C2：fatal一击蓝 5-8 / 黄 8-12 / 暗金 15-18）
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
            // 无暗金column的affix：黄+ 才可 roll，取非暗金range
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
