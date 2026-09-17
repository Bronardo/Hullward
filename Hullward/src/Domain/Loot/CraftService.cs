using Hullward.Domain.Modules;

namespace Hullward.Domain.Loot;

/// <summary>
/// 空间站工坊（纯 C# 域层）：拆解模块回收合金。
/// 拆解价值随品质递增：白1 / 蓝2 / 黄4 / 绿8 / 太古16。
/// </summary>
public static class CraftService
{
    public static int ScrapValue(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Magic => 2,
        ItemRarity.Rare => 4,
        ItemRarity.Set => 8,
        ItemRarity.Ancient => 16,
        _ => 1
    };

    /// <summary>拆解背包第 index 个模块：移除并回收合金。失败返回 false。</summary>
    public static bool Disassemble(Inventory inventory, int index)
    {
        if (!inventory.TryRemoveModule(index, out ModuleDrop? drop) || drop == null)
        {
            return false;
        }
        inventory.AddAlloy(ScrapValue(drop.Rarity));
        return true;
    }
}
