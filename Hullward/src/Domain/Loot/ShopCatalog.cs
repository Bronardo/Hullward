using System;
using System.Collections.Generic;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Loot;

/// <summary>
/// 母舰商店（LD 空间站清单 §4：白/蓝模块，合金计价，不卖高阶）：
/// 每次进店刷新 3 件商品（1 白 + 1 蓝 + 1 随机白/蓝），购买后该商品售罄。
/// </summary>
public sealed class ShopCatalog
{
    public sealed class ShopItem
    {
        public ModuleDrop Module { get; }
        public int Price { get; }

        public ShopItem(ModuleDrop module, int price)
        {
            Module = module;
            Price = price;
        }
    }

    private static readonly ModuleType[] Slots =
    {
        ModuleType.Weapon, ModuleType.Armor, ModuleType.Power, ModuleType.Special
    };

    /// <summary>当前在售商品。</summary>
    public List<ShopItem> Items { get; } = new();

    /// <summary>商品定价：白 10 / 蓝 30 合金（LD §4 合金计价）。</summary>
    public static int Price(ItemRarity rarity) => rarity == ItemRarity.Common ? 10 : 30;

    /// <summary>刷新商品（进店调用）。</summary>
    public void Refresh(Random rng)
    {
        Items.Clear();
        Items.Add(MakeItem(ItemRarity.Common, rng));
        Items.Add(MakeItem(ItemRarity.Magic, rng));
        Items.Add(MakeItem(rng.Next(2) == 0 ? ItemRarity.Common : ItemRarity.Magic, rng));
    }

    /// <summary>购买第 index 件商品：扣合金、模块入背包、商品售罄。失败返回 false。</summary>
    public bool TryBuy(Inventory inventory, int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return false;
        }
        ShopItem item = Items[index];
        if (!inventory.SpendAlloy(item.Price))
        {
            return false;
        }
        inventory.AddModule(item.Module);
        Items.RemoveAt(index);
        return true;
    }

    private ShopItem MakeItem(ItemRarity rarity, Random rng)
    {
        ModuleType slot = Slots[rng.Next(Slots.Length)];
        var drop = new ModuleDrop(slot, rarity);
        ModuleRoller.RollAffixes(drop, rng);
        return new ShopItem(drop, Price(rarity));
    }
}
