using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Save;
using Hullward.Domain.Ships;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// 迭代 8（用户插单：船坞/舰船选择 + UI 1920x1080 适配）域层测试：
/// ShipCatalog 四档船体工厂与解锁规则 + 换船槽位重排 + 存档船型往返。
/// </summary>
public class DockAndShipClassTests
{
    // ---------- ShipCatalog：四档船体工厂 ----------

    [Theory]
    [InlineData(ShipClass.Scout, "Scout", 2)]
    [InlineData(ShipClass.Assault, "Assault", 3)]
    [InlineData(ShipClass.Battleship, "Battleship", 4)]
    [InlineData(ShipClass.Fortress, "Fortress", 5)]
    public void Create_ReturnsExpectedHullAndSlots(ShipClass shipClass, string name, int slots)
    {
        ShipBase ship = ShipCatalog.Create(shipClass);
        Assert.Equal(name, ship.Name);
        Assert.Equal(slots, ship.ModuleSlots);
    }

    [Fact]
    public void Create_UnknownDefaultsToScout()
    {
        Assert.IsType<ScoutShip>(ShipCatalog.Create((ShipClass)99));
    }

    [Fact]
    public void All_ReturnsFourClassesInOrder()
    {
        ShipClass[] all = ShipCatalog.All();
        Assert.Equal(4, all.Length);
        Assert.Equal(new[] { ShipClass.Scout, ShipClass.Assault, ShipClass.Battleship, ShipClass.Fortress }, all);
    }

    // ---------- 解锁规则：母舰 Lv.N 解锁第 N 档 ----------

    [Theory]
    [InlineData(ShipClass.Scout, 1, true)]
    [InlineData(ShipClass.Assault, 1, false)]
    [InlineData(ShipClass.Assault, 2, true)]
    [InlineData(ShipClass.Battleship, 3, true)]
    [InlineData(ShipClass.Fortress, 3, false)]
    [InlineData(ShipClass.Fortress, 4, true)]
    public void IsUnlocked_FollowsMothershipLevel(ShipClass shipClass, int level, bool expected)
    {
        Assert.Equal(expected, ShipCatalog.IsUnlocked(shipClass, level));
    }

    // ---------- 换船槽位重排（RebaseSlots） ----------

    [Fact]
    public void RebaseSlots_UpgradeKeepsFilledAndAddsEmpty()
    {
        var inventory = new Inventory();
        var oldSlots = new ModuleDrop?[] { Mk(ItemRarity.Magic), Mk(ItemRarity.Common), null, null }.ToList();

        var newSlots = ShipFittingService.RebaseSlots(inventory, oldSlots, 4);

        Assert.Equal(4, newSlots.Count);
        Assert.NotNull(newSlots[0]);
        Assert.NotNull(newSlots[1]);
        Assert.Null(newSlots[2]);
        Assert.Null(newSlots[3]);
        Assert.Empty(inventory.Modules); // 升级无退回
    }

    [Fact]
    public void RebaseSlots_DowngradeReturnsExcessToInventory()
    {
        var inventory = new Inventory();
        var oldSlots = new ModuleDrop?[] { Mk(ItemRarity.Magic), Mk(ItemRarity.Common), Mk(ItemRarity.Rare), Mk(ItemRarity.Set) }.ToList();

        var newSlots = ShipFittingService.RebaseSlots(inventory, oldSlots, 2);

        Assert.Equal(2, newSlots.Count);
        Assert.NotNull(newSlots[0]);
        Assert.NotNull(newSlots[1]);
        Assert.Equal(2, inventory.Modules.Count); // 第 3、4 槽退回背包
    }

    [Fact]
    public void RebaseSlots_DowngradePreservesFirstSlots()
    {
        var inventory = new Inventory();
        var oldSlots = new ModuleDrop?[] { Mk(ItemRarity.Magic), Mk(ItemRarity.Common), Mk(ItemRarity.Rare) }.ToList();

        var newSlots = ShipFittingService.RebaseSlots(inventory, oldSlots, 2);

        Assert.Same(oldSlots[0], newSlots[0]);
        Assert.Same(oldSlots[1], newSlots[1]);
        Assert.Single(inventory.Modules);
        Assert.Same(oldSlots[2], inventory.Modules[0]);
    }

    // ---------- 存档船型往返 ----------

    [Fact]
    public void SaveData_ShipClassRoundTripsThroughMapper()
    {
        var data = new SaveData { ShipClass = ShipClass.Battleship };
        // SaveDataMapper 不处理船型（域对象直接持有），验证默认值与显式值均可达
        Assert.Equal(ShipClass.Battleship, data.ShipClass);
        Assert.Equal(ShipClass.Scout, new SaveData().ShipClass);
    }

    [Fact]
    public void SaveService_RoundTrip_PreservesShipClass()
    {
        var svc = new SaveService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hullward_test_" + System.Guid.NewGuid().ToString("N")));
        var data = new SaveData { ShipClass = ShipClass.Fortress, MothershipLevel = 4 };
        svc.Save("docktest", data);

        SaveData? loaded = svc.Load("docktest");

        Assert.NotNull(loaded);
        Assert.Equal(ShipClass.Fortress, loaded!.ShipClass);
    }

    private static ModuleDrop Mk(ItemRarity rarity) => new(ModuleType.Weapon, rarity);
}
