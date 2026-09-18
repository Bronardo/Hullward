using System.Collections.Generic;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Save;

/// <summary>词缀存档条目（LD 词缀表 §3）。</summary>
public sealed class AffixData
{
    public string Name { get; set; } = "";

    public AffixStat Stat { get; set; }

    public float Value { get; set; }
}

/// <summary>背包模块存档条目（含词缀与洗练次数）。</summary>
public sealed class ModuleDropData
{
    public ModuleType Slot { get; set; }

    public ItemRarity Rarity { get; set; }

    /// <summary>品类（Sprint5 A3：旧档无此字段反序列化为 0 默认，见 ToDomain 兼容处理）。</summary>
    public int Category { get; set; } = -1;

    public List<AffixData> Affixes { get; set; } = new();

    public int RerollCount { get; set; }
}

/// <summary>存档快照（JSON 序列化，UI 规格 v0.2 §3）。</summary>
public sealed class SaveData
{
    /// <summary>当前章节（1-4）。</summary>
    public int ZoneLevel { get; set; } = 1;

    public int Alloy { get; set; }

    /// <summary>玩家耐久（读档恢复）。</summary>
    public int PlayerHull { get; set; }

    public int ModulesPicked { get; set; }

    /// <summary>背包模块。</summary>
    public List<ModuleDropData> Modules { get; set; } = new();

    /// <summary>母舰等级（UI 规格 v0.2 §5/§6）。</summary>
    public int MothershipLevel { get; set; } = 1;

    /// <summary>母舰经验。</summary>
    public int MothershipExp { get; set; }

    /// <summary>当前出战装配（槽位列表，null = 空槽；长度 ≤ 船体槽数）。</summary>
    public List<ModuleDropData?> EquippedSlots { get; set; } = new();

    /// <summary>当前旗舰型号（船坞选择，默认轻巡）。</summary>
    public ShipClass ShipClass { get; set; } = ShipClass.Scout;
}

/// <summary>存档 ↔ 域对象转换（词缀/洗练次数/装配槽位）。</summary>
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
            // 旧档（Category 反序列化为 -1 或 0 默认）→ null，显示名按确定性映射；新档按原品类
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
