using Hullward.Domain.Modules;

namespace Hullward.Domain.Loot;

/// <summary>
/// 空间站workshop（兼容层）：disassemblemodulesalvagealloy。
/// stat与规则统一delegate <see cref="WorkshopService"/>（LD §4：白1/蓝3/黄8/绿15/太古不可拆）。
/// </summary>
public static class CraftService
{
    public static int ScrapValue(ItemRarity rarity) => WorkshopService.ScrapValue(rarity);

    /// <summary>disassembleinventory第 index 个module：remove并salvagealloy。failback false。</summary>
    public static bool Disassemble(Inventory inventory, int index) => WorkshopService.Disassemble(inventory, index);
}
