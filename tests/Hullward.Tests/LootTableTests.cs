using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Xunit;

namespace Hullward.Tests.Domain;

public class LootTableTests
{
    [Fact]
    public void RollModule_Zone1_NeverDropsAncient()
    {
        var table = new LootTable();
        var rng = new Random(1);

        for (int i = 0; i < 500; i++)
        {
            var drop = table.RollModule(1, rng);
            Assert.NotEqual(ItemRarity.Ancient, drop!.Rarity);
        }
    }

    [Fact]
    public void RollModule_Zone4_NeverDropsCommon()
    {
        var table = new LootTable();
        var rng = new Random(2);

        for (int i = 0; i < 500; i++)
        {
            var drop = table.RollModule(4, rng);
            Assert.NotEqual(ItemRarity.Common, drop!.Rarity);
        }
    }

    [Fact]
    public void RollModule_HigherZone_DropsHigherRarityShare()
    {
        var table = new LootTable();
        var rng1 = new Random(3);
        var rng4 = new Random(4);

        int highInZone1 = CountRareOrBetter(table, 1, rng1, 500);
        int highInZone4 = CountRareOrBetter(table, 4, rng4, 500);

        Assert.True(highInZone4 > highInZone1, $"Zone4 高品质({highInZone4})应多于 Zone1({highInZone1})");
    }

    [Fact]
    public void RollAlloy_IncreasesWithZoneLevel()
    {
        var table = new LootTable();
        var rng = new Random(5);

        long sum1 = 0, sum4 = 0;
        for (int i = 0; i < 200; i++)
        {
            sum1 += table.RollAlloy(1, rng);
            sum4 += table.RollAlloy(4, rng);
        }

        Assert.True(sum4 > sum1, "高阶星域合金产量应更高");
    }

    [Fact]
    public void RollModule_InvalidZone_Throws()
    {
        var table = new LootTable();
        Assert.Throws<ArgumentOutOfRangeException>(() => table.RollModule(0, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.RollModule(5, new Random(1)));
    }

    [Fact]
    public void RollModule_SameSeed_IsDeterministic()
    {
        var table = new LootTable();
        var a = new Random(2026);
        var b = new Random(2026);

        for (int i = 0; i < 50; i++)
        {
            var da = table.RollModule(2, a);
            var db = table.RollModule(2, b);
            Assert.Equal(da!.Rarity, db!.Rarity);
            Assert.Equal(da.Slot, db.Slot);
        }
    }

    [Fact]
    public void ModuleDrop_AlwaysValidSlot()
    {
        var table = new LootTable();
        var rng = new Random(9);
        var valid = new[]
        {
            ModuleType.Weapon, ModuleType.Armor, ModuleType.Power, ModuleType.Special
        };

        for (int i = 0; i < 200; i++)
        {
            var drop = table.RollModule(3, rng);
            Assert.Contains(drop!.Slot, valid);
        }
    }

    private static int CountRareOrBetter(LootTable table, int zone, Random rng, int samples)
    {
        int count = 0;
        for (int i = 0; i < samples; i++)
        {
            var drop = table.RollModule(zone, rng)!;
            if (drop.Rarity >= ItemRarity.Rare)
            {
                count++;
            }
        }
        return count;
    }
}
