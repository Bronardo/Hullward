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
        Assert.True(ship.Modules.Any(m => ((WeaponModule)m).FirepowerBonus == 30f));
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
        Assert.True(CraftService.ScrapValue(ItemRarity.Common) < CraftService.ScrapValue(ItemRarity.Magic));
        Assert.True(CraftService.ScrapValue(ItemRarity.Magic) < CraftService.ScrapValue(ItemRarity.Rare));
        Assert.True(CraftService.ScrapValue(ItemRarity.Rare) < CraftService.ScrapValue(ItemRarity.Set));
        Assert.True(CraftService.ScrapValue(ItemRarity.Set) < CraftService.ScrapValue(ItemRarity.Ancient));
    }
}
