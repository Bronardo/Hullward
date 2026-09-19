using System.Collections.Generic;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Save;

/// <summary>affixsaveentry（LD affix表 §3）。</summary>
public sealed class AffixData
{
    public string Name { get; set; } = "";

    public AffixStat Stat { get; set; }

    public float Value { get; set; }
}

/// <summary>inventorymodulesaveentry（含affix与reroll次数）。</summary>
public sealed class ModuleDropData
{
    public ModuleType Slot { get; set; }

    public ItemRarity Rarity { get; set; }

    /// <summary>品类（Sprint5 A3：old save无此field反serialize为 0 default，见 ToDomain 兼容处理）。</summary>
    public int Category { get; set; } = -1;

    public List<AffixData> Affixes { get; set; } = new();

    public int RerollCount { get; set; }
}

/// <summary>savesnapshot（JSON serialize，UI spec v0.2 §3）。</summary>
public sealed class SaveData
{
    /// <summary>currentsector（1-4）。</summary>
    public int ZoneLevel { get; set; } = 1;

    public int Alloy { get; set; }

    /// <summary>玩家hull（loadrecovery）。</summary>
    public int PlayerHull { get; set; }

    public int ModulesPicked { get; set; }

    /// <summary>inventorymodule。</summary>
    public List<ModuleDropData> Modules { get; set; } = new();

    /// <summary>mothership level（UI spec v0.2 §5/§6）。</summary>
    public int MothershipLevel { get; set; } = 1;

    /// <summary>mothership XP。</summary>
    public int MothershipExp { get; set; }

    /// <summary>current出战fit（slotlist，null = 空槽；长度 ≤ hull槽数）。</summary>
    public List<ModuleDropData?> EquippedSlots { get; set; } = new();

    /// <summary>currentflagship型号（dockselect，default轻巡）。</summary>
    public ShipClass ShipClass { get; set; } = ShipClass.Scout;
}

/// <summary>save ↔ 域objectconvert（affix/reroll次数/fit slot位）。</summary>
public static class SaveDataMapper
{
    public static ModuleDropData ToData(ModuleDrop module)
    {
        var data = new ModuleDropData
        {
            Slot = module.Slot,
            Rarity = module.Rarity,
            RerollCount = module.RerollCount,
            Category = module.Category is null ? -1 : (int)module.Category
        };
        foreach (var affix in module.Affixes)
        {
            data.Affixes.Add(new AffixData { Name = affix.Name, Stat = affix.Stat, Value = affix.Value });
        }
        return data;
    }

    public static ModuleDrop ToDomain(ModuleDropData data)
    {
        var drop = new ModuleDrop(data.Slot, data.Rarity)
        {
            RerollCount = data.RerollCount,
            // old save（Category 反serialize为 -1 或 0 default）→ null，show名按deterministicmap；new save按原品类
            Category = data.Category < 0 ? null : (ModuleCategory)data.Category
        };
        foreach (var affix in data.Affixes)
        {
            drop.Affixes.Add(new Affix(affix.Name, affix.Stat, affix.Value));
        }
        return drop;
    }

    public static List<ModuleDropData?> ToDataSlots(List<ModuleDrop?> slots)
    {
        var result = new List<ModuleDropData?>();
        foreach (var slot in slots)
        {
            result.Add(slot == null ? null : ToData(slot));
        }
        return result;
    }

    public static List<ModuleDrop?> ToDomainSlots(List<ModuleDropData?> slots)
    {
        var result = new List<ModuleDrop?>();
        foreach (var slot in slots)
        {
            result.Add(slot == null ? null : ToDomain(slot));
        }
        return result;
    }
}
