using System.Collections.Generic;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Save;

/// <summary>背包模块存档条目。</summary>
public sealed class ModuleDropData
{
    public ModuleType Slot { get; set; }

    public ItemRarity Rarity { get; set; }
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
}
