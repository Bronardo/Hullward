using System;
using System.Collections.Generic;
using Hullward.Domain.Loot;

namespace Hullward.Domain.Modules;

/// <summary>Affix stat type (LD affix table §3.2)。</summary>
public enum AffixStat
{
    // weapon slot
    FirepowerPercent,      // Powered Cannons: weapon damage +%
    AttackSpeedPercent,    // Rapid Loading: attack speed +%
    CritChance,            // Critical Hit: crit chance +%
    CritDamage,            // Critical Amp: crit damage +%
    ShieldPierce,          // Shield Pierce: ignore target shield %
    SlowOnHit,             // Slow Field: hit slow % (2s)
    EnergyOnKill,          // Energy Feedback: on-kill energy return
    AoeBlast,              // Aoe Blast: hit explodes in range (Rare+)
    // armor slot
    MaxShieldPercent,      // shieldscale out：maxshield +%
    MaxHullPercent,        // Hull Reinforcement: hull +%
    ResistancePercent,     // All-Resist: all resist +%
    ShieldRegen,           // Nano Repair: shield regen per sec +
    DamageReduction,       // Damage Reduction: on-hit damage reduction %
    Thorns,                // Thorns Plating: reflect melee damage % (Rare+)
    // power slot
    MaxEnergyPercent,      // Energy Capacitor: max energy +%
    EnergyRegenPercent,    // Fast Recharge: energy regen +%
    SkillCostPercent,      // Skill Cost Down: skill energy cost -%
    CooldownPercent,       // Cooldown Reduction: skill cooldown -%
    OverdrivePercent,      // Overdrive Amp: overload cannon damage +% (Rare+)
    // special slot
    WarpCooldownPercent,   // Warp Speed: warp cooldown -%
    TargetingRangePercent, // Wide Scanner: targeting range +%
    MagicFind,             // Magic Find: loot bonus +
    EmpDuration,           // EMP Amp: EMP/slow duration +%
    EmergencyShield        // Emergency Shield: on-death trigger small shield (Rare+)
}

/// <summary>
/// Module affix (LD affix table §3): one affix = stat type + value (percent or flat)。
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

    /// <summary>Display text: affix name + value (percent rounded; trigger affixes show name only)。</summary>
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
/// Affix pool config (verbatim from LD affix table §3.2)：
/// One pool per slot; entry = affix name + stat + rarity range + weight + min rarity。
/// </summary>
public sealed class AffixPool
{
    public sealed class AffixEntry
    {
        public string Name { get; }
        public AffixStat Stat { get; }
        public float Min { get; }
        public float Max { get; }
        /// <summary>Rare/Set exclusive range; -1 = unset, fall back to normal (blue) range。</summary>
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
/// Module affix generator (LD affix table §3.3 roll rules)：
/// Rarity-determined affix count (white 0/blue 1-2/yellow 2-3/green 3/ancient 2 ancient-tier), weight-drawn, no duplicate stat per module, uniform random in stat range。
/// Ancient cannot reroll; set affixes (green fixed items) approximated with random count (refine in v2.0)。
/// </summary>
public static class ModuleRoller
{
    /// <summary>Rarity -> affix count range (LD §3.1: blue 1-2, yellow 2-3, white 0)。</summary>
    public static (int Min, int Max) AffixCountRange(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => (0, 0),
        ItemRarity.Magic => (1, 2),
        ItemRarity.Rare => (2, 3),
        ItemRarity.Set => (3, 3),        // Green: fixed set + 1 (set not tabulated, approximate 3 random)
        ItemRarity.Ancient => (2, 2),    // Ancient: fixed strong values (ancient-tier range)
        _ => (0, 0)
    };

    /// <summary>Is this an ancient-tier range (strong stat for ancient only)。</summary>
    public static bool IsAncientRoll(ItemRarity rarity) => rarity == ItemRarity.Ancient;

    /// <summary>Roll affixes for a dropped module (called by LootTable; ancient fixed 2 ancient-tier affixes)。</summary>
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
        // Expand candidates by weight (no duplicate stat per module)
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
            candidates.RemoveAll(e => e.Stat == entry.Stat); // no duplicate stat per module
            float value = RollValue(entry, drop.Rarity, rng);
            drop.Affixes.Add(new Affix(entry.Name, entry.Stat, value));
        }
    }

    /// <summary>Reroll: yellow+ re-rolls affix count and values (ancient cannot reroll; caller validates)。</summary>
    public static void RerollAffixes(ModuleDrop drop, Random rng)
    {
        drop.Affixes.Clear();
        RollAffixes(drop, rng);
        drop.RerollCount++;
    }

    /// <summary>Uniform random in rarity range。</summary>
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
            // Rare/Set exclusive range (LD line C C2: Critical Hit blue 5-8 / yellow 8-12 / ancient 15-18)
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
            // Affixes without ancient column: rollable at yellow+, use non-ancient range
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
