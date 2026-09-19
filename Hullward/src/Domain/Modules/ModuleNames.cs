using System;
using Hullward.Domain.Loot;

namespace Hullward.Domain.Modules;

/// <summary>
/// module品类（LD Sprint5 命名map v0.1 §2）：每slot 4 个玩家可read品类，drop时random分配，与affix roll 正交。
/// quality prefix（§3）：白=无 / 蓝=改良 / 黄=精锐 / 绿=深烬 / 暗金=太古。
/// </summary>
public enum ModuleCategory
{
    // weapon slot（§2 品类表）
    PulseCannon,   // 脉冲炮：均衡射速
    RailGun,       // 磁轨炮：高单发
    LaserArray,    // 激光阵列：持续输出
    Gatling,       // 速射机炮：多发快攻
    // armor slot
    Composite,     // 复合装甲：均衡耐久
    Reactive,      // 反应装甲：格挡减伤
    NanoCoating,   // 纳米镀层：自修复感
    Phase,         // 相位装甲：闪避感
    // power slot
    FusionCore,    // 聚变核心：能量上限
    Capacitor,     // 电容电池：能量回复
    Reactor,       // 反应炉：能耗效率
    EnergyNode,    // 能源节点：冷却缩减
    // special slot
    Scanner,       // 扫描器：索敌 / MF
    Jammer,        // 干扰器：敌方 debuff
    WarpEngine,    // 跃迁引擎：跃迁 / 速度
    EmergencyShield // 应急护盾：濒死触发
}

/// <summary>
/// moduleshow名service（LD Sprint5 §2-4）：品类名 + quality prefix → 玩家可read名。
/// old save兼容（A3）：Category 未分配（null）时按slot + 名称hashdeterministicmap，showstable、stat不变。
/// </summary>
public static class ModuleNames
{
    /// <summary>品类名（LD §2 表）。</summary>
    public static string CategoryName(ModuleCategory category) => category switch
    {
        ModuleCategory.PulseCannon => "Pulse Cannon",
        ModuleCategory.RailGun => "Railgun",
        ModuleCategory.LaserArray => "Laser Array",
        ModuleCategory.Gatling => "Autocannon",
        ModuleCategory.Composite => "Composite Armor",
        ModuleCategory.Reactive => "Reactive Armor",
        ModuleCategory.NanoCoating => "Nano Plating",
        ModuleCategory.Phase => "Phase Armor",
        ModuleCategory.FusionCore => "Fusion Core",
        ModuleCategory.Capacitor => "Capacitor Cell",
        ModuleCategory.Reactor => "Reactor",
        ModuleCategory.EnergyNode => "Power Node",
        ModuleCategory.Scanner => "Scanner",
        ModuleCategory.Jammer => "Jammer",
        ModuleCategory.WarpEngine => "Warp Engine",
        _ => "Emergency Shield"
    };

    /// <summary>quality prefix（LD §3；白无前缀）。</summary>
    public static string RarityPrefix(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "Improved ",
        ItemRarity.Rare => "Elite ",
        ItemRarity.Set => "Ember ",
        ItemRarity.Ancient => "Ancient ",
        _ => ""
    };

    /// <summary>该slot全部品类（4 选 1 池）。</summary>
    public static ModuleCategory[] ForSlot(ModuleType slot) => slot switch
    {
        ModuleType.Weapon => new[] { ModuleCategory.PulseCannon, ModuleCategory.RailGun, ModuleCategory.LaserArray, ModuleCategory.Gatling },
        ModuleType.Armor => new[] { ModuleCategory.Composite, ModuleCategory.Reactive, ModuleCategory.NanoCoating, ModuleCategory.Phase },
        ModuleType.Power => new[] { ModuleCategory.FusionCore, ModuleCategory.Capacitor, ModuleCategory.Reactor, ModuleCategory.EnergyNode },
        _ => new[] { ModuleCategory.Scanner, ModuleCategory.Jammer, ModuleCategory.WarpEngine, ModuleCategory.EmergencyShield }
    };

    /// <summary>drop时random品类（与affix roll 正交，LD §2）。</summary>
    public static ModuleCategory RandomCategory(ModuleType slot, Random rng)
    {
        var pool = ForSlot(slot);
        return pool[rng.Next(pool.Length)];
    }

    /// <summary>old savedeterministicmap：按slot + 名称hashstable落入该slot 4 品类之一（同档同moduleshow名恒定）。</summary>
    public static ModuleCategory DeterministicFallback(ModuleType slot, string legacyName)
    {
        var pool = ForSlot(slot);
        int hash = 17;
        foreach (char c in legacyName)
        {
            hash = hash * 31 + c;
        }
        return pool[Math.Abs(hash) % pool.Length];
    }

    /// <summary>完整show名：`[quality prefix][品类名]`（LD §4 list项格式）。</summary>
    public static string DisplayName(ModuleDrop drop)
    {
        ModuleCategory category = drop.Category ?? DeterministicFallback(drop.Slot, drop.Name);
        return $"{RarityPrefix(drop.Rarity)}{CategoryName(category)}";
    }
}
