using Hullward.Domain.Modules;

namespace Hullward.Domain.Loot;

/// <summary>
/// 空间站工坊（兼容层）：拆解模块回收合金。
/// 数值与规则统一委托 <see cref="WorkshopService"/>（LD §4：白1/蓝3/黄8/绿15/太古不可拆）。
/// </summary>
public static class CraftService
{
    public static int ScrapValue(ItemRarity rarity) => WorkshopService.ScrapValue(rarity);

    /// <summary>拆解背包第 index 个模块：移除并回收合金。失败返回 false。</summary>
    public static bool Disassemble(Inventory inventory, int index) => WorkshopService.Disassemble(inventory, index);
}
