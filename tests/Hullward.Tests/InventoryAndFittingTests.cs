using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;
using Xunit;

namespace Hullward.Tests.Domain;

public class InventoryAndFittingTests
{
    [Fact]
    public void Inventory_AddAndRemove_Works()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));
        inv.AddAlloy(5);

        Assert.Single(inv.Modules);
        Assert.Equal(5, inv.Alloy);

        Assert.True(inv.TryRemoveModule(0, out var removed));
        Assert.Equal(ItemRarity.Rare, removed!.Rarity);
        Assert.Empty(inv.Modules);
    }

    [Fact]
    public void AutoEquipBest_EquipsHighestRarityFirst()
    {
        var ship = new ScoutShip(); // 2 槽
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Common));
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Magic));
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Ancient));

        ShipFitting.AutoEquipBest(ship, inv);

        Assert.Equal(2, ship.Modules.Count); // 槽满即停
        Assert.Single(inv.Modules); // Common 被留下
        Assert.Equal(ItemRarity.Common, inv.Modules[0].Rarity);
        // 先装配的是最高品质（Ancient → +30 火力）
        Assert.Contains(ship.Modules, m => ((WeaponModule)m).FirepowerBonus == 30f);
    }

    [Fact]
    public void AutoEquipBest_StopsWhenSlotsFull()
    {
        var ship = new ScoutShip(); // 2 槽
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Magic));
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Rare));

        ShipFitting.AutoEquipBest(ship, inv);

        Assert.Equal(2, ship.Modules.Count); // 槽满即停
        Assert.Single(inv.Modules); // 剩 1 个未装配
    }

    [Fact]
    public void AutoEquip_WeaponModule_IncreasesFirepower()
    {
        var ship = new ScoutShip(); // 10 火力
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare)); // +10

        ShipFitting.AutoEquipBest(ship, inv);

        Assert.Equal(20f, ship.Firepower);
    }

    [Fact]
    public void AutoEquip_ArmorModule_IncreasesShield()
    {
        var ship = new ScoutShip(); // 40 护盾
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Common)); // +10

        ShipFitting.AutoEquipBest(ship, inv);

        Assert.Equal(50, ship.Shield);
    }

    [Fact]
    public void ResetCombatState_RestoresBaseAndReappliesModules()
    {
        var ship = new ScoutShip();
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));
        inv.AddModule(new ModuleDrop(ModuleType.Armor, ItemRarity.Magic));
        ShipFitting.AutoEquipBest(ship, inv); // 火力 10+10=20, 护盾 40+25=65

        ship.TakeHit(100); // 护盾 0，耐久 20
        ship.ResetCombatState();

        Assert.Equal(120, ship.Hull);
        Assert.Equal(65, ship.Shield); // 基础 40 + 模块 25
        Assert.Equal(20f, ship.Firepower);
    }

    [Fact]
    public void CraftService_Disassemble_ReturnsScrapByRarity()
    {
        var inv = new Inventory();
        inv.AddModule(new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare));

        Assert.True(CraftService.Disassemble(inv, 0));

        Assert.Equal(CraftService.ScrapValue(ItemRarity.Rare), inv.Alloy); // 4
        Assert.Empty(inv.Modules);
    }

    [Fact]
    public void CraftService_Disassemble_InvalidIndex_Fails()
    {
        var inv = new Inventory();
        Assert.False(CraftService.Disassemble(inv, 5));
        Assert.Equal(0, inv.Alloy);
    }

    [Fact]
    public void ScrapValue_MonotonicallyIncreasesWithRarity()
    {
        // LD §4：白1/蓝3/黄8/绿15；太古不可拆（0）
        Assert.True(CraftService.ScrapValue(ItemRarity.Common) < CraftService.ScrapValue(ItemRarity.Magic));
        Assert.True(CraftService.ScrapValue(ItemRarity.Magic) < CraftService.ScrapValue(ItemRarity.Rare));
        Assert.True(CraftService.ScrapValue(ItemRarity.Rare) < CraftService.ScrapValue(ItemRarity.Set));
        Assert.Equal(0, CraftService.ScrapValue(ItemRarity.Ancient));
    }
}
