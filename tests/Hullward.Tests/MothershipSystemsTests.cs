using System;
using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// 母舰系统域层测试（迭代 6）：
/// 词缀生成（LD §3）、工坊（拆解/洗练/维修，LD §4）、商店（LD §4）、手动装配与配装方案。
/// </summary>
public class MothershipSystemsTests
{
    private static readonly Random Rng = new(20260917);

    // ---------- 词缀生成（LD §3） ----------

    [Fact]
    public void ModuleRoller_Common_HasNoAffixes()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Common);
        ModuleRoller.RollAffixes(drop, Rng);
        Assert.Empty(drop.Affixes);
    }

    [Theory]
    [InlineData(ItemRarity.Magic)]
    [InlineData(ItemRarity.Rare)]
    [InlineData(ItemRarity.Set)]
    public void ModuleRoller_NonCommon_AffixCountWithinRange(ItemRarity rarity)
    {
        (int min, int max) = ModuleRoller.AffixCountRange(rarity);
        for (int i = 0; i < 20; i++)
        {
            var drop = new ModuleDrop(ModuleType.Weapon, rarity);
            ModuleRoller.RollAffixes(drop, Rng);
            Assert.InRange(drop.Affixes.Count, min, max);
        }
    }

    [Fact]
    public void ModuleRoller_Ancient_UsesAncientRanges()
    {
        var drop = new ModuleDrop(ModuleType.Special, ItemRarity.Ancient);
        ModuleRoller.RollAffixes(drop, Rng);
        Assert.Equal(2, drop.Affixes.Count);
        // 太古固定暗金区间：打捞增效 30-50
        var mf = drop.Affixes.FirstOrDefault(a => a.Stat == AffixStat.MagicFind);
        if (mf != null)
        {
            Assert.InRange(mf.Value, 30f, 50f);
        }
    }

    [Fact]
    public void ModuleRoller_NoDuplicateStatsWithinModule()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Set);
        ModuleRoller.RollAffixes(drop, Rng);
        Assert.Equal(drop.Affixes.Count, drop.Affixes.Select(a => a.Stat).Distinct().Count());
    }

    [Fact]
    public void ModuleRoller_WeightedPool_OnlyAffixesValidForSlot()
    {
        var drop = new ModuleDrop(ModuleType.Armor, ItemRarity.Set);
        ModuleRoller.RollAffixes(drop, Rng);
        foreach (var affix in drop.Affixes)
        {
            // 装甲槽词缀池合法属性
            Assert.Contains(affix.Stat, new[]
            {
                AffixStat.MaxShieldPercent, AffixStat.MaxHullPercent, AffixStat.ResistancePercent,
                AffixStat.ShieldRegen, AffixStat.DamageReduction, AffixStat.Thorns
            });
        }
    }

    // ---------- 洗练费用递增（LD §4） ----------

    [Fact]
    public void RerollCost_DoublesPerReroll()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        Assert.Equal(5, drop.RerollCost);
        drop.RerollCount = 1;
        Assert.Equal(10, drop.RerollCost);
        drop.RerollCount = 2;
        Assert.Equal(20, drop.RerollCost);
        drop.RerollCount = 3;
        Assert.Equal(40, drop.RerollCost);
    }

    // ---------- 工坊（LD §4） ----------

    [Fact]
    public void Workshop_Disassemble_ValuesMatchSpec()
    {
        Assert.Equal(1, WorkshopService.ScrapValue(ItemRarity.Common));
        Assert.Equal(3, WorkshopService.ScrapValue(ItemRarity.Magic));
        Assert.Equal(8, WorkshopService.ScrapValue(ItemRarity.Rare));
        Assert.Equal(15, WorkshopService.ScrapValue(ItemRarity.Set));
        Assert.Equal(0, WorkshopService.ScrapValue(ItemRarity.Ancient));
        Assert.False(WorkshopService.CanDisassemble(ItemRarity.Ancient));
    }

    [Fact]
    public void Workshop_Disassemble_Ancient_Refused()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Ancient));
        Assert.False(WorkshopService.Disassemble(inv, 0));
        Assert.Single(inv.Modules);
        Assert.Equal(0, inv.Alloy);
    }

    [Fact]
    public void Workshop_Reroll_ConsumesAlloyAndReRollsAffixes()
    {
        var inv = new Inventory();
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        ModuleRoller.RollAffixes(drop, Rng);
        inv.AddModule(drop);
        inv.AddAlloy(20);

        Assert.True(WorkshopService.Reroll(inv, 0, Rng));

        Assert.Equal(15, inv.Alloy); // 5 合金
        Assert.Equal(1, drop.RerollCount);
        Assert.Equal(10, drop.RerollCost); // 下次费用翻倍
        Assert.InRange(drop.Affixes.Count, 2, 3);
    }

    [Fact]
    public void Workshop_Reroll_CommonOrAncient_Refused()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Common));
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Ancient));
        inv.AddAlloy(100);

        Assert.False(WorkshopService.Reroll(inv, 0, Rng));
        Assert.False(WorkshopService.Reroll(inv, 1, Rng));
    }

    [Fact]
    public void Workshop_Reroll_InsufficientAlloy_Fails()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));
        inv.AddAlloy(4);

        Assert.False(WorkshopService.Reroll(inv, 0, Rng));
        Assert.Equal(4, inv.Alloy);
    }

    [Fact]
    public void Workshop_Repair_CostsByDamageRatio()
    {
        var ship = new ScoutShip(); // 120 耐久
        var inv = new Inventory();

        Assert.Equal(0, WorkshopService.RepairCost(ship)); // 无伤

        ship.TakeHit(60); // 剩 60
        int cost = WorkshopService.RepairCost(ship);
        Assert.True(cost > 0);
        inv.AddAlloy(cost);

        Assert.True(WorkshopService.Repair(ship, inv));
        Assert.Equal(ship.MaxHull, ship.Hull);
        Assert.Equal(0, inv.Alloy);
    }

    [Fact]
    public void Workshop_Repair_InsufficientAlloy_Fails()
    {
        var ship = new ScoutShip();
        ship.TakeHit(60); // 破盾 40 + 耐久 20 → 剩 100
        var inv = new Inventory();

        Assert.False(WorkshopService.Repair(ship, inv));
        Assert.Equal(100, ship.Hull);
    }

    // ---------- 商店（LD §4） ----------

    [Fact]
    public void Shop_Refresh_OffersThreeCommonOrMagicItems()
    {
        var shop = new ShopCatalog();
        shop.Refresh(Rng);

        Assert.Equal(3, shop.Items.Count);
        foreach (var item in shop.Items)
        {
            Assert.Contains(item.Module.Rarity, new[] { ItemRarity.Common, ItemRarity.Magic });
            Assert.Equal(item.Price, ShopCatalog.Price(item.Module.Rarity));
        }
    }

    [Fact]
    public void Shop_Buy_SpendsAlloyAndAddsModule()
    {
        var shop = new ShopCatalog();
        shop.Refresh(Rng);
        var inv = new Inventory();
        inv.AddAlloy(100);

        int price = shop.Items[0].Price;
        Assert.True(shop.TryBuy(inv, 0));

        Assert.Equal(100 - price, inv.Alloy);
        Assert.Single(inv.Modules);
        Assert.Equal(2, shop.Items.Count);
    }

    [Fact]
    public void Shop_Buy_InsufficientAlloy_Fails()
    {
        var shop = new ShopCatalog();
        shop.Refresh(Rng);
        var inv = new Inventory();

        Assert.False(shop.TryBuy(inv, 0));
        Assert.Empty(inv.Modules);
    }

    // ---------- 手动装配与配装方案（LD §4） ----------

    [Fact]
    public void FittingService_EquipAndUnequip_MovesModuleBetweenBagAndSlot()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));
        var slots = ShipFittingService.EmptySlots(2);

        Assert.True(ShipFittingService.TryEquip(inv, slots, 0, 0));
        Assert.Empty(inv.Modules);
        Assert.NotNull(slots[0]);
        Assert.Equal(ItemRarity.Rare, slots[0]!.Rarity);

        Assert.True(ShipFittingService.TryUnequip(inv, slots, 0));
        Assert.Null(slots[0]);
        Assert.Single(inv.Modules);
    }

    [Fact]
    public void FittingService_EquipIntoOccupiedSlot_SwapsBackToInventory()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Magic));
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Rare));
        var slots = ShipFittingService.EmptySlots(2);

        ShipFittingService.TryEquip(inv, slots, 0, 0); // Magic → 槽0
        ShipFittingService.TryEquip(inv, slots, 0, 0); // Rare → 槽0，Magic 回背包

        Assert.Equal(ItemRarity.Rare, slots[0]!.Rarity);
        Assert.Single(inv.Modules);
        Assert.Equal(ItemRarity.Magic, inv.Modules[0].Rarity);
    }

    [Fact]
    public void FittingService_ApplyToShip_AppliesAffixBonuses()
    {
        var inv = new Inventory();
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        drop.Affixes.Add(new Affix("强化炮击", AffixStat.FirepowerPercent, 50f)); // 火力 +50%
        inv.AddModule(drop);
        var slots = ShipFittingService.EmptySlots(1);
        ShipFittingService.TryEquip(inv, slots, 0, 0);

        var ship = new ScoutShip(); // 基础火力 10，Rare 武器 +10 → 词缀 +50% → 10 + 15
        ShipFittingService.ApplyToShip(ship, slots);

        Assert.Equal(25f, ship.Firepower, 1);
        Assert.Equal(1f, ship.FireRateMultiplier, 2); // 无攻速词缀
    }

    [Fact]
    public void FittingService_ApplyToShip_MagicFindAndMaxHull()
    {
        var inv = new Inventory();
        var mf = new ModuleDrop(ModuleType.Special, ItemRarity.Rare);
        mf.Affixes.Add(new Affix("打捞增效", AffixStat.MagicFind, 10f));
        inv.AddModule(mf);
        var hull = new ModuleDrop(ModuleType.Armor, ItemRarity.Rare);
        hull.Affixes.Add(new Affix("船体加固", AffixStat.MaxHullPercent, 50f)); // 品质盾 50 → +25
        inv.AddModule(hull);
        var slots = ShipFittingService.EmptySlots(2);
        ShipFittingService.TryEquip(inv, slots, 0, 0);
        ShipFittingService.TryEquip(inv, slots, 0, 1);

        var ship = new ScoutShip(); // 120 耐久 / 40 盾
        ShipFittingService.ApplyToShip(ship, slots);

        Assert.Equal(10, ship.MagicFind);
        Assert.Equal(145, ship.MaxHull); // 120 + 25
    }

    [Fact]
    public void AutoFit_EquipsHighestIntoEmptySlots()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Common));
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Ancient));
        var slots = ShipFittingService.EmptySlots(2);

        int equipped = AutoFit.AutoEquipIntoSlots(inv, slots);

        Assert.Equal(2, equipped);
        Assert.Equal(ItemRarity.Ancient, slots[0]!.Rarity);
        Assert.Equal(ItemRarity.Common, slots[1]!.Rarity);
        Assert.Empty(inv.Modules);
    }

    [Fact]
    public void Presets_SaveAndApply_RestoresSlotLoadout()
    {
        var inv = new Inventory();
        var rare = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        var magic = new ModuleDrop(ModuleType.Armor, ItemRarity.Magic);
        inv.AddModule(rare);
        inv.AddModule(magic);
        var slots = ShipFittingService.EmptySlots(2);
        ShipFittingService.TryEquip(inv, slots, 0, 0);
        ShipFittingService.TryEquip(inv, slots, 0, 1);

        var presets = new ShipPresets();
        presets.Save('A', "火力套", slots);

        // 换装后应用方案
        ShipFittingService.UnequipAll(inv, slots);
        Assert.True(presets.TryApply('A', inv, slots));
        Assert.Equal(ItemRarity.Rare, slots[0]!.Rarity);
        Assert.Equal(ItemRarity.Magic, slots[1]!.Rarity);
        Assert.Empty(inv.Modules);
    }
}
